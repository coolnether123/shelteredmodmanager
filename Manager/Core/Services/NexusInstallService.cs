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

        // Archives a player drops on the manager come from anywhere, so extraction
        // is bounded: a crafted zip cannot fill the disk.
        private const int LocalMaximumEntries = 20000;
        private const long LocalMaximumEntryBytes = 512L * 1024 * 1024;
        private const long LocalMaximumExpandedBytes = 2L * 1024 * 1024 * 1024;
        private readonly IArchiveExtractor _localArchiveExtractor;

        public NexusInstallService()
            : this(null)
        {
        }

        /// <param name="archiveExtractor">Replaces both extractors (for tests); null keeps the defaults, with dropped files bounded.</param>
        internal NexusInstallService(IArchiveExtractor archiveExtractor)
        {
            _archiveExtractor = archiveExtractor ?? new ZipArchiveExtractor();
            _localArchiveExtractor = archiveExtractor ?? new ZipArchiveExtractor(LocalMaximumEntries, LocalMaximumEntryBytes, LocalMaximumExpandedBytes);
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
                // Cleanup failures do not replace the primary install result.
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

            string attemptId = Guid.NewGuid().ToString("N");
            string archivePath = Path.Combine(
                tempRoot,
                "nexus_" + mod.ModId + "_" + file.FileId + "_" + attemptId + ".download");

            string extractPath = Path.Combine(
                tempRoot,
                "extract_" + mod.ModId + "_" + file.FileId + "_" + attemptId);

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
                    // Directory.Move is atomic only within one volume. Keep the
                    // rollback copy beside the configured mods so a Manager on
                    // another drive can still update the game safely.
                    var backupRoot = Path.Combine(modsPath, "_smm_backup");
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

        /// <summary>
        /// Installs a mod the user dropped on the manager: a .zip or a mod
        /// folder. It goes through the same checks as Nexus installs (About.json,
        /// reserved names, duplicate IDs, backup and rollback). When a mod with
        /// the same ID is installed, <paramref name="confirmReplace"/> is asked
        /// with the mod's name and installed folder; returning false cancels
        /// with no error.
        /// </summary>
        public NexusInstallResult InstallLocalPackage(
            string packagePath,
            string modsPath,
            Func<string, string, bool> confirmReplace,
            out string installedModId,
            out string errorMessage)
        {
            installedModId = null;
            errorMessage = null;
            if (string.IsNullOrEmpty(modsPath) || !Directory.Exists(modsPath))
            {
                errorMessage = "The mods folder is not set. Choose the game in Game setup first.";
                return null;
            }
            if (!CanWriteToDirectory(modsPath, out errorMessage))
                return null;

            bool isFolder = Directory.Exists(packagePath);
            if (!isFolder && !File.Exists(packagePath))
            {
                errorMessage = "The file no longer exists.";
                return null;
            }
            if (isFolder && IsInsideDirectory(packagePath, modsPath))
            {
                errorMessage = "That folder is already inside the mods folder.";
                return null;
            }

            string extractPath = null;
            string sourceRoot = packagePath;
            try
            {
                if (!isFolder)
                {
                    if (!IsSupportedZipArchive(packagePath))
                    {
                        errorMessage = "Only .zip archives can be installed this way. Extract other archives and drop the mod folder instead.";
                        return null;
                    }
                    string tempRoot = Path.Combine(GetManagerBinPath(), "_smm_temp");
                    if (!Directory.Exists(tempRoot))
                        Directory.CreateDirectory(tempRoot);
                    extractPath = Path.Combine(tempRoot, "extract_local_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", System.Globalization.CultureInfo.InvariantCulture) + "_" + Guid.NewGuid().ToString("N").Substring(0, 6));
                    if (!_localArchiveExtractor.TryExtract(packagePath, extractPath, out errorMessage))
                        return null;
                    sourceRoot = extractPath;
                }

                string modRoot = FindModRoot(sourceRoot);
                if (string.IsNullOrEmpty(modRoot) || !Directory.Exists(modRoot))
                {
                    errorMessage = "This doesn't look like a Sheltered mod: no About/About.json was found inside.";
                    return null;
                }

                global::Manager.ModTypes.ModAboutInfo about;
                string modId;
                if (!ModPackageSafety.TryReadRequiredAbout(modRoot, out about, out modId, out errorMessage))
                {
                    errorMessage = "This isn't a valid SMM mod: " + errorMessage;
                    return null;
                }

                NexusInstallTargetContext targetContext = null;
                string existingPath = FindInstalledModFolder(modsPath, modId);
                if (existingPath != null)
                {
                    if (confirmReplace == null || !confirmReplace(about.name, existingPath))
                        return null;
                    targetContext = new NexusInstallTargetContext();
                    targetContext.ExpectedLocalModId = modId;
                    targetContext.ExistingInstalledPath = existingPath;
                }

                string folderName = SanitizeFolderName(IsSameDirectory(modRoot, sourceRoot) && !isFolder
                    ? FirstNonEmpty(about.id, about.name, "Mod")
                    : Path.GetFileName(modRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)));
                if (string.IsNullOrEmpty(folderName))
                    folderName = SanitizeFolderName(about.id);
                string targetPath = ResolveTargetPath(modsPath, targetContext, folderName);
                if (existingPath == null && Directory.Exists(targetPath))
                {
                    errorMessage = "A different mod already uses the folder name \"" + folderName + "\".";
                    return null;
                }
                if (!ModPackageSafety.ValidateInstallTarget(modsPath, targetPath, modId, targetContext, out errorMessage))
                    return null;

                string backupPath = null;
                bool movedToBackup = false;
                try
                {
                    if (Directory.Exists(targetPath))
                    {
                        string backupRoot = Path.Combine(modsPath, "_smm_backup");
                        if (!Directory.Exists(backupRoot))
                            Directory.CreateDirectory(backupRoot);
                        backupPath = CreateUniqueBackupPath(backupRoot, folderName);
                        Directory.Move(targetPath, backupPath);
                        movedToBackup = true;
                    }

                    CopyDirectoryRecursive(modRoot, targetPath);
                    if (!VerifyCopiedFiles(modRoot, targetPath, out errorMessage))
                    {
                        errorMessage = "Install check failed: " + errorMessage;
                        RestoreBackupAfterFailedInstall(targetPath, backupPath, movedToBackup, ref errorMessage);
                        return null;
                    }
                }
                catch (Exception ex)
                {
                    errorMessage = "Install failed: " + ex.Message;
                    RestoreBackupAfterFailedInstall(targetPath, backupPath, movedToBackup, ref errorMessage);
                    return null;
                }

                installedModId = modId;
                var result = new NexusInstallResult();
                result.InstalledPath = targetPath;
                result.BackupPath = backupPath;
                result.DownloadedArchivePath = string.Empty;
                result.VerificationSummary = "About.json id=" + about.id + ", version=" + about.version + ", copied file set verified.";
                return result;
            }
            finally
            {
                TryDeleteDirectory(extractPath);
            }
        }

        private static string FindInstalledModFolder(string modsPath, string modId)
        {
            string[] directories;
            try
            {
                directories = Directory.GetDirectories(modsPath);
            }
            catch
            {
                return null;
            }
            for (int i = 0; i < directories.Length; i++)
            {
                if (ModPackageSafety.IsReservedModFolderName(Path.GetFileName(directories[i])))
                    continue;
                global::Manager.ModTypes.ModAboutInfo about;
                string normalizedId;
                string readError;
                if (ModPackageSafety.TryReadRequiredAbout(directories[i], out about, out normalizedId, out readError) &&
                    string.Equals(normalizedId, modId, StringComparison.OrdinalIgnoreCase))
                    return directories[i];
            }
            return null;
        }

        private static bool IsInsideDirectory(string path, string directory)
        {
            string fullPath = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            string fullDirectory = EnsureTrailingDirectorySeparator(Path.GetFullPath(directory));
            return fullPath.StartsWith(fullDirectory, StringComparison.OrdinalIgnoreCase);
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

            // Shallowest mod wins; among equally shallow ones the first by path,
            // so the same archive always installs the same folder.
            Array.Sort(aboutFiles, StringComparer.OrdinalIgnoreCase);
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
