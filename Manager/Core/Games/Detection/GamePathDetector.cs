using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Manager.Core.Games.Models;

namespace Manager.Core.Games.Detection
{
    public sealed class GamePathDetector
    {
        public string TryDetect(GameProfile profile)
        {
            return TryDetect(profile, null);
        }

        public string TryDetect(GameProfile profile, Action<string> log)
        {
            if (profile == null)
                return string.Empty;

            string running = TryDetectRunningProcess(profile, log);
            if (!string.IsNullOrEmpty(running))
                return running;

            List<string> searchDirs = BuildSearchDirectories(profile, log);
            string found = FindExecutableInDirectories(profile, searchDirs);
            return found ?? string.Empty;
        }

        public string ResolveExecutableFromDirectory(GameProfile profile, string directory)
        {
            if (profile == null || string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return string.Empty;

            string[] executableNames = profile.ExecutableNames ?? new string[0];
            for (int i = 0; i < executableNames.Length; i++)
            {
                string candidate = Path.Combine(directory, executableNames[i]);
                if (File.Exists(candidate))
                    return candidate;
            }

            string[] allExeFiles = Directory.GetFiles(directory, "*.exe", SearchOption.TopDirectoryOnly);
            return allExeFiles.Length == 1 ? allExeFiles[0] : string.Empty;
        }

        private string TryDetectRunningProcess(GameProfile profile, Action<string> log)
        {
            string[] executableNames = profile.ExecutableNames ?? new string[0];
            for (int i = 0; i < executableNames.Length; i++)
            {
                try
                {
                    string processName = Path.GetFileNameWithoutExtension(executableNames[i]);
                    Process[] processes = Process.GetProcessesByName(processName);
                    if (processes.Length == 0)
                        continue;

                    string path = processes[0].MainModule.FileName;
                    if (File.Exists(path))
                        return path;
                }
                catch (Exception ex)
                {
                    LogDetection(log, "Could not inspect running process for " + executableNames[i] + ": " + ex.Message);
                }
            }

            return string.Empty;
        }

        private List<string> BuildSearchDirectories(GameProfile profile, Action<string> log)
        {
            List<string> searchDirs = new List<string>();

            try
            {
                string exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                AddDirectory(searchDirs, exeDir);

                DirectoryInfo parent = Directory.GetParent(exeDir);
                if (parent != null)
                    AddDirectory(searchDirs, parent.FullName);

                DirectoryInfo grandparent = parent != null ? parent.Parent : null;
                if (grandparent != null)
                    AddDirectory(searchDirs, grandparent.FullName);
            }
            catch (Exception ex)
            {
                LogDetection(log, "Could not add manager-relative search directories: " + ex.Message);
            }

            AddSteamAndGogHints(searchDirs, profile, log);

            string[] commonDirs = profile.CommonInstallDirectories ?? new string[0];
            for (int i = 0; i < commonDirs.Length; i++)
                AddDirectory(searchDirs, commonDirs[i]);

            return searchDirs;
        }

        private static void AddSteamAndGogHints(List<string> searchDirs, GameProfile profile, Action<string> log)
        {
            string steamPath = null;

            try
            {
                if (!string.IsNullOrEmpty(profile.SteamAppId))
                {
                    using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(
                        @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\Steam App " + profile.SteamAppId))
                    {
                        string installPath = key != null ? key.GetValue("InstallLocation") as string : null;
                        AddDirectory(searchDirs, installPath);
                    }
                }
            }
            catch (Exception ex)
            {
                LogDetection(log, "Could not read Steam uninstall registry hint: " + ex.Message);
            }

            try
            {
                if (!string.IsNullOrEmpty(profile.SteamCommonFolderName))
                {
                    using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        steamPath = key != null ? key.GetValue("SteamPath") as string : null;
                        if (!string.IsNullOrEmpty(steamPath))
                            AddDirectory(searchDirs, Path.Combine(steamPath, Path.Combine("steamapps", Path.Combine("common", profile.SteamCommonFolderName))));
                    }
                }
            }
            catch (Exception ex)
            {
                LogDetection(log, "Could not read Steam library registry hint: " + ex.Message);
            }

            AddSteamLibraryFolders(searchDirs, profile, steamPath, log);
        }

        private static void AddSteamLibraryFolders(List<string> searchDirs, GameProfile profile, string steamPath, Action<string> log)
        {
            if (profile == null || string.IsNullOrEmpty(steamPath))
                return;

            string libraryFoldersPath = Path.Combine(Path.Combine(steamPath, "steamapps"), "libraryfolders.vdf");
            if (!File.Exists(libraryFoldersPath))
                return;

            try
            {
                string[] lines = File.ReadAllLines(libraryFoldersPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string libraryPath = TryReadVdfValue(lines[i], "path");
                    if (string.IsNullOrEmpty(libraryPath))
                        continue;

                    AddSteamLibraryInstallDirectory(searchDirs, profile, libraryPath);
                }
            }
            catch (Exception ex)
            {
                LogDetection(log, "Could not read Steam library folders: " + ex.Message);
            }
        }

        private static void AddSteamLibraryInstallDirectory(List<string> searchDirs, GameProfile profile, string libraryPath)
        {
            string steamAppsPath = Path.Combine(libraryPath, "steamapps");
            string commonPath = Path.Combine(steamAppsPath, "common");

            string installDir = ReadSteamInstallDirFromManifest(Path.Combine(steamAppsPath, "appmanifest_" + profile.SteamAppId + ".acf"));
            if (!string.IsNullOrEmpty(installDir))
            {
                AddDirectory(searchDirs, Path.Combine(commonPath, installDir));
                return;
            }

            if (!string.IsNullOrEmpty(profile.SteamCommonFolderName))
                AddDirectory(searchDirs, Path.Combine(commonPath, profile.SteamCommonFolderName));
        }

        private static string ReadSteamInstallDirFromManifest(string manifestPath)
        {
            if (string.IsNullOrEmpty(manifestPath) || !File.Exists(manifestPath))
                return string.Empty;

            try
            {
                string[] lines = File.ReadAllLines(manifestPath);
                for (int i = 0; i < lines.Length; i++)
                {
                    string installDir = TryReadVdfValue(lines[i], "installdir");
                    if (!string.IsNullOrEmpty(installDir))
                        return installDir;
                }
            }
            catch
            {
            }

            return string.Empty;
        }

        private static string TryReadVdfValue(string line, string key)
        {
            if (string.IsNullOrEmpty(line) || string.IsNullOrEmpty(key))
                return string.Empty;

            string normalized = line.Trim();
            string prefix = "\"" + key + "\"";
            if (!normalized.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            string value = normalized.Substring(prefix.Length).Trim();
            if (value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"')
                value = value.Substring(1, value.Length - 2);

            return value.Replace(@"\\", @"\");
        }

        private static string FindExecutableInDirectories(GameProfile profile, IList<string> searchDirs)
        {
            for (int i = 0; i < searchDirs.Count; i++)
            {
                string dir = searchDirs[i];
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                    continue;

                string[] executableNames = profile.ExecutableNames ?? new string[0];
                for (int j = 0; j < executableNames.Length; j++)
                {
                    string path = Path.Combine(dir, executableNames[j]);
                    if (File.Exists(path))
                        return path;
                }
            }

            return string.Empty;
        }

        private static void AddDirectory(IList<string> directories, string directory)
        {
            if (string.IsNullOrEmpty(directory))
                return;

            for (int i = 0; i < directories.Count; i++)
            {
                if (string.Equals(directories[i], directory, StringComparison.OrdinalIgnoreCase))
                    return;
            }

            directories.Add(directory);
        }

        private static void LogDetection(Action<string> log, string message)
        {
            if (log != null && !string.IsNullOrEmpty(message))
                log(message);
        }
    }
}
