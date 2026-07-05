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

    public class NexusInstallTargetContext
    {
        public string ExpectedLocalModId { get; set; }
        public string ExistingInstalledPath { get; set; }
        public string ExpectedVersion { get; set; }

        public static NexusInstallTargetContext FromInstalledMod(ModItem mod)
        {
            if (mod == null)
                return null;

            var context = new NexusInstallTargetContext();
            context.ExpectedLocalModId = mod.Id;
            context.ExistingInstalledPath = mod.RootPath;
            context.ExpectedVersion = mod.NexusRemoteVersion;
            return context;
        }

        public bool HasExpectedLocalMod
        {
            get { return !string.IsNullOrEmpty(ExpectedLocalModId); }
        }
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

                foreach (var download in Directory.GetFiles(tempRoot, "*.download", SearchOption.TopDirectoryOnly))
                {
                    TryDeleteFile(download);
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
            return DownloadAndInstall(downloadUrl, modsPath, mod, file, null, out errorMessage);
        }

        public NexusInstallResult DownloadAndInstall(
            string downloadUrl,
            string modsPath,
            NexusRemoteMod mod,
            NexusRemoteModFile file,
            NexusInstallTargetContext targetContext,
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

            if (!ValidateTargetContext(modsPath, targetContext, out errorMessage))
                return null;

            if (!CanWriteToDirectory(modsPath, out errorMessage))
                return null;

            string binRoot = GetManagerBinPath();
            var tempRoot = Path.Combine(binRoot, "_smm_temp");
            if (!Directory.Exists(tempRoot))
                Directory.CreateDirectory(tempRoot);

            string archivePath = Path.Combine(
                tempRoot,
                "nexus_" + mod.ModId + "_" + file.FileId + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".download");

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

            if (!IsSupportedZipArchive(archivePath))
            {
                TryDeleteFile(archivePath);
                errorMessage = "Unsupported archive format. Direct Nexus install currently supports ZIP archives only; this downloaded file is not a ZIP archive.";
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
                TryDeleteDirectory(extractPath);
                TryDeleteFile(archivePath);
                errorMessage = "Downloaded archive did not contain a recognizable mod folder.";
                return null;
            }

            global::Manager.ModTypes.ModAboutInfo sourceAbout;
            string sourceModId;
            if (!ModPackageSafety.TryReadRequiredAbout(sourceModRoot, out sourceAbout, out sourceModId, out errorMessage))
            {
                TryDeleteDirectory(extractPath);
                TryDeleteFile(archivePath);
                errorMessage = "Downloaded archive is not a valid SMM mod package: " + errorMessage;
                return null;
            }

            string targetFolderName = ResolveInstallFolderName(sourceModRoot, extractPath, sourceAbout, mod);

            string targetPath = ResolveTargetPath(modsPath, targetContext, targetFolderName);
            if (!ModPackageSafety.ValidateInstallTarget(modsPath, targetPath, sourceModId, targetContext, out errorMessage))
            {
                TryDeleteDirectory(extractPath);
                TryDeleteFile(archivePath);
                return null;
            }

            string backupPath = null;
            bool movedToBackup = false;
            string verificationSummary = string.Empty;

            try
            {
                if (Directory.Exists(targetPath))
                {
                    var backupRoot = Path.Combine(binRoot, "_smm_backup");
                    if (!Directory.Exists(backupRoot))
                        Directory.CreateDirectory(backupRoot);

                    backupPath = CreateUniqueBackupPath(backupRoot, targetFolderName);

                    Directory.Move(targetPath, backupPath);
                    movedToBackup = true;
                }

                CopyDirectoryRecursive(sourceModRoot, targetPath);
                WriteNexusMetadata(targetPath, mod.GameDomain, mod.ModId);

                if (!VerifyInstalledMod(sourceModRoot, targetPath, mod, file, targetContext, out verificationSummary, out errorMessage))
                {
                    errorMessage = "Install verification failed: " + errorMessage;
                    RestoreBackupAfterFailedInstall(targetPath, backupPath, movedToBackup, ref errorMessage);
                    return null;
                }

                System.Diagnostics.Debug.WriteLine("Nexus install verification passed: " + verificationSummary);
            }
            catch (Exception ex)
            {
                errorMessage = "Install failed: " + ex.Message;
                RestoreBackupAfterFailedInstall(targetPath, backupPath, movedToBackup, ref errorMessage);
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

        private static bool ValidateTargetContext(string modsPath, NexusInstallTargetContext targetContext, out string errorMessage)
        {
            errorMessage = null;
            if (targetContext == null || string.IsNullOrEmpty(targetContext.ExistingInstalledPath))
                return true;

            string fullModsPath = EnsureTrailingDirectorySeparator(Path.GetFullPath(modsPath));
            string fullTargetPath = Path.GetFullPath(targetContext.ExistingInstalledPath);
            if (!fullTargetPath.StartsWith(fullModsPath, StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Selected installed mod is outside the configured Mods folder.";
                return false;
            }

            if (!Directory.Exists(fullTargetPath))
            {
                errorMessage = "Selected installed mod folder no longer exists: " + fullTargetPath;
                return false;
            }

            return true;
        }

        private static bool CanWriteToDirectory(string directoryPath, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                string testPath = Path.Combine(directoryPath, ".smm_write_test_" + Guid.NewGuid().ToString("N") + ".tmp");
                using (FileStream stream = File.Open(testPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    byte[] marker = System.Text.Encoding.ASCII.GetBytes("SMM");
                    stream.Write(marker, 0, marker.Length);
                }

                TryDeleteFile(testPath);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Mods folder is not writable. Run SMM with permission to update this Sheltered install or choose a writable test install. " + ex.Message;
                return false;
            }
        }

        private static string ResolveTargetPath(
            string modsPath,
            NexusInstallTargetContext targetContext,
            string fallbackFolderName)
        {
            if (targetContext != null && !string.IsNullOrEmpty(targetContext.ExistingInstalledPath))
                return Path.GetFullPath(targetContext.ExistingInstalledPath);

            return Path.Combine(modsPath, fallbackFolderName);
        }

        private static string ResolveInstallFolderName(
            string sourceModRoot,
            string extractPath,
            global::Manager.ModTypes.ModAboutInfo sourceAbout,
            NexusRemoteMod mod)
        {
            string folderName = Path.GetFileName(sourceModRoot);
            if (IsSameDirectory(sourceModRoot, extractPath))
            {
                folderName = FirstNonEmpty(
                    sourceAbout != null ? sourceAbout.id : null,
                    sourceAbout != null ? sourceAbout.name : null,
                    mod != null ? mod.Name : null,
                    mod != null && mod.ModId > 0 ? "NexusMod_" + mod.ModId.ToString() : "NexusMod");
            }

            folderName = SanitizeFolderName(folderName);
            return string.IsNullOrEmpty(folderName) ? "NexusMod" : folderName;
        }

        private static bool IsSameDirectory(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
                return false;

            return string.Equals(
                Path.GetFullPath(left).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetFullPath(right).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            if (values == null)
                return string.Empty;

            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i]))
                    return values[i].Trim();
            }

            return string.Empty;
        }

        private static string SanitizeFolderName(string value)
        {
            string text = string.IsNullOrEmpty(value) ? string.Empty : value.Trim();
            char[] invalid = Path.GetInvalidFileNameChars();
            for (int i = 0; i < invalid.Length; i++)
                text = text.Replace(invalid[i], '_');

            return text.Trim(' ', '.');
        }

        private static string CreateUniqueBackupPath(string backupRoot, string targetFolderName)
        {
            string safeName = string.IsNullOrEmpty(targetFolderName) ? "NexusMod" : targetFolderName;
            foreach (char invalid in Path.GetInvalidFileNameChars())
                safeName = safeName.Replace(invalid, '_');

            string prefix = safeName + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string candidate = Path.Combine(backupRoot, prefix);
            int suffix = 1;
            while (Directory.Exists(candidate) || File.Exists(candidate))
            {
                candidate = Path.Combine(backupRoot, prefix + "_" + suffix.ToString());
                suffix++;
            }

            return candidate;
        }

        private static void RestoreBackupAfterFailedInstall(string targetPath, string backupPath, bool movedToBackup, ref string errorMessage)
        {
            if (string.IsNullOrEmpty(targetPath))
                return;

            try
            {
                if (movedToBackup)
                {
                    TryDeleteDirectory(targetPath);
                    if (!string.IsNullOrEmpty(backupPath) && Directory.Exists(backupPath))
                    {
                        Directory.Move(backupPath, targetPath);
                        AppendInstallRollbackMessage(ref errorMessage, "Backup restored after failed install.");
                    }
                    else
                    {
                        AppendInstallRollbackMessage(ref errorMessage, "Backup was moved but could not be found for restore.");
                    }
                    return;
                }

                if (string.IsNullOrEmpty(backupPath))
                {
                    bool removedFailedInstall = Directory.Exists(targetPath);
                    TryDeleteDirectory(targetPath);
                    if (removedFailedInstall)
                    {
                        AppendInstallRollbackMessage(ref errorMessage, "Removed failed install.");
                        System.Diagnostics.Debug.WriteLine("Nexus install rollback: removed failed install at " + targetPath);
                    }
                    return;
                }

                AppendInstallRollbackMessage(ref errorMessage, "Existing install was left in place because backup creation did not complete.");
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
            NexusInstallTargetContext targetContext,
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
            if (!ModPackageSafety.TryReadRequiredAbout(targetPath, out about, out normalizedId, out errorMessage))
                return false;

            if (targetContext != null && targetContext.HasExpectedLocalMod &&
                !string.Equals(NormalizeModId(about.id), NormalizeModId(targetContext.ExpectedLocalModId), StringComparison.OrdinalIgnoreCase))
            {
                errorMessage = "Installed mod id '" + about.id + "' does not match selected installed mod id '" + targetContext.ExpectedLocalModId + "'.";
                return false;
            }

            string expectedVersion = GetExpectedInstalledVersion(targetContext, mod, file);
            if (!string.IsNullOrEmpty(expectedVersion) &&
                NexusVersionComparer.CompareVersions(about.version, expectedVersion) != 0)
            {
                errorMessage = "Installed mod version '" + about.version + "' does not match expected Nexus version '" + expectedVersion + "'.";
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
                + ", version=" + about.version
                + ", copied file set verified"
                + ", nexusModId=" + (mod != null ? mod.ModId.ToString() : "unknown")
                + ", fileId=" + (file != null ? file.FileId.ToString() : "unknown") + ".";
            return true;
        }

        private static string GetExpectedInstalledVersion(NexusInstallTargetContext targetContext, NexusRemoteMod mod, NexusRemoteModFile file)
        {
            if (file != null && !string.IsNullOrEmpty(file.Version))
                return file.Version.Trim();

            if (mod != null && !string.IsNullOrEmpty(mod.Version))
                return mod.Version.Trim();

            if (targetContext != null && !string.IsNullOrEmpty(targetContext.ExpectedVersion))
                return targetContext.ExpectedVersion.Trim();

            return string.Empty;
        }

        private static string NormalizeModId(string modId)
        {
            return ModPackageSafety.NormalizeModId(modId);
        }

        private static bool IsSupportedZipArchive(string archivePath)
        {
            if (string.IsNullOrEmpty(archivePath) || !File.Exists(archivePath))
                return false;

            try
            {
                using (FileStream stream = File.Open(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length < 4)
                        return false;

                    byte[] header = new byte[4];
                    int read = stream.Read(header, 0, header.Length);
                    if (read < 4)
                        return false;

                    return header[0] == 0x50
                        && header[1] == 0x4b
                        && (header[2] == 0x03 || header[2] == 0x05 || header[2] == 0x07)
                        && (header[3] == 0x04 || header[3] == 0x06 || header[3] == 0x08);
                }
            }
            catch
            {
                return false;
            }
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

        private static string EnsureTrailingDirectorySeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
                return Path.DirectorySeparatorChar.ToString();

            char last = path[path.Length - 1];
            if (last == Path.DirectorySeparatorChar || last == Path.AltDirectorySeparatorChar)
                return path;

            return path + Path.DirectorySeparatorChar;
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
