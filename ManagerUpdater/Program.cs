using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ShelteredModManager.Update;

namespace ManagerUpdater
{
    internal static class Program
    {
        private const string Usage =
            "Sheltered Mod Manager Updater\n\n" +
            "Usage:\n" +
            "  ManagerUpdater.exe --parent-pid <pid> --current <SMM directory>\n" +
            "    --staged <staged SMM directory> --backup <backup directory>\n" +
            "    --restart <current SMM Manager.exe>\n\n" +
            "The current and backup directories must be on the same volume. The backup path must not exist.";

        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length == 1 && IsHelp(args[0]))
            {
                MessageBox.Show(Usage, "Manager Updater Help", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return 0;
            }

            string logPath = CreateLogPath();
            UpdateLog log = new UpdateLog(logPath);
            try
            {
                log.Write("Updater started.");
                UpdateArguments options = UpdateArguments.Parse(args);
                options.Validate(log);

                using (Mutex updateMutex = new Mutex(false, @"Local\ShelteredModManager.Updater"))
                {
                    bool acquired;
                    try { acquired = updateMutex.WaitOne(0, false); }
                    catch (AbandonedMutexException) { acquired = true; }
                    if (!acquired)
                        throw new InvalidOperationException("Another manager update is already running.");
                    try
                    {
                        WaitForParent(options.ParentProcessId, log);
                        ApplyUpdate(options, log);
                    }
                    finally
                    {
                        try { updateMutex.ReleaseMutex(); }
                        catch { }
                    }
                }
                log.Write("Update completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                log.Write("Update failed: " + ex);
                if (!string.Equals(
                    Environment.GetEnvironmentVariable("SMM_UPDATER_NO_UI"),
                    "1",
                    StringComparison.Ordinal))
                {
                    MessageBox.Show(
                        "Sheltered Mod Manager could not be updated.\n\n" +
                        ex.Message + "\n\nLog: " + logPath,
                        "Manager Update Failed",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
                return 1;
            }
            finally
            {
                log.Dispose();
            }
        }

        private static bool IsHelp(string value)
        {
            return string.Equals(value, "--help", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "-h", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(value, "/?", StringComparison.OrdinalIgnoreCase);
        }

        private static string CreateLogPath()
        {
            string directory = Path.Combine(Path.GetTempPath(), "ShelteredModManager");
            Directory.CreateDirectory(directory);
            string fileName = string.Format(
                CultureInfo.InvariantCulture,
                "ManagerUpdater-{0:yyyyMMdd-HHmmss}-{1}.log",
                DateTime.UtcNow,
                Process.GetCurrentProcess().Id);
            return Path.Combine(directory, fileName);
        }

        private static void WaitForParent(int parentProcessId, UpdateLog log)
        {
            if (parentProcessId == Process.GetCurrentProcess().Id)
                throw new InvalidOperationException("The parent PID cannot be the updater process.");

            try
            {
                using (Process parent = Process.GetProcessById(parentProcessId))
                {
                    log.Write("Waiting for parent process " + parentProcessId + " to exit.");
                    if (!parent.WaitForExit(120000))
                        throw new TimeoutException("The running manager did not close within two minutes.");
                    log.Write("Parent process exited.");
                }
            }
            catch (ArgumentException)
            {
                log.Write("Parent process is already stopped.");
            }
        }

        private static void ApplyUpdate(UpdateArguments options, UpdateLog log)
        {
            bool currentMoved = false;
            bool stagedMoved = false;
            string promotionDirectory = null;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(options.BackupDirectory));
                promotionDirectory = PreparePromotionDirectory(options, log);

                log.Write("Moving current installation to backup.");
                Directory.Move(options.CurrentDirectory, options.BackupDirectory);
                currentMoved = true;

                log.Write("Moving staged installation into place.");
                Directory.Move(promotionDirectory, options.CurrentDirectory);
                stagedMoved = true;

                PreserveLocalState(options, log);
                ValidateInstalledManager(options);
                StartManager(options, log);

            }
            catch
            {
                if (currentMoved)
                {
                    try { RollBack(options, promotionDirectory, stagedMoved, log); }
                    catch (Exception rollbackError)
                    {
                        log.Write("Rollback failed. Previous installation backup: " + options.BackupDirectory + ". " + rollbackError);
                        throw new IOException("Update rollback failed. Restore the previous installation from " + options.BackupDirectory, rollbackError);
                    }
                }
                TryRestartPreviousManager(options, log);
                throw;
            }
            // Maintenance cannot invalidate an installation that has started successfully.
            try { CleanupOldBackups(options.BackupDirectory, log); }
            catch (Exception ex) { log.Write("Backup cleanup failed: " + ex.Message); }
            try { CleanupRecoveryFolders(options, promotionDirectory, log); }
            catch (Exception ex) { log.Write("Recovery folder cleanup failed: " + ex.Message); }
        }

        private static string PreparePromotionDirectory(UpdateArguments options, UpdateLog log)
        {
            string currentVolume = VolumeIdentity.Get(options.CurrentDirectory);
            string stagedVolume = VolumeIdentity.Get(options.StagedDirectory);
            if (string.Equals(currentVolume, stagedVolume, StringComparison.OrdinalIgnoreCase))
                return options.StagedDirectory;

            string promotionDirectory = Path.Combine(
                Path.GetDirectoryName(options.BackupDirectory),
                "promotion-" + Guid.NewGuid().ToString("N"));
            log.Write("Copying cross-volume staging payload to " + promotionDirectory + ".");
            CopyDirectory(options.StagedDirectory, promotionDirectory);
            if (!File.Exists(Path.Combine(promotionDirectory, "Manager.exe")))
                throw new InvalidDataException("The copied promotion payload does not contain Manager.exe.");
            return promotionDirectory;
        }

        private static void PreserveLocalState(UpdateArguments options, UpdateLog log)
        {
            CopyFileIfPresent(
                Path.Combine(options.BackupDirectory, @"bin\mod_manager.ini"),
                Path.Combine(options.CurrentDirectory, @"bin\mod_manager.ini"),
                log);
            CopyFileIfPresent(
                Path.Combine(options.BackupDirectory, @"bin\manager_options.json"),
                Path.Combine(options.CurrentDirectory, @"bin\manager_options.json"),
                log);
            CopyFileIfPresent(
                Path.Combine(options.BackupDirectory, "mod_manager.log"),
                Path.Combine(options.CurrentDirectory, "mod_manager.log"),
                log);
            CopyDirectoryIfPresent(
                Path.Combine(options.BackupDirectory, "nexus_uploads"),
                Path.Combine(options.CurrentDirectory, "nexus_uploads"),
                log);
            CopyDirectoryIfPresent(
                Path.Combine(options.BackupDirectory, @"bin\_nxm_inbox"),
                Path.Combine(options.CurrentDirectory, @"bin\_nxm_inbox"),
                log);
            CopyDirectoryIfPresent(
                Path.Combine(options.BackupDirectory, "Feedback"),
                Path.Combine(options.CurrentDirectory, "Feedback"),
                log);
        }

        private static void CopyFileIfPresent(string source, string destination, UpdateLog log)
        {
            if (!File.Exists(source))
                return;

            string parent = Path.GetDirectoryName(destination);
            if (!Directory.Exists(parent))
                Directory.CreateDirectory(parent);
            File.Copy(source, destination, true);
            log.Write("Preserved " + source + ".");
        }

        private static void CopyDirectoryIfPresent(string source, string destination, UpdateLog log)
        {
            if (!Directory.Exists(source))
                return;

            CopyDirectory(source, destination);
            log.Write("Preserved " + source + ".");
        }

        private static void CopyDirectory(string source, string destination)
        {
            Directory.CreateDirectory(destination);
            string[] files = Directory.GetFiles(source);
            for (int i = 0; i < files.Length; i++)
            {
                string target = Path.Combine(destination, Path.GetFileName(files[i]));
                File.Copy(files[i], target, true);
            }

            string[] directories = Directory.GetDirectories(source);
            for (int i = 0; i < directories.Length; i++)
            {
                string target = Path.Combine(destination, Path.GetFileName(directories[i]));
                CopyDirectory(directories[i], target);
            }
        }

        private static void ValidateInstalledManager(UpdateArguments options)
        {
            string contractError;
            if (!ManagerPackageContract.TryValidateRoot(options.CurrentDirectory, out contractError))
                throw new InvalidDataException(contractError);
            if (!File.Exists(options.RestartExecutable))
                throw new FileNotFoundException("The restart executable was not installed.", options.RestartExecutable);
        }

        private static void StartManager(UpdateArguments options, UpdateLog log)
        {
            if (string.Equals(
                Environment.GetEnvironmentVariable("SMM_UPDATER_TEST_FAIL_BEFORE_RESTART"),
                "1",
                StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Injected updater restart failure.");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = options.RestartExecutable;
            startInfo.WorkingDirectory = options.CurrentDirectory;
            startInfo.UseShellExecute = false;
            string healthDirectory = Path.Combine(Path.GetTempPath(), "ShelteredModManager");
            Directory.CreateDirectory(healthDirectory);
            string healthFile = Path.Combine(
                healthDirectory,
                "ManagerHealth-" + Guid.NewGuid().ToString("N") + ".ready");
            startInfo.Arguments = "--update-health-file " + QuoteArgument(healthFile);
            log.Write("Restarting " + options.RestartExecutable + ".");

            Process process = ManagerProcessLauncher.Start(startInfo, log);
            if (process == null)
                throw new InvalidOperationException("Manager.exe could not be restarted.");
            try
            {
                DateTime deadline = DateTime.UtcNow.AddSeconds(30);
                while (DateTime.UtcNow < deadline)
                {
                    if (File.Exists(healthFile))
                    {
                        if (process.WaitForExit(1000))
                            throw new InvalidOperationException("The updated manager exited immediately after reporting startup.");
                        log.Write("Updated manager reported a healthy UI startup.");
                        TryDeleteFile(healthFile);
                        return;
                    }
                    process.Refresh();
                    if (process.HasExited)
                        throw new InvalidOperationException("The updated manager exited before reporting a healthy startup.");
                    System.Threading.Thread.Sleep(200);
                }

                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                        process.WaitForExit(5000);
                    }
                }
                catch { }
                throw new TimeoutException("The updated manager did not report a healthy startup within 30 seconds.");
            }
            finally
            {
                process.Dispose();
                TryDeleteFile(healthFile);
            }
        }

        private static void TryRestartPreviousManager(UpdateArguments options, UpdateLog log)
        {
            if (!File.Exists(options.RestartExecutable))
                return;
            try
            {
                var start = new ProcessStartInfo();
                start.FileName = options.RestartExecutable;
                start.WorkingDirectory = options.CurrentDirectory;
                start.UseShellExecute = false;
                Process process = ManagerProcessLauncher.Start(start, log);
                if (process != null)
                    process.Dispose();
                log.Write("Restarted the previous manager after update failure.");
            }
            catch (Exception ex)
            {
                log.Write("Could not restart the previous manager after rollback: " + ex.Message);
            }
        }

        private static void CleanupOldBackups(string retainedBackup, UpdateLog log)
        {
            string parent = Path.GetDirectoryName(retainedBackup);
            if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
                return;
            string[] backups = Directory.GetDirectories(parent, "backup_*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < backups.Length; i++)
            {
                if (string.Equals(backups[i], retainedBackup, StringComparison.OrdinalIgnoreCase))
                    continue;
                try
                {
                    Directory.Delete(backups[i], true);
                    log.Write("Removed superseded rollback backup " + backups[i] + ".");
                }
                catch (Exception ex)
                {
                    log.Write("Could not remove old rollback backup " + backups[i] + ": " + ex.Message);
                }
            }
        }

        private static void CleanupRecoveryFolders(UpdateArguments options, string promotionDirectory, UpdateLog log)
        {
            string parent = Path.GetDirectoryName(options.BackupDirectory);
            foreach (string pattern in new string[] { "failed-update-*", "promotion-*" })
            {
                DirectoryInfo[] folders = new DirectoryInfo(parent).GetDirectories(pattern);
                Array.Sort(folders, delegate(DirectoryInfo a, DirectoryInfo b)
                {
                    int order = b.LastWriteTimeUtc.CompareTo(a.LastWriteTimeUtc);
                    return order != 0 ? order : StringComparer.OrdinalIgnoreCase.Compare(a.FullName, b.FullName);
                });
                // Keep the newest recovery folder of each kind, even after a healthy startup.
                for (int i = 1; i < folders.Length; i++)
                {
                    string path = folders[i].FullName;
                    bool protectedPath = false;
                    foreach (string active in new string[] { options.CurrentDirectory, options.StagedDirectory, options.BackupDirectory, promotionDirectory })
                    {
                        if (!string.IsNullOrEmpty(active) &&
                            (string.Equals(path, active, StringComparison.OrdinalIgnoreCase) ||
                             active.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)))
                            protectedPath = true;
                    }
                    if (protectedPath || (folders[i].Attributes & FileAttributes.ReparsePoint) != 0)
                        continue;
                    try
                    {
                        folders[i].Delete(true);
                        log.Write("Removed stale recovery folder " + path + ".");
                    }
                    catch (Exception ex) { log.Write("Could not remove recovery folder " + path + ": " + ex.Message); }
                }
            }
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('"') >= 0)
                throw new ArgumentException("Updater argument contains an invalid path.");
            return "\"" + value + "\"";
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                    File.Delete(path);
            }
            catch { }
        }

        private static void RollBack(
            UpdateArguments options,
            string promotionDirectory,
            bool stagedMoved,
            UpdateLog log)
        {
            log.Write("Rolling back the update.");
            Exception displacementFailure = null;

            if (stagedMoved && Directory.Exists(options.CurrentDirectory))
            {
                try
                {
                    Directory.Move(options.CurrentDirectory, promotionDirectory);
                    log.Write("Moved the failed update back to its staging path.");
                }
                catch (Exception ex)
                {
                    displacementFailure = ex;
                    log.Write("Could not return the failed update to staging: " + ex);
                    TryDisplaceFailedInstallation(options, log);
                }
            }

            if (!Directory.Exists(options.CurrentDirectory) && Directory.Exists(options.BackupDirectory))
            {
                Directory.Move(options.BackupDirectory, options.CurrentDirectory);
                log.Write("Restored the previous installation.");
            }

            if (Directory.Exists(options.BackupDirectory) || !Directory.Exists(options.CurrentDirectory))
                throw new IOException("Rollback could not restore the previous installation. Backup: " + options.BackupDirectory, displacementFailure);
        }

        private static void TryDisplaceFailedInstallation(UpdateArguments options, UpdateLog log)
        {
            string parent = Path.GetDirectoryName(options.BackupDirectory);
            string failedPath = Path.Combine(
                parent,
                "failed-update-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
            Directory.Move(options.CurrentDirectory, failedPath);
            log.Write("Moved the failed installation to " + failedPath + ".");
        }
    }

    internal static class ManagerProcessLauncher
    {
        public static Process Start(ProcessStartInfo start, UpdateLog log)
        {
            try
            {
                using (System.Security.Principal.WindowsIdentity identity = System.Security.Principal.WindowsIdentity.GetCurrent())
                {
                    if (!IsElevated(identity.Token))
                        return Process.Start(start);
                }
                return StartWithShellToken(start, log);
            }
            catch (Exception ex)
            {
                log.Write("Cannot restart Manager at normal integrity. No elevated restart was attempted. Open Manager manually. " + ex.Message);
                throw;
            }
        }

        private static bool IsElevated(IntPtr token)
        {
            int elevated, length;
            if (!GetTokenInformation(token, 20, out elevated, sizeof(int), out length))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            return elevated != 0;
        }

        private static Process StartWithShellToken(ProcessStartInfo start, UpdateLog log)
        {
            IntPtr shell = GetShellWindow();
            if (shell == IntPtr.Zero)
                throw new InvalidOperationException("No desktop shell is available.");
            uint shellPid;
            GetWindowThreadProcessId(shell, out shellPid);
            IntPtr shellProcess = OpenProcess(0x1000, false, shellPid); // PROCESS_QUERY_LIMITED_INFORMATION
            IntPtr token = IntPtr.Zero, primary = IntPtr.Zero, environment = IntPtr.Zero;
            try
            {
                if (shellProcess == IntPtr.Zero || !OpenProcessToken(shellProcess, 0x000A, out token)) // QUERY | DUPLICATE
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                if (IsElevated(token))
                    throw new InvalidOperationException("The desktop shell is elevated.");
                if (!DuplicateTokenEx(token, 0x000B, IntPtr.Zero, 2, 1, out primary)) // Primary token
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                if (!CreateEnvironmentBlock(out environment, primary, false))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                StartupInfo startup = new StartupInfo();
                startup.Size = Marshal.SizeOf(typeof(StartupInfo));
                startup.Desktop = @"winsta0\default";
                ProcessInformation process;
                StringBuilder command = new StringBuilder("\"" + start.FileName + "\" " + start.Arguments);
                if (!CreateProcessWithTokenW(primary, 0, start.FileName, command, 0x400,
                    environment, start.WorkingDirectory, ref startup, out process))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                try
                {
                    log.Write("Restarted Manager using the desktop shell's non-elevated token.");
                    return Process.GetProcessById((int)process.ProcessId);
                }
                finally
                {
                    CloseHandle(process.Thread);
                    CloseHandle(process.Process);
                }
            }
            finally
            {
                if (environment != IntPtr.Zero) DestroyEnvironmentBlock(environment);
                if (primary != IntPtr.Zero) CloseHandle(primary);
                if (token != IntPtr.Zero) CloseHandle(token);
                if (shellProcess != IntPtr.Zero) CloseHandle(shellProcess);
            }
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct StartupInfo
        {
            public int Size;
            public string Reserved, Desktop, Title;
            public int X, Y, XSize, YSize, XCountChars, YCountChars, FillAttribute, Flags;
            public short ShowWindow, ReservedSize;
            public IntPtr ReservedBytes, StdInput, StdOutput, StdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ProcessInformation
        {
            public IntPtr Process, Thread;
            public uint ProcessId, ThreadId;
        }

        [DllImport("user32.dll")] private static extern IntPtr GetShellWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);
        [DllImport("kernel32.dll", SetLastError = true)] private static extern IntPtr OpenProcess(uint access, bool inherit, uint pid);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool OpenProcessToken(IntPtr process, uint access, out IntPtr token);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool GetTokenInformation(IntPtr token, int informationClass, out int information, int size, out int length);
        [DllImport("advapi32.dll", SetLastError = true)] private static extern bool DuplicateTokenEx(IntPtr token, uint access, IntPtr attributes, int level, int type, out IntPtr primary);
        [DllImport("userenv.dll", SetLastError = true)] private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);
        [DllImport("userenv.dll")] private static extern bool DestroyEnvironmentBlock(IntPtr environment);
        [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CreateProcessWithTokenW(IntPtr token, uint logonFlags, string application,
            StringBuilder commandLine, uint flags, IntPtr environment, string directory,
            ref StartupInfo startup, out ProcessInformation process);
    }

    internal sealed class UpdateArguments
    {
        private static readonly string[] RequiredNames =
        {
            "--parent-pid", "--current", "--staged", "--backup", "--restart"
        };

        private UpdateArguments()
        {
        }

        public int ParentProcessId { get; private set; }
        public string CurrentDirectory { get; private set; }
        public string StagedDirectory { get; private set; }
        public string BackupDirectory { get; private set; }
        public string RestartExecutable { get; private set; }

        public static UpdateArguments Parse(string[] args)
        {
            if (args == null || args.Length == 0)
                throw new ArgumentException("Missing arguments.\n\n" + UsageText());
            if (args.Length % 2 != 0)
                throw new ArgumentException("Every option must have a value.\n\n" + UsageText());

            Dictionary<string, string> values =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < args.Length; i += 2)
            {
                string name = args[i];
                if (!IsRequiredName(name))
                    throw new ArgumentException("Unknown option: " + name + "\n\n" + UsageText());
                if (values.ContainsKey(name))
                    throw new ArgumentException("Duplicate option: " + name);
                if (string.IsNullOrEmpty(args[i + 1]))
                    throw new ArgumentException("Option has no value: " + name);
                values.Add(name, args[i + 1]);
            }

            for (int i = 0; i < RequiredNames.Length; i++)
            {
                if (!values.ContainsKey(RequiredNames[i]))
                    throw new ArgumentException("Missing option: " + RequiredNames[i] + "\n\n" + UsageText());
            }

            int parentProcessId;
            if (!int.TryParse(values["--parent-pid"], NumberStyles.None, CultureInfo.InvariantCulture, out parentProcessId) ||
                parentProcessId <= 0)
            {
                throw new ArgumentException("--parent-pid must be a positive process ID.");
            }

            UpdateArguments result = new UpdateArguments();
            result.ParentProcessId = parentProcessId;
            result.CurrentDirectory = CanonicalizeDirectory(values["--current"], "--current");
            result.StagedDirectory = CanonicalizeDirectory(values["--staged"], "--staged");
            result.BackupDirectory = CanonicalizeDirectory(values["--backup"], "--backup");
            result.RestartExecutable = CanonicalizeFile(values["--restart"], "--restart");
            return result;
        }

        public void Validate(UpdateLog log)
        {
            if (!Directory.Exists(CurrentDirectory))
                throw new DirectoryNotFoundException("Current SMM directory does not exist: " + CurrentDirectory);
            if (!Directory.Exists(StagedDirectory))
                throw new DirectoryNotFoundException("Staged SMM directory does not exist: " + StagedDirectory);
            if (Directory.Exists(BackupDirectory) || File.Exists(BackupDirectory))
                throw new IOException("Backup path already exists: " + BackupDirectory);
            if (!File.Exists(Path.Combine(StagedDirectory, "Manager.exe")))
                throw new FileNotFoundException("The staged SMM directory does not contain Manager.exe.");

            EnsureSeparateTrees(CurrentDirectory, StagedDirectory, "--current and --staged");
            EnsureSeparateTrees(CurrentDirectory, BackupDirectory, "--current and --backup");
            EnsureSeparateTrees(StagedDirectory, BackupDirectory, "--staged and --backup");

            string expectedRestart = Path.Combine(CurrentDirectory, "Manager.exe");
            if (!PathsEqual(RestartExecutable, expectedRestart))
                throw new ArgumentException("--restart must identify Manager.exe directly inside --current.");

            string currentVolume = VolumeIdentity.Get(CurrentDirectory);
            string backupVolume = VolumeIdentity.GetNearestExistingParent(BackupDirectory);
            if (!string.Equals(currentVolume, backupVolume, StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Current and backup directories must be on the same volume.");
            }

            log.Write("Validated current directory: " + CurrentDirectory);
            log.Write("Validated staged directory: " + StagedDirectory);
            log.Write("Validated backup directory: " + BackupDirectory);
            log.Write("Validated installation volume: " + currentVolume);
        }

        private static bool IsRequiredName(string value)
        {
            for (int i = 0; i < RequiredNames.Length; i++)
            {
                if (string.Equals(value, RequiredNames[i], StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        private static string CanonicalizeDirectory(string value, string option)
        {
            string path = Canonicalize(value, option);
            string root = Path.GetPathRoot(path);
            if (PathsEqual(path, root))
                throw new ArgumentException(option + " cannot be a volume root.");
            return path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string CanonicalizeFile(string value, string option)
        {
            return Canonicalize(value, option);
        }

        private static string Canonicalize(string value, string option)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException(option + " cannot be empty.");
            if (!Path.IsPathRooted(value))
                throw new ArgumentException(option + " must be an absolute path.");

            string fullPath = Path.GetFullPath(value);
            if (!Path.IsPathRooted(fullPath))
                throw new ArgumentException(option + " could not be canonicalized.");
            return fullPath;
        }

        private static void EnsureSeparateTrees(string first, string second, string description)
        {
            if (PathsEqual(first, second) || IsDescendant(first, second) || IsDescendant(second, first))
                throw new ArgumentException(description + " must be separate, non-overlapping paths.");
        }

        private static bool IsDescendant(string parent, string candidate)
        {
            string prefix = parent.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                            Path.DirectorySeparatorChar;
            return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool PathsEqual(string first, string second)
        {
            return string.Equals(
                first.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                second.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase);
        }

        private static string UsageText()
        {
            return "Use --help to view the required command line.";
        }
    }

    internal static class VolumeIdentity
    {
        public static string GetNearestExistingParent(string path)
        {
            string current = path;
            while (!Directory.Exists(current))
            {
                current = Path.GetDirectoryName(current);
                if (string.IsNullOrEmpty(current))
                    throw new DirectoryNotFoundException("No existing parent was found for " + path + ".");
            }
            return Get(current);
        }

        public static string Get(string path)
        {
            StringBuilder volumePath = new StringBuilder(260);
            if (!GetVolumePathName(path, volumePath, volumePath.Capacity))
                throw new IOException("Could not resolve the volume for " + path + ".", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));

            StringBuilder volumeName = new StringBuilder(260);
            if (!GetVolumeNameForVolumeMountPoint(volumePath.ToString(), volumeName, volumeName.Capacity))
                throw new IOException("Could not identify the volume for " + path + ".", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            return volumeName.ToString();
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumePathName(
            string fileName,
            StringBuilder volumePathName,
            int bufferLength);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool GetVolumeNameForVolumeMountPoint(
            string volumeMountPoint,
            StringBuilder volumeName,
            int bufferLength);
    }

    internal sealed class UpdateLog : IDisposable
    {
        private readonly StreamWriter writer;

        public UpdateLog(string path)
        {
            writer = new StreamWriter(path, true, Encoding.UTF8);
            writer.AutoFlush = true;
        }

        public void Write(string message)
        {
            writer.WriteLine(
                "[{0:O}] {1}",
                DateTime.UtcNow,
                message);
        }

        public void Dispose()
        {
            writer.Dispose();
        }
    }
}
