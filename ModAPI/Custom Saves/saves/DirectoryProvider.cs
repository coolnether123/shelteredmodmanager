using System;
using System.IO;
using UnityEngine;

namespace ModAPI.Saves
{
    internal static class DirectoryProvider
    {
        public static string ModsRoot
        {
            get
            {
                try
                {
                    // Prefer <GameRoot>/mods (lowercase) to align with manager
                    var gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                    var a = Path.Combine(gameRoot, "mods");
                    var b = Path.Combine(gameRoot, "Mods");
                    if (Directory.Exists(a)) return a;
                    if (Directory.Exists(b)) return b;
                    Directory.CreateDirectory(a);
                    return a;
                }
                catch
                {
                    return Path.Combine(Directory.GetCurrentDirectory(), "mods");
                }
            }
        }

        public static string SmmRoot
        {
            get
            {
                string gameRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                return Path.Combine(gameRoot, "SMM");
            }
        }

        public static string ConfigPath => Path.Combine(Path.Combine(SmmRoot, "bin"), "mod_manager.ini");

        public static string ModApiRoot
        {
            get
            {
                var root = Path.Combine(ModsRoot, "ModAPI");
                EnsureDir(root);
                return root;
            }
        }

        public static string UserRoot
        {
            get
            {
                var root = Path.Combine(ModApiRoot, "User");
                EnsureDir(root);
                return root;
            }
        }

        public static string SavesRoot
        {
            get
            {
                var root = Path.Combine(ModApiRoot, "Saves");
                EnsureDir(root);
                return root;
            }
        }

        public static string ScenarioRoot(string scenarioId, bool create = true)
        {
            scenarioId = NameSanitizer.SanitizeId(scenarioId);
            var path = Path.Combine(SavesRoot, scenarioId);
            if (create) EnsureDir(path);
            return path;
        }

        public static string SlotRoot(string scenarioId, int absoluteSlot, bool create = true)
        {
            var path = Path.Combine(ScenarioRoot(scenarioId, create), $"Slot_{absoluteSlot}");
            if (create) EnsureDir(path);
            return path;
        }

        // REMOVED: Global ManifestPath() - each slot now has its own manifest.json
        // The slot-level manifest.json stores only mod tracking data.
        // Save metadata is read from the XML file on demand.

        public static string EntryPath(string scenarioId, int absoluteSlot)
        {
            return Path.Combine(SlotRoot(scenarioId, absoluteSlot), "SaveData.xml");
        }

        public static string EntryPath(string scenarioId, string saveId)
        {
            // Legacy fall-back
            return Path.Combine(ScenarioRoot(scenarioId), NameSanitizer.SanitizeId(saveId) + ".xml");
        }

        public static string PreviewsRoot(string scenarioId)
        {
            var path = Path.Combine(ScenarioRoot(scenarioId), "previews");
            EnsureDir(path);
            return path;
        }

        public static string PreviewPath(string scenarioId, string saveId)
        {
            return Path.Combine(PreviewsRoot(scenarioId), NameSanitizer.SanitizeId(saveId) + ".png");
        }

        public static string CorruptRoot(string scenarioId)
        {
            var path = Path.Combine(ScenarioRoot(scenarioId), "_corrupt");
            EnsureDir(path);
            return path;
        }

        public static string LibsRoot
        {
            get
            {
                var root = Path.Combine(Directory.GetCurrentDirectory(), "libs");
                EnsureDir(root);
                return root;
            }
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }
}
