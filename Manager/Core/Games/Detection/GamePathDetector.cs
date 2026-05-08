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
            if (profile == null)
                return string.Empty;

            string running = TryDetectRunningProcess(profile);
            if (!string.IsNullOrEmpty(running))
                return running;

            List<string> searchDirs = BuildSearchDirectories(profile);
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

        private string TryDetectRunningProcess(GameProfile profile)
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
                catch { }
            }

            return string.Empty;
        }

        private List<string> BuildSearchDirectories(GameProfile profile)
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
            catch { }

            AddSteamAndGogHints(searchDirs, profile);

            string[] commonDirs = profile.CommonInstallDirectories ?? new string[0];
            for (int i = 0; i < commonDirs.Length; i++)
                AddDirectory(searchDirs, commonDirs[i]);

            return searchDirs;
        }

        private static void AddSteamAndGogHints(List<string> searchDirs, GameProfile profile)
        {
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
            catch { }

            try
            {
                if (!string.IsNullOrEmpty(profile.SteamCommonFolderName))
                {
                    using (Microsoft.Win32.RegistryKey key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
                    {
                        string steamPath = key != null ? key.GetValue("SteamPath") as string : null;
                        if (!string.IsNullOrEmpty(steamPath))
                            AddDirectory(searchDirs, Path.Combine(steamPath, Path.Combine("steamapps", Path.Combine("common", profile.SteamCommonFolderName))));
                    }
                }
            }
            catch { }
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
    }
}
