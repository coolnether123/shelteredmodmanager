using System;
using System.IO;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioAuthoringStoragePaths
    {
        private const string ShellFolderName = "ScenarioAuthoring";

        public static string GetShellRootPath(bool create)
        {
            string gameRoot;
            try
            {
                gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }
            catch
            {
                gameRoot = Directory.GetCurrentDirectory();
            }

            string modsRoot = Path.Combine(gameRoot, "mods");
            if (!Directory.Exists(modsRoot))
            {
                string legacyModsRoot = Path.Combine(gameRoot, "Mods");
                modsRoot = Directory.Exists(legacyModsRoot) ? legacyModsRoot : modsRoot;
            }

            string path = Path.Combine(Path.Combine(Path.Combine(modsRoot, "ModAPI"), "User"), ShellFolderName);
            if (create && !Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }

        public static string GetLayoutFilePath()
        {
            return Path.Combine(GetShellRootPath(true), "layout.xml");
        }

        public static string GetSettingsFilePath()
        {
            return Path.Combine(GetShellRootPath(true), "settings.xml");
        }

        public static string GetAssetsRootPath()
        {
            string gameRoot;
            try
            {
                gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            }
            catch
            {
                gameRoot = Directory.GetCurrentDirectory();
            }

            string modsRoot = Path.Combine(gameRoot, "mods");
            if (!Directory.Exists(modsRoot))
            {
                string legacyModsRoot = Path.Combine(gameRoot, "Mods");
                modsRoot = Directory.Exists(legacyModsRoot) ? legacyModsRoot : modsRoot;
            }

            string path = Path.Combine(Path.Combine(modsRoot, "ModAPI"), "Assets");
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
            return path;
        }
    }
}
