using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Web.Script.Serialization;
using Manager.Core;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    public class NexusInstallResult
    {
        public string InstalledPath { get; set; }
        public string BackupPath { get; set; }
        public string DownloadedArchivePath { get; set; }
        public string VerificationSummary { get; set; }
    }

    /// <summary>
    /// Handles download/extract/install workflow for Nexus archives.
    /// </summary>
    public class NexusInstallService
    {
        private readonly IArchiveExtractor _archiveExtractor;

        public NexusInstallService()
            : this(new ZipArchiveExtractor())
        {
        }

        internal NexusInstallService(IArchiveExtractor archiveExtractor)
        {
            _archiveExtractor = archiveExtractor ?? new ZipArchiveExtractor();
        }

        public static void CleanupStartupArtifacts()
        {
            try
            {
                string binRoot = GetManagerBinPath();
                string tempRoot = Path.Combine(binRoot, "_smm_temp");
                if (!Directory.Exists(tempRoot))
                    return;

                // Remove staged archives.
                foreach (var zip in Directory.GetFiles(tempRoot, "*.zip", SearchOption.TopDirectoryOnly))
                {
                    TryDeleteFile(zip);
                }

                // Remove leftover extract folders.
                foreach (var extractDir in Directory.GetDirectories(tempRoot, "extract_*", SearchOption.TopDirectoryOnly))
                {
                    TryDeleteDirectory(extractDir);
                }
            }
            catch
            {
                // Best-effort cleanup only.
            }
        }

        public NexusInstallResult DownloadAndInstall(
            string downloadUrl,
            string modsPath,
            NexusRemoteMod mod,
            NexusRemoteModFile file,
            out string errorMessage)
        {
            errorMessage = null;

            if (string.IsNullOrEmpty(downloadUrl))
            {
                errorMessage = "Download URL is empty.";
                return null;
            }

            if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
            {
                errorMessage = "Mods folder is not configured.";
                return null;
            }

            if (mod == null || mod.ModId <= 0 || string.IsNullOrEmpty(mod.GameDomain))
            {
                errorMessage = "Invalid Nexus mod context.";
                return null;
            }

            if (file == null || file.FileId <= 0)
            {
                errorMessage = "Invalid Nexus file context.";
                return null;
            }

            string binRoot = GetManagerBinPath();
            var tempRoot = Path.Combine(binRoot, "_smm_temp");
            if (!Directory.Exists(tempRoot))
                Directory.CreateDirectory(tempRoot);

            string archivePath = Path.Combine(
                tempRoot,
                "nexus_" + mod.ModId + "_" + file.FileId + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".zip");

            string extractPath = Path.Combine(
                tempRoot,
                "extract_" + mod.ModId + "_" + file.FileId + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

            try
            {
                using (var web = new WebClient())
                {
                    web.Headers["User-Agent"] = AppVersionInfo.UserAgent;
                    web.DownloadFile(downloadUrl, archivePath);
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Download failed: " + ex.Message;
                return null;
            }

            if (!_archiveExtractor.TryExtract(archivePath, extractPath, out errorMessage))
            {
                TryDeleteDirectory(extractPath);
                TryDeleteFile(archivePath);
                return null;
            }

            string sourceModRoot = FindModRoot(extractPath);
            if (string.IsNullOrEmpty(sourceModRoot) || !Directory.Exists(sourceModRoot))
            {
                errorMessage = "Downloaded archive did not contain a recognizable mod folder.";
                return null;
            }

            string targetFolderName = Path.GetFileName(sourceModRoot);
            if (string.IsNullOrEmpty(targetFolderName))
                targetFolderName = "NexusMod_" + mod.ModId;

            string targetPath = Path.Combine(modsPath, targetFolderName);
            string backupPath = null;
            string verificationSummary = string.Empty;

            try
            {
                if (Directory.Exists(targetPath))
                {
                    var backupRoot = Path.Combine(binRoot, "_smm_backup");
                    if (!Directory.Exists(backupRoot))
                        Directory.CreateDirectory(backupRoot);

                    backupPath = Path.Combine(
                        backupRoot,
                        targetFolderName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));

                    Directory.Move(targetPath, backupPath);
                }

                CopyDirectoryRecursive(sourceModRoot, targetPath);
                WriteNexusMetadata(targetPath, mod.GameDomain, mod.ModId);

                if (!VerifyInstalledMod(sourceModRoot, targetPath, mod, file, out verificationSummary, out errorMessage))
                {
                    errorMessage = "Install verification failed: " + errorMessage;
                    RestoreBackupAfterFailedInstall(targetPath, backupPath, ref errorMessage);
                    return null;
                }

                System.Diagnostics.Debug.WriteLine("Nexus install verification passed: " + verificationSummary);
            }
            catch (Exception ex)
            {
                errorMessage = "Install failed: " + ex.Message;
                RestoreBackupAfterFailedInstall(targetPath, backupPath, ref errorMessage);
                return null;
            }
            finally
            {
                TryDeleteDirectory(extractPath);
                TryDeleteFile(archivePath);
            }

            var result = new NexusInstallResult();
            result.InstalledPath = targetPath;
            result.BackupPath = backupPath;
            result.DownloadedArchivePath = string.Empty;
            result.VerificationSummary = verificationSummary;
            return result;
        }

        private static void RestoreBackupAfterFailedInstall(string targetPath, string backupPath, ref string errorMessage)
        {
            if (string.IsNullOrEmpty(targetPath))
                return;

            try
            {
                bool removedFailedInstall = Directory.Exists(targetPath);
                TryDeleteDirectory(targetPath);
                if (!string.IsNullOrEmpty(backupPath) && Directory.Exists(backupPath))
                {
                    Directory.Move(backupPath, targetPath);
                    AppendInstallRollbackMessage(ref errorMessage, "Backup restored after failed install.");
                }
                else if (removedFailedInstall)
                {
                    AppendInstallRollbackMessage(ref errorMessage, "Removed failed install.");
                    System.Diagnostics.Debug.WriteLine("Nexus install rollback: removed failed install at " + targetPath);
                }
            }
            catch (Exception restoreEx)
            {
                string restoreMessage = " Backup restore failed: " + restoreEx.Message;
                errorMessage = string.IsNullOrEmpty(errorMessage)
                    ? restoreMessage.Trim()
                    : errorMessage + restoreMessage;
            }
        }

        private static void AppendInstallRollbackMessage(ref string errorMessage, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            errorMessage = string.IsNullOrEmpty(errorMessage)
                ? message
                : errorMessage + " " + message;
        }

        private static bool VerifyInstalledMod(
            string sourceModRoot,
            string targetPath,
            NexusRemoteMod mod,
            NexusRemoteModFile file,
            out string verificationSummary,
            out string errorMessage)
        {
            verificationSummary = string.Empty;
            errorMessage = null;

            if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            {
                errorMessage = "Installed mod folder is missing.";
                return false;
            }

            global::Manager.ModTypes.ModAboutInfo about;
            string normalizedId;
            string displayName;
            string previewPath;
            if (!global::Manager.ModAboutReader.TryLoad(targetPath, out about, out normalizedId, out displayName, out previewPath) || about == null)
            {
                errorMessage = "Installed mod is missing a readable About/About.json file.";
                return false;
            }

            if (string.IsNullOrEmpty(about.id) || string.IsNullOrEmpty(about.name) || string.IsNullOrEmpty(about.version))
            {
                errorMessage = "Installed About.json is missing required id, name, or version metadata.";
                return false;
            }

            string nexusJson = Path.Combine(Path.Combine(targetPath, "About"), "Nexus.json");
            if (!File.Exists(nexusJson))
            {
                errorMessage = "Installed Nexus metadata file was not written.";
                return false;
            }

            if (!VerifyCopiedFiles(sourceModRoot, targetPath, out errorMessage))
                return false;

            verificationSummary = "About.json id=" + about.id
                + ", copied file set verified"
                + ", nexusModId=" + (mod != null ? mod.ModId.ToString() : "unknown")
                + ", fileId=" + (file != null ? file.FileId.ToString() : "unknown") + ".";
            return true;
        }

        private static bool VerifyCopiedFiles(string sourceModRoot, string targetPath, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(sourceModRoot) || !Directory.Exists(sourceModRoot))
            {
                errorMessage = "Extracted source mod folder is unavailable for verification.";
                return false;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(sourceModRoot, "*", SearchOption.AllDirectories);
            }
            catch (Exception ex)
            {
                errorMessage = "Could not enumerate extracted files for verification: " + ex.Message;
                return false;
            }

            for (int i = 0; i < files.Length; i++)
            {
                string relative = GetRelativePath(sourceModRoot, files[i]);
                string installed = Path.Combine(targetPath, relative);
                if (!File.Exists(installed))
                {
                    errorMessage = "Expected installed file is missing: " + relative;
                    return false;
                }

                try
                {
                    FileInfo sourceInfo = new FileInfo(files[i]);
                    FileInfo installedInfo = new FileInfo(installed);
                    if (sourceInfo.Length != installedInfo.Length)
                    {
                        errorMessage = "Installed file length mismatch: " + relative;
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = "Could not verify installed file '" + relative + "': " + ex.Message;
                    return false;
                }
            }

            return true;
        }

        private static string GetRelativePath(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(fullRoot.Length);
            return Path.GetFileName(path);
        }

        private static string GetManagerBinPath()
        {
            string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (string.IsNullOrEmpty(exeDir))
                return Path.GetTempPath();

            if (string.Equals(Path.GetFileName(exeDir), "bin", StringComparison.OrdinalIgnoreCase))
                return exeDir;

            string binDir = Path.Combine(exeDir, "bin");
            try
            {
                if (!Directory.Exists(binDir))
                    Directory.CreateDirectory(binDir);
            }
            catch
            {
                return exeDir;
            }

            return binDir;
        }

        private static string FindModRoot(string extractPath)
        {
            if (string.IsNullOrEmpty(extractPath) || !Directory.Exists(extractPath))
                return null;

            if (File.Exists(Path.Combine(Path.Combine(extractPath, "About"), "About.json")))
                return extractPath;

            string[] aboutFiles = new string[0];
            try
            {
                aboutFiles = Directory.GetFiles(extractPath, "About.json", SearchOption.AllDirectories);
            }
            catch { }

            string best = null;
            int bestDepth = int.MaxValue;

            foreach (var about in aboutFiles)
            {
                string aboutDir = Path.GetDirectoryName(about);
                if (string.IsNullOrEmpty(aboutDir)) continue;
                if (!string.Equals(Path.GetFileName(aboutDir), "About", StringComparison.OrdinalIgnoreCase)) continue;

                var parent = Directory.GetParent(aboutDir);
                if (parent == null) continue;

                string candidate = parent.FullName;
                int depth = candidate.Split(Path.DirectorySeparatorChar).Length;
                if (depth < bestDepth)
                {
                    best = candidate;
                    bestDepth = depth;
                }
            }

            if (!string.IsNullOrEmpty(best))
                return best;

            var topDirectories = Directory.GetDirectories(extractPath);
            if (topDirectories.Length == 1)
                return topDirectories[0];

            return null;
        }

        private static void WriteNexusMetadata(string installedModPath, string gameDomain, int modId)
        {
            if (string.IsNullOrEmpty(installedModPath) || modId <= 0)
                return;

            string aboutDir = Path.Combine(installedModPath, "About");
            if (!Directory.Exists(aboutDir))
                Directory.CreateDirectory(aboutDir);

            var serializer = new JavaScriptSerializer();

            var jsonData = new
            {
                gameDomain = (gameDomain ?? string.Empty).Trim().ToLowerInvariant(),
                modId = modId
            };

            string jsonPath = Path.Combine(aboutDir, "Nexus.json");
            File.WriteAllText(jsonPath, serializer.Serialize(jsonData));
        }

        private static void CopyDirectoryRecursive(string sourcePath, string destinationPath)
        {
            if (!Directory.Exists(destinationPath))
                Directory.CreateDirectory(destinationPath);

            foreach (var file in Directory.GetFiles(sourcePath))
            {
                string name = Path.GetFileName(file);
                string target = Path.Combine(destinationPath, name);
                File.Copy(file, target, true);
            }

            foreach (var directory in Directory.GetDirectories(sourcePath))
            {
                string name = Path.GetFileName(directory);
                string target = Path.Combine(destinationPath, name);
                CopyDirectoryRecursive(directory, target);
            }
        }

        private static void TryDeleteDirectory(string path)
        {
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
                return;

            try { Directory.Delete(path, true); }
            catch { }
        }

        private static void TryDeleteFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return;

            try { File.Delete(path); }
            catch { }
        }
    }
}
