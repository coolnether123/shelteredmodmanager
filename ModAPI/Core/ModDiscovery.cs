using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Scans the mods directory and loads mod assemblies from discovered entries.
    /// </summary>
    public static class ModDiscovery
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

        /// <summary>
        /// Loads all DLLs under a mod's Assemblies folder into the current AppDomain.
        /// </summary>
        /// <remarks>
        /// Uses byte-loading to avoid file locks during iterative development.
        /// </remarks>
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

            // Register mod by ID (once per mod, before loading DLLs)
            ModRegistry.RegisterModById(entry);
            
            foreach (var dllPath in dllFiles)
            {
                try
                {
                    // Load assembly bytes to avoid file locking (developer-friendly)
                    byte[] assemblyBytes = File.ReadAllBytes(dllPath);
                    var asm = Assembly.Load(assemblyBytes);
                    assemblies.Add(asm);
                    ModRegistry.RegisterAssemblyForMod(asm, entry);
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"LoadAssemblies: FAILED to load assembly '{dllPath}' for mod '{entry.Id}': {ex.ToString()}");
                }
            }
            return assemblies;
        }

    }
}
