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
    public class ModEntry
    {
        public string Id;            // normalized id                              
        public string Name;          // display name                                
        public string Version;       // optional version string                  
        public string RootPath;      // mod root folder                            
        public string AboutPath;     // path to About/About.json                    
        public string AssembliesPath;// path to Assemblies folder                   
        public ModAbout About; // parsed about                             
    }

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

                if (!Directory.Exists(modsRoot))
                {
                    MMLog.Write("No 'mods' directory found. Skipping discovery.");
                    return results;
                }

                foreach (var dir in Directory.GetDirectories(modsRoot))
                {
                    var name = Path.GetFileName(dir);
                    if (string.Equals(name, "disabled", StringComparison.OrdinalIgnoreCase)) continue;

                    var about = Path.Combine(dir, "About");
                    var aboutJson = Path.Combine(about, "About.json");
                    if (!File.Exists(aboutJson))
                    {
                        // Not a about-driven mod; will be handled by legacy loader
                        continue;
                    }

                    try
                    {
                        var text = File.ReadAllText(aboutJson);
                        var modAbout = JsonUtility.FromJson<ModAbout>(text);
                        if (modAbout == null)
                        {
                            MMLog.Write("Failed to parse About.json in: " + dir);
                            continue;
                        }

                        // Validate required fields (basic)
                        if (string.IsNullOrEmpty(modAbout.id) || string.IsNullOrEmpty(modAbout.name) ||
                            string.IsNullOrEmpty(modAbout.version) || string.IsNullOrEmpty(modAbout.description) ||
                            modAbout.authors == null || modAbout.authors.Length == 0)
                        {
                            MMLog.Write("About.json missing required fields in: " + dir);
                            continue;
                        }

                        var entry = new ModEntry
                        {
                            Id = NormId(modAbout.id),
                            Name = modAbout.name,
                            Version = modAbout.version,
                            RootPath = dir,
                            AboutPath = aboutJson,
                            AssembliesPath = Path.Combine(dir, "Assemblies"),
                            About = modAbout
                        };

                        results.Add(entry);
                    }
                    catch (Exception ex)
                    {
                        MMLog.Write("Error reading About.json in '" + dir + "': " + ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("Discovery error: " + ex.Message);
            }
            return results;
        }

        public static List<Assembly> LoadAssemblies(ModEntry entry)
        {
            var assemblies = new List<Assembly>();
            if (entry == null)
            {
                MMLog.WriteDebug("[Trace] LoadAssemblies: entry was null.");
                return assemblies;
            }

            MMLog.WriteDebug($"[Trace] LoadAssemblies: Processing entry '{entry.Id}' with AssembliesPath: '{entry.AssembliesPath}'");

            if (!Directory.Exists(entry.AssembliesPath))
            {
                MMLog.WriteDebug($"[Trace] LoadAssemblies: Directory does not exist: '{entry.AssembliesPath}'");
                return assemblies;
            }

            var dllFiles = Directory.GetFiles(entry.AssembliesPath, "*.dll", SearchOption.AllDirectories);
            MMLog.WriteDebug($"[Trace] LoadAssemblies: Found {dllFiles.Length} DLL(s): {string.Join(", ", dllFiles)}");

            foreach (var dllPath in dllFiles)
            {
                MMLog.WriteDebug($"[Trace] LoadAssemblies: Attempting to load '{dllPath}'...");
                try
                {
                    var asm = Assembly.LoadFrom(dllPath);
                    assemblies.Add(asm);
                    ModRegistry.RegisterAssemblyForMod(asm, entry);
                    MMLog.WriteDebug($"[Trace] LoadAssemblies: SUCCESS loading assembly '{asm.FullName}' for mod '{entry.Id}'.");
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"[Trace] LoadAssemblies: FAILED to load assembly '{dllPath}' for mod '{entry.Id}': {ex.ToString()}");
                }
            }
            return assemblies;
        }



        private static string NormId(string s) => (s ?? "").Trim().ToLowerInvariant();
    }
}