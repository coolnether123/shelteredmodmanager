using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    /**
     * Author: coolnether123
     * Discovers mods in mods/enabled, reads About/About.json, and loads assemblies.
     * Kept modular to keep PluginManager lean and focused.
     * Coolnether123
     */
    public static class ModDiscovery
    {
        // Finds enabled mods with About/About.json and returns descriptors
        // Coolnether123
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
                    MMLog.Write("[Discovery] No 'mods' directory found. Skipping discovery.");
                    return results;
                }

                var dirs = Directory.GetDirectories(modsRoot);
                foreach (var dir in dirs)
                {
                    var name = Path.GetFileName(dir);

                    if (string.Equals(name, "disabled", StringComparison.OrdinalIgnoreCase))
                    {
                        MMLog.Write($"[Discovery]   Skipped (disabled folder)");
                        continue;
                    }

                    var entry = ModAboutReader.TryRead(dir);
                    if (entry != null) results.Add(entry);
                }
            }
            catch (Exception ex)
            {
                MMLog.Write($"[Discovery] Discovery error: {ex.Message}");
            }

            MMLog.Write($"[Discovery] Total mods discovered: {results.Count}");
            return results;
        }

        public static List<Assembly> LoadAssemblies(ModEntry entry)
        {
            var assemblies = new List<Assembly>();
            if (entry == null)
            {
                return assemblies;
            }

            if (!Directory.Exists(entry.AssembliesPath))
            {
                return assemblies;
            }

            var dllFiles = Directory.GetFiles(entry.AssembliesPath, "*.dll", SearchOption.AllDirectories);

            foreach (var dllPath in dllFiles)
            {
                try
                {
                    var asm = Assembly.LoadFrom(dllPath);
                    assemblies.Add(asm);
                    ModRegistry.RegisterAssemblyForMod(asm, entry);
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"[Trace] LoadAssemblies: FAILED to load assembly '{dllPath}' for mod '{entry.Id}': {ex.ToString()}");
                }
            }
            return assemblies;
        }

    }
}
