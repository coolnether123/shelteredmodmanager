using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    public class NexusInstallResult
    {
        public string InstalledPath { get; set; }
        public string BackupPath { get; set; }
        public string DownloadedArchivePath { get; set; }
    }

    /// <summary>
    /// Handles download/extract/install workflow for Nexus archives.
    /// </summary>
    public class NexusInstallService
    {
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
                    web.Headers["User-Agent"] = "ShelteredModManager/1.3.0";
                    web.DownloadFile(downloadUrl, archivePath);
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Download failed: " + ex.Message;
                return null;
            }

            if (!ExtractZip(archivePath, extractPath, out errorMessage))
                return null;

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
            }
            catch (Exception ex)
            {
                errorMessage = "Install failed: " + ex.Message;
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
            return result;
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

        private static bool ExtractZip(string zipPath, string destination, out string errorMessage)
        {
            errorMessage = null;

            try
            {
                if (!Directory.Exists(destination))
                    Directory.CreateDirectory(destination);

                Type shellType = Type.GetTypeFromProgID("Shell.Application");
                if (shellType == null)
                {
                    errorMessage = "Shell extraction is unavailable on this system.";
                    return false;
                }

                object shell = Activator.CreateInstance(shellType);
                object src = shellType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new object[] { zipPath });
                object dst = shellType.InvokeMember("NameSpace", BindingFlags.InvokeMethod, null, shell, new object[] { destination });

                if (src == null || dst == null)
                {
                    errorMessage = "Could not open archive for extraction.";
                    return false;
                }

                object items = src.GetType().InvokeMember("Items", BindingFlags.InvokeMethod, null, src, null);
                // 16: no UI, 4: no confirmation, 1024: no progress box
                dst.GetType().InvokeMember("CopyHere", BindingFlags.InvokeMethod, null, dst, new object[] { items, 16 + 4 + 1024 });

                DateTime until = DateTime.UtcNow.AddSeconds(45);
                int lastCount = -1;
                int stableTicks = 0;

                while (DateTime.UtcNow < until)
                {
                    int count = 0;
                    try { count = Directory.GetFileSystemEntries(destination).Length; }
                    catch { }

                    if (count > 0)
                    {
                        if (count == lastCount)
                        {
                            stableTicks++;
                            if (stableTicks >= 3)
                                return true;
                        }
                        else
                        {
                            stableTicks = 0;
                        }
                    }

                    lastCount = count;
                    Thread.Sleep(500);
                }

                if (Directory.GetFileSystemEntries(destination).Length == 0)
                {
                    errorMessage = "Archive extraction produced no files.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Archive extraction failed: " + ex.Message;
                return false;
            }
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
