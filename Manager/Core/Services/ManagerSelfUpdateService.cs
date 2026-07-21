using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Manager.Core;
using ShelteredModManager.Update;

namespace Manager.Core.Services
{
    public sealed class ManagerUpdateStage
    {
        public string WorkspacePath { get; set; }
        public string StagedSmmPath { get; set; }
        public string CurrentSmmPath { get; set; }
        public string BackupPath { get; set; }
        public string RestartPath { get; set; }
        public string UpdaterPath { get; set; }
        public string Version { get; set; }
        public bool RequiresElevation { get; set; }
    }

    public sealed class ManagerUpdateArchiveHashExpectation
    {
        public string ExpectedMd5 { get; set; }
        public string ReleaseMetadata { get; set; }
    }

    public sealed class ManagerSelfUpdateService
    {
        private const long MaximumDownloadBytes = 512L * 1024L * 1024L;
        private readonly IArchiveExtractor _archiveExtractor;

        private sealed class StageContext
        {
            public string CurrentSmmPath;
            public string UpdateRoot;
            public string Workspace;
            public string ArchivePath;
            public string ExtractPath;
            public bool RequiresElevation;
        }

        public ManagerSelfUpdateService()
            : this(new ZipArchiveExtractor(
                4096,
                512L * 1024L * 1024L,
                1024L * 1024L * 1024L))
        {
        }

        internal ManagerSelfUpdateService(IArchiveExtractor archiveExtractor)
        {
            _archiveExtractor = archiveExtractor ?? new ZipArchiveExtractor();
        }

        public static void CleanupStartupArtifacts()
        {
            try
            {
                string localRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShelteredModManager\\Updates");
                CleanupOldPendingDirectories(localRoot);

                string currentSmmPath = Path.GetFullPath(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
                DirectoryInfo parent = Directory.GetParent(currentSmmPath);
                if (parent != null)
                    CleanupOldPendingDirectories(Path.Combine(parent.FullName, "_smm_update"));
            }
            catch { }
        }

        public ManagerUpdateStage DownloadAndStage(
            string downloadUrl,
            string expectedVersion,
            out string errorMessage)
        {
            return DownloadAndStage(downloadUrl, expectedVersion, null, out errorMessage);
        }

        public ManagerUpdateStage DownloadAndStage(
            string downloadUrl,
            string expectedVersion,
            ManagerUpdateArchiveHashExpectation hashExpectation,
            out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(downloadUrl))
            {
                errorMessage = "The manager update download URL is empty.";
                return null;
            }

            StageContext context;
            if (!TryCreateStageContext(out context, out errorMessage))
                return null;
            try
            {
                if (!DownloadBounded(downloadUrl, context.ArchivePath, out errorMessage))
                    return null;
                if (!VerifyDownloadedArchiveHashes(context.ArchivePath, hashExpectation, out errorMessage))
                {
                    DeleteStagedArchive(context.ArchivePath);
                    return null;
                }
                return ExtractAndValidateStage(context, expectedVersion, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = "Could not stage the manager update: " + ex.Message;
                return null;
            }
        }

        public ManagerUpdateStage StageLocalPackage(
            string packagePath,
            string expectedVersion,
            out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(packagePath) || !File.Exists(packagePath))
            {
                errorMessage = "Select a downloaded Sheltered Mod Manager ZIP package.";
                return null;
            }

            try
            {
                var package = new FileInfo(packagePath);
                if (package.Length <= 0 || package.Length > MaximumDownloadBytes)
                {
                    errorMessage = "The manager update package is empty or exceeds the 512 MB safety limit.";
                    return null;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Could not inspect the manager update package: " + ex.Message;
                return null;
            }

            StageContext context;
            if (!TryCreateStageContext(out context, out errorMessage))
                return null;
            try
            {
                File.Copy(packagePath, context.ArchivePath, false);
                return ExtractAndValidateStage(context, expectedVersion, out errorMessage);
            }
            catch (Exception ex)
            {
                errorMessage = "Could not stage the downloaded manager package: " + ex.Message;
                return null;
            }
        }

        private static bool TryCreateStageContext(
            out StageContext context,
            out string errorMessage)
        {
            context = null;
            errorMessage = null;
            string currentSmmPath = Path.GetFullPath(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location));
            DirectoryInfo parent = Directory.GetParent(currentSmmPath);
            string gameRoot = parent != null ? parent.FullName : null;
            if (string.IsNullOrEmpty(gameRoot))
            {
                errorMessage = "Could not resolve the game folder for the manager update.";
                return false;
            }

            string updateRoot = Path.Combine(gameRoot, "_smm_update");
            bool requiresElevation = !CanWriteToDirectory(gameRoot);
            string workspaceRoot = requiresElevation
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShelteredModManager\\Updates")
                : updateRoot;
            string workspace = Path.Combine(workspaceRoot, "pending_" + Guid.NewGuid().ToString("N"));
            try { Directory.CreateDirectory(workspace); }
            catch (Exception ex)
            {
                errorMessage = "Could not create the manager update workspace: " + ex.Message;
                return false;
            }

            context = new StageContext
            {
                CurrentSmmPath = currentSmmPath,
                UpdateRoot = updateRoot,
                Workspace = workspace,
                ArchivePath = Path.Combine(workspace, "manager-update.download"),
                ExtractPath = Path.Combine(workspace, "extract"),
                RequiresElevation = requiresElevation
            };
            return true;
        }

        private ManagerUpdateStage ExtractAndValidateStage(
            StageContext context,
            string expectedVersion,
            out string errorMessage)
        {
            errorMessage = null;
            if (!IsZipArchive(context.ArchivePath))
            {
                errorMessage = "The manager update is not a ZIP archive.";
                return null;
            }
            if (!_archiveExtractor.TryExtract(context.ArchivePath, context.ExtractPath, out errorMessage))
                return null;

            string stagedSmmPath = ResolveStagedSmmPath(context.ExtractPath);
            string stagedVersion;
            if (!ValidateStagedPackage(stagedSmmPath, expectedVersion, out stagedVersion, out errorMessage))
                return null;

            string installedUpdater = Path.Combine(context.CurrentSmmPath, "ManagerUpdater.exe");
            if (!File.Exists(installedUpdater))
            {
                errorMessage = "ManagerUpdater.exe is missing. Reinstall this manager build once before using automatic updates.";
                return null;
            }

            string detachedUpdater = Path.Combine(context.Workspace, "ManagerUpdater.exe");
            File.Copy(installedUpdater, detachedUpdater, true);
            return new ManagerUpdateStage
            {
                WorkspacePath = context.Workspace,
                StagedSmmPath = stagedSmmPath,
                CurrentSmmPath = context.CurrentSmmPath,
                BackupPath = Path.Combine(context.UpdateRoot, "backup_" + Guid.NewGuid().ToString("N")),
                RestartPath = Path.Combine(context.CurrentSmmPath, "Manager.exe"),
                UpdaterPath = detachedUpdater,
                Version = stagedVersion,
                RequiresElevation = context.RequiresElevation
            };
        }

        public bool LaunchUpdater(ManagerUpdateStage stage, out string errorMessage)
        {
            errorMessage = null;
            if (stage == null || string.IsNullOrEmpty(stage.UpdaterPath) || !File.Exists(stage.UpdaterPath))
            {
                errorMessage = "The staged manager updater is unavailable.";
                return false;
            }

            try
            {
                var start = new ProcessStartInfo();
                start.FileName = stage.UpdaterPath;
                start.UseShellExecute = stage.RequiresElevation;
                if (stage.RequiresElevation)
                    start.Verb = "runas";
                start.WorkingDirectory = stage.WorkspacePath;
                start.Arguments =
                    "--parent-pid " + Process.GetCurrentProcess().Id +
                    " --current " + QuoteArgument(stage.CurrentSmmPath) +
                    " --staged " + QuoteArgument(stage.StagedSmmPath) +
                    " --backup " + QuoteArgument(stage.BackupPath) +
                    " --restart " + QuoteArgument(stage.RestartPath);
                Process.Start(start);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Could not start the manager updater: " + ex.Message;
                return false;
            }
        }

        private static bool DownloadBounded(string downloadUrl, string destination, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                Uri requestedUri;
                if (!Uri.TryCreate(downloadUrl, UriKind.Absolute, out requestedUri) ||
                    !string.Equals(requestedUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Manager updates must use an HTTPS download URL.";
                    return false;
                }

                var request = (HttpWebRequest)WebRequest.Create(requestedUri);
                request.Method = "GET";
                request.Timeout = 30000;
                request.ReadWriteTimeout = 30000;
                request.KeepAlive = false;
                request.UserAgent = AppVersionInfo.UserAgent;

                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    if (response.ResponseUri == null ||
                        !string.Equals(response.ResponseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                    {
                        errorMessage = "The manager update redirected to a non-HTTPS URL.";
                        return false;
                    }
                    if (response.ContentLength > MaximumDownloadBytes)
                    {
                        errorMessage = "The manager update exceeds the 512 MB safety limit.";
                        return false;
                    }

                    using (Stream input = response.GetResponseStream())
                    using (FileStream output = File.Open(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    {
                        byte[] buffer = new byte[32768];
                        long total = 0;
                        while (true)
                        {
                            int read = input != null ? input.Read(buffer, 0, buffer.Length) : 0;
                            if (read <= 0)
                                break;

                            total += read;
                            if (total > MaximumDownloadBytes)
                            {
                                errorMessage = "The manager update exceeds the 512 MB safety limit.";
                                return false;
                            }
                            output.Write(buffer, 0, read);
                        }
                    }
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Manager update download failed: " + ex.Message;
                return false;
            }
        }

        private static bool VerifyDownloadedArchiveHashes(
            string archivePath,
            ManagerUpdateArchiveHashExpectation expectation,
            out string errorMessage)
        {
            errorMessage = null;
            string expectedMd5 = expectation != null ? NormalizeHexHash(expectation.ExpectedMd5, 32) : string.Empty;
            string expectedSha256 = ExtractSha256Expectation(expectation != null ? expectation.ReleaseMetadata : null);

            bool hasMd5Input = expectation != null && !string.IsNullOrEmpty(expectation.ExpectedMd5);
            if (hasMd5Input && string.IsNullOrEmpty(expectedMd5))
            {
                errorMessage = "Nexus returned an invalid MD5 hash for the manager update.";
                return false;
            }

            if (!string.IsNullOrEmpty(expectedMd5))
            {
                string actualMd5 = ComputeFileHash(archivePath, MD5.Create());
                if (!string.Equals(actualMd5, expectedMd5, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Manager update archive MD5 verification failed. Expected " +
                        expectedMd5 + " but downloaded " + actualMd5 + ".";
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(expectedSha256))
            {
                string actualSha256 = ComputeFileHash(archivePath, SHA256.Create());
                if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
                {
                    errorMessage = "Manager update archive SHA-256 verification failed. Expected " +
                        expectedSha256 + " but downloaded " + actualSha256 + ".";
                    return false;
                }
            }

            if (string.IsNullOrEmpty(expectedMd5) && string.IsNullOrEmpty(expectedSha256))
                Trace.TraceWarning("Nexus manager update metadata did not include an archive hash; continuing without cryptographic archive verification.");

            return true;
        }

        private static string ComputeFileHash(string path, HashAlgorithm algorithm)
        {
            using (algorithm)
            using (FileStream stream = File.OpenRead(path))
            {
                byte[] hash = algorithm.ComputeHash(stream);
                var builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                    builder.Append(hash[i].ToString("x2"));
                return builder.ToString();
            }
        }

        private static string ExtractSha256Expectation(string metadata)
        {
            if (string.IsNullOrEmpty(metadata))
                return string.Empty;

            Match match = Regex.Match(metadata, @"sha256\s*:\s*([0-9a-fA-F]{64})(?![0-9a-fA-F])", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value.ToLowerInvariant() : string.Empty;
        }

        private static string NormalizeHexHash(string value, int length)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string trimmed = value.Trim();
            if (trimmed.Length != length)
                return string.Empty;

            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                bool isHex = (c >= '0' && c <= '9') ||
                    (c >= 'a' && c <= 'f') ||
                    (c >= 'A' && c <= 'F');
                if (!isHex)
                    return string.Empty;
            }

            return trimmed.ToLowerInvariant();
        }

        private static void DeleteStagedArchive(string archivePath)
        {
            try
            {
                if (!string.IsNullOrEmpty(archivePath) && File.Exists(archivePath))
                    File.Delete(archivePath);
            }
            catch { }
        }

        private static string ResolveStagedSmmPath(string extractPath)
        {
            string nested = Path.Combine(extractPath, "SMM");
            if (File.Exists(Path.Combine(nested, "Manager.exe")))
                return nested;
            if (File.Exists(Path.Combine(extractPath, "Manager.exe")))
                return extractPath;
            return null;
        }

        private static bool ValidateStagedPackage(
            string stagedSmmPath,
            string expectedVersion,
            out string stagedVersion,
            out string errorMessage)
        {
            stagedVersion = null;
            errorMessage = null;
            if (!ManagerPackageContract.TryValidateRoot(stagedSmmPath, out errorMessage))
                return false;
            string managerPath = Path.Combine(stagedSmmPath, "Manager.exe");

            try
            {
                stagedVersion = AssemblyName.GetAssemblyName(managerPath).Version.ToString();
            }
            catch (Exception ex)
            {
                errorMessage = "The staged Manager.exe is invalid: " + ex.Message;
                return false;
            }

            if (!string.IsNullOrEmpty(expectedVersion) &&
                NexusVersionComparer.CompareVersions(stagedVersion, expectedVersion) != 0)
            {
                errorMessage = "The downloaded manager version (" + stagedVersion +
                    ") does not match the Nexus file version (" + expectedVersion + ").";
                return false;
            }
            string installedVersion = Assembly.GetExecutingAssembly().GetName().Version.ToString();
            if (NexusVersionComparer.CompareVersions(stagedVersion, installedVersion) <= 0)
            {
                errorMessage = "The staged manager version (" + stagedVersion +
                    ") is not newer than the installed version (" + installedVersion + ").";
                return false;
            }

            return true;
        }

        private static bool IsZipArchive(string path)
        {
            try
            {
                using (FileStream stream = File.OpenRead(path))
                {
                    if (stream.Length < 4)
                        return false;
                    return stream.ReadByte() == 0x50 && stream.ReadByte() == 0x4b;
                }
            }
            catch { return false; }
        }

        private static bool CanWriteToDirectory(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return false;

            string probe = Path.Combine(directory, ".smm-write-test-" + Guid.NewGuid().ToString("N"));
            try
            {
                using (FileStream stream = File.Open(
                    probe,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                    stream.WriteByte(0);
                }
                File.Delete(probe);
                return true;
            }
            catch
            {
                try
                {
                    if (File.Exists(probe))
                        File.Delete(probe);
                }
                catch { }
                return false;
            }
        }

        private static void CleanupOldPendingDirectories(string root)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return;
            string[] pending = Directory.GetDirectories(root, "pending_*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < pending.Length; i++)
            {
                try
                {
                    if (Directory.GetCreationTimeUtc(pending[i]) < DateTime.UtcNow.AddDays(-7))
                        Directory.Delete(pending[i], true);
                }
                catch { }
            }
        }

        private static string QuoteArgument(string value)
        {
            value = value ?? string.Empty;
            if (value.IndexOf('"') >= 0)
                throw new ArgumentException("Update paths cannot contain quote characters.");
            return "\"" + value + "\"";
        }
    }
}
