using System;
using System.IO;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Encapsulates About.json discovery and parsing so ModDiscovery stays DRY.
    /// </summary>
    public static class ModAboutReader
    {
        public static ModEntry TryRead(string modDirectory)
        {
            if (string.IsNullOrEmpty(modDirectory) || !Directory.Exists(modDirectory))
                return null;

            try
            {
                var aboutDir = Path.Combine(modDirectory, "About");
                var aboutJson = Path.Combine(aboutDir, "About.json");
                if (!File.Exists(aboutJson))
                    return null;

                var text = File.ReadAllText(aboutJson);
                var modAbout = JsonUtility.FromJson<ModAbout>(text);
                if (modAbout == null)
                {
                    MMLog.Write($"[Discovery] Failed to parse About.json in '{modDirectory}'");
                    return null;
                }

                if (!HasRequiredFields(modAbout))
                {
                    MMLog.Write($"[Discovery] About.json missing required fields in '{modDirectory}'");
                    return null;
                }

                var entry = new ModEntry
                {
                    Id = NormId(modAbout.id),
                    Name = modAbout.name,
                    Version = modAbout.version,
                    RootPath = modDirectory,
                    AboutPath = aboutJson,
                    AssembliesPath = Path.Combine(modDirectory, "Assemblies"),
                    About = modAbout
                };

                MMLog.WriteDebug($"[Discovery] Discovered mod: {entry.Id} ({entry.Name})");
                return entry;
            }
            catch (Exception ex)
            {
                MMLog.Write($"[Discovery] Error reading About.json in '{modDirectory}': {ex.Message}");
                return null;
            }
        }

        private static bool HasRequiredFields(ModAbout about)
        {
            return about != null
                && !string.IsNullOrEmpty(about.id)
                && !string.IsNullOrEmpty(about.name)
                && !string.IsNullOrEmpty(about.version)
                && !string.IsNullOrEmpty(about.description)
                && about.authors != null
                && about.authors.Length > 0;
        }

        private static string NormId(string s) => (s ?? "").Trim().ToLowerInvariant();
    }
}
