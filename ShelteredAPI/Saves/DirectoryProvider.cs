using System;
using System.IO;
using ModAPI.Core;

namespace ShelteredAPI.Saves
{
    internal static class DirectoryProvider
    {
        public static string ModsRoot
        {
            get { return ModApiPaths.ModsRoot; }
        }

        public static string SmmRoot
        {
            get { return ModApiPaths.SmmRoot; }
        }

        public static string ConfigPath => ModApiPaths.ConfigPath;

        public static string ModApiRoot
        {
            get { return ModApiPaths.ModApiRoot; }
        }

        public static string UserRoot
        {
            get { return ModApiPaths.UserRoot; }
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

        public static string BackupsRoot
        {
            get
            {
                var root = Path.Combine(ModApiRoot, "Backups");
                EnsureDir(root);
                return root;
            }
        }

        public static string SaveBackupsRoot
        {
            get
            {
                var root = Path.Combine(BackupsRoot, "Saves");
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

        public static string DeletedRoot(string scenarioId)
        {
            var path = Path.Combine(ScenarioRoot(scenarioId), "_trash");
            EnsureDir(path);
            return path;
        }

        public static string LibsRoot
        {
            get { return ModApiPaths.LibsRoot; }
        }

        private static void EnsureDir(string path)
        {
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
        }
    }
}
