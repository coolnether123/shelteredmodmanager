using System;
using System.Collections.Generic;
using System.Linq;
using ModAPI.Core;

namespace ModAPI.Loading
{
    internal static class ModLoadPlanBuilder
    {
        internal static List<ModEntry> DiscoverAndOrder(List<string> orderedModIds)
        {
            List<ModEntry> discovered = ModDiscovery.DiscoverAllMods();
            MMLog.WriteDebug("DiscoverAndOrderMods: " + discovered.Count + " mods found on disk.");
            foreach (ModEntry mod in discovered)
                MMLog.WriteDebug("  - On Disk: '" + mod.Id + "' at '" + mod.RootPath + "'");

            if (orderedModIds == null)
            {
                MMLog.WriteDebug("No load order provided (loadorder.json missing). Enabling ALL discovered mods.");
                return discovered;
            }

            if (orderedModIds.Count == 0)
            {
                MMLog.Write("Explicit empty load order found. Enabling NO mods (core runtime remains active).");
                return new List<ModEntry>();
            }

            MMLog.WriteDebug("Applying load order (contains " + orderedModIds.Count + " IDs).");
            return ApplyLoadOrder(discovered, orderedModIds);
        }

        private static List<ModEntry> ApplyLoadOrder(List<ModEntry> discovered, List<string> orderedModIds)
        {
            var ordered = new List<ModEntry>();
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string id in orderedModIds)
            {
                MMLog.WriteDebug("  Looking for ordered ID: '" + id + "'");
                ModEntry mod = discovered.FirstOrDefault(m => string.Equals(m.Id, id, StringComparison.OrdinalIgnoreCase));
                if (mod != null)
                {
                    if (seenIds.Add(mod.Id))
                    {
                        ordered.Add(mod);
                        MMLog.WriteDebug("    FOUND and enabled: " + mod.Id);
                    }
                }
                else
                {
                    MMLog.WriteDebug("    NOT FOUND on disk: " + id);
                }
            }

            MMLog.WriteDebug("Final LoadedMods count: " + ordered.Count);
            return ordered;
        }
    }
}
