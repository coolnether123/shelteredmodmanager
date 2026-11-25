using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Scans the mods directory for mod descriptors.
    /// </summary>
    internal static class ModDiscovery
    {
        /// <summary>
        /// Finds mods with About/About.json and returns descriptors.
        /// </summary>
        public static List<ModEntry> DiscoverAllMods()
        {
            var results = new List<ModEntry>();
            try
            {
                string gameRootPath = Directory.GetParent(Application.dataPath).FullName;
                string modsRoot = Path.Combine(gameRootPath, "mods");

                // Minimal logging: only emit per-mod discovery at info level.

                if (!Directory.Exists(modsRoot))
                {
                    MMLog.Write("No 'mods' directory found. Skipping discovery.");
                    return results;
                }

                var dirs = Directory.GetDirectories(modsRoot);
                foreach (var dir in dirs)
                {
                    var name = Path.GetFileName(dir);

                    if (string.Equals(name, "disabled", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(name, "ModAPI", StringComparison.OrdinalIgnoreCase))
                    {
                        MMLog.WriteDebug($"   Skipped (reserved folder: {name})");
                        continue;
                    }

                    var entry = ModAboutReader.TryRead(dir);
                    if (entry != null) results.Add(entry);
                }
            }
            catch (Exception ex)
            {
                MMLog.Write($"Discovery error: {ex.Message}");
            }

            MMLog.Write($"Total mods discovered: {results.Count}");
            return results;
        }

    }
}
