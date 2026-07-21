using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Microsoft.Win32;

namespace Manager.Core.Services
{
    /// <summary>
    /// Temporarily captures nxm:// links, restores the user's prior handler, and
    /// securely relays links to the already-running Manager process.
    /// </summary>
    internal static class NexusProtocolHandlerService
    {
        private const string ClassesKeyPath = @"Software\Classes\nxm";
        private const string BackupKeyPath = @"Software\ShelteredModManager\NxmHandlerBackup";
        private const string CommandSubKey = @"shell\open\command";
        private const string DefaultIconSubKey = "DefaultIcon";
        private static readonly byte[] InboxEntropy = Encoding.UTF8.GetBytes("ShelteredModManager.NxmInbox.v1");
        private static readonly object TimerLock = new object();
        private static Timer _restoreTimer;

        public static bool BeginTemporaryCapture(string executablePath, out string errorMessage)
        {
            errorMessage = null;
            if (string.IsNullOrEmpty(executablePath) || !File.Exists(executablePath))
            {
                errorMessage = "Manager.exe could not be located for Nexus link handling.";
                return false;
            }

            string expectedCommand = "\"" + executablePath + "\" --nxm \"%1\"";
            try
            {
                string currentCommand = ReadString(Registry.CurrentUser, ClassesKeyPath + "\\" + CommandSubKey, null);
                string savedExpected = ReadString(Registry.CurrentUser, BackupKeyPath, "ExpectedCommand");
                if (!string.Equals(currentCommand, expectedCommand, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(savedExpected, expectedCommand, StringComparison.OrdinalIgnoreCase))
                {
                    using (RegistryKey existing = Registry.CurrentUser.OpenSubKey(ClassesKeyPath))
                    using (RegistryKey backup = Registry.CurrentUser.CreateSubKey(BackupKeyPath))
                    {
                        bool hadUserHandler = existing != null;
                        backup.SetValue("HadUserHandler", hadUserHandler ? 1 : 0, RegistryValueKind.DWord);
                        backup.SetValue("PreviousDefault", existing != null ? Convert.ToString(existing.GetValue(null, string.Empty)) : string.Empty);
                        backup.SetValue("PreviousUrlProtocol", existing != null ? Convert.ToString(existing.GetValue("URL Protocol", string.Empty)) : string.Empty);
                        backup.SetValue("PreviousCommand", ReadString(Registry.CurrentUser, ClassesKeyPath + "\\" + CommandSubKey, null));
                        backup.SetValue("PreviousIcon", ReadString(Registry.CurrentUser, ClassesKeyPath + "\\" + DefaultIconSubKey, null));
                        backup.SetValue("ExpectedCommand", expectedCommand);
                    }
                }

                using (RegistryKey protocol = Registry.CurrentUser.CreateSubKey(ClassesKeyPath))
                {
                    protocol.SetValue(null, "URL:Nexus Mods Protocol");
                    protocol.SetValue("URL Protocol", string.Empty);
                }
                using (RegistryKey icon = Registry.CurrentUser.CreateSubKey(ClassesKeyPath + "\\" + DefaultIconSubKey))
                    icon.SetValue(null, executablePath);
                using (RegistryKey command = Registry.CurrentUser.CreateSubKey(ClassesKeyPath + "\\" + CommandSubKey))
                    command.SetValue(null, expectedCommand);

                ScheduleRestore(TimeSpan.FromMinutes(15));
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Windows could not temporarily register Manager for Nexus links: " + ex.Message;
                return false;
            }
        }

        public static void RestorePreviousHandler()
        {
            lock (TimerLock)
            {
                if (_restoreTimer != null)
                {
                    _restoreTimer.Dispose();
                    _restoreTimer = null;
                }
            }

            try
            {
                string expected;
                bool hadUserHandler;
                string previousDefault;
                string previousUrlProtocol;
                string previousIcon;
                string previousCommand;
                using (RegistryKey backup = Registry.CurrentUser.OpenSubKey(BackupKeyPath))
                {
                    if (backup == null)
                        return;

                    expected = Convert.ToString(backup.GetValue("ExpectedCommand", string.Empty));
                    hadUserHandler = Convert.ToInt32(backup.GetValue("HadUserHandler", 0)) != 0;
                    previousDefault = Convert.ToString(backup.GetValue("PreviousDefault", string.Empty));
                    previousUrlProtocol = Convert.ToString(backup.GetValue("PreviousUrlProtocol", string.Empty));
                    previousIcon = Convert.ToString(backup.GetValue("PreviousIcon", string.Empty));
                    previousCommand = Convert.ToString(backup.GetValue("PreviousCommand", string.Empty));
                }

                string current = ReadString(Registry.CurrentUser, ClassesKeyPath + "\\" + CommandSubKey, null);
                if (!string.Equals(current, expected, StringComparison.OrdinalIgnoreCase))
                {
                    DeleteBackupKey();
                    return;
                }

                if (!hadUserHandler)
                {
                    Registry.CurrentUser.DeleteSubKeyTree(ClassesKeyPath);
                }
                else
                {
                    using (RegistryKey protocol = Registry.CurrentUser.CreateSubKey(ClassesKeyPath))
                    {
                        protocol.SetValue(null, previousDefault);
                        protocol.SetValue("URL Protocol", previousUrlProtocol);
                    }
                    using (RegistryKey icon = Registry.CurrentUser.CreateSubKey(ClassesKeyPath + "\\" + DefaultIconSubKey))
                        icon.SetValue(null, previousIcon);
                    using (RegistryKey command = Registry.CurrentUser.CreateSubKey(ClassesKeyPath + "\\" + CommandSubKey))
                        command.SetValue(null, previousCommand);
                }
            }
            catch
            {
                return;
            }

            DeleteBackupKey();
        }

        public static bool EnqueueForRunningManager(string rawUrl, out string errorMessage)
        {
            errorMessage = null;
            try
            {
                string directory = GetInboxDirectory();
                if (!Directory.Exists(directory))
                    Directory.CreateDirectory(directory);

                byte[] clear = Encoding.UTF8.GetBytes(rawUrl ?? string.Empty);
                byte[] protectedBytes = ProtectedData.Protect(clear, InboxEntropy, DataProtectionScope.CurrentUser);
                string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".nxm");
                File.WriteAllText(path, Convert.ToBase64String(protectedBytes));
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "The Nexus link could not be sent to the running Manager: " + ex.Message;
                return false;
            }
        }

        public static IList<string> DequeuePendingLinks()
        {
            var links = new List<string>();
            string directory = GetInboxDirectory();
            if (!Directory.Exists(directory))
                return links;

            string[] files;
            try { files = Directory.GetFiles(directory, "*.nxm", SearchOption.TopDirectoryOnly); }
            catch { return links; }

            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    byte[] protectedBytes = Convert.FromBase64String(File.ReadAllText(files[i]));
                    byte[] clear = ProtectedData.Unprotect(protectedBytes, InboxEntropy, DataProtectionScope.CurrentUser);
                    string link = Encoding.UTF8.GetString(clear);
                    if (!string.IsNullOrEmpty(link))
                        links.Add(link);
                }
                catch
                {
                }
                finally
                {
                    try { File.Delete(files[i]); }
                    catch { }
                }
            }
            return links;
        }

        private static void ScheduleRestore(TimeSpan delay)
        {
            lock (TimerLock)
            {
                if (_restoreTimer != null)
                    _restoreTimer.Dispose();
                _restoreTimer = new Timer(delegate { RestorePreviousHandler(); }, null, (int)delay.TotalMilliseconds, Timeout.Infinite);
            }
        }

        private static string GetInboxDirectory()
        {
            string executableDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(Path.Combine(executableDirectory, "bin"), "_nxm_inbox");
        }

        private static string ReadString(RegistryKey root, string path, string valueName)
        {
            using (RegistryKey key = root.OpenSubKey(path))
                return key != null ? Convert.ToString(key.GetValue(valueName, string.Empty)) : string.Empty;
        }

        private static void DeleteBackupKey()
        {
            try { Registry.CurrentUser.DeleteSubKeyTree(BackupKeyPath); }
            catch { }
        }
    }
}
