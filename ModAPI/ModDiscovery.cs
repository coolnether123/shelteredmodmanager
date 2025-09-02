using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;

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
    public static List<ModEntry> DiscoverEnabledMods()
    {
        var results = new List<ModEntry>();
        try
        {
            string gameRootPath = Directory.GetParent(Application.dataPath).FullName;
            string modsRoot = Path.Combine(gameRootPath, "mods");
            string enabledRoot = Path.Combine(modsRoot, "enabled");

            if (!Directory.Exists(enabledRoot))
            {
                MMLog.Write("No 'mods/enabled' directory found. Skipping discovery.");
                return results;
            }

            foreach (var dir in Directory.GetDirectories(enabledRoot))
            {
                var about = Path.Combine(dir, "About");
                var aboutJson = Path.Combine(about, "About.json"); // <- renamed about file  // Coolnether123
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
                        Id = NormalizeId(modAbout.id),
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

    // Loads assemblies for a given mod entry, using simple TFM preference
    // Coolnether123
    public static List<Assembly> LoadAssemblies(ModEntry entry)
    {
        var assemblies = new List<Assembly>();
        try
        {
            var asmPath = SelectAssembliesPath(entry.AssembliesPath);
            if (asmPath == null)
            {
                MMLog.Write("No Assemblies found for mod: " + entry.Name + " (" + entry.Id + ")");
                return assemblies;
            }

            foreach (var dll in Directory.GetFiles(asmPath, "*.dll"))
            {
                try
                {
                    MMLog.Write("Loading mod assembly: " + dll);
                    assemblies.Add(Assembly.LoadFile(dll));
                }
                catch (Exception ex)
                {
                    MMLog.Write("Failed to load assembly '" + dll + "': " + ex.Message);
                }
            }
        }
        catch (Exception ex)
        {
            MMLog.Write("Assembly load error for mod '" + entry.Id + "': " + ex.Message);
        }
        return assemblies;
    }

    // Chooses the assemblies folder either directly under Assemblies or a TFM subfolder
    // For Sheltered/.NET 3.5, prefer net35 if present; else fallback to Assemblies root.
    // Coolnether123
    private static string SelectAssembliesPath(string assembliesRoot)
    {
        if (string.IsNullOrEmpty(assembliesRoot) || !Directory.Exists(assembliesRoot))
            return null;

        // Look for known TFMs in descending preference for older Unity
        var tfms = new[] { "net35", "net3.5", "netstandard2.0", "net20", "net40", "net45", "net472", "net48" };
        foreach (var t in tfms)
        {
            var candidate = Path.Combine(assembliesRoot, t);
            if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "*.dll").Length > 0)
                return candidate;
        }

        // Fallback to Assemblies root
        if (Directory.GetFiles(assembliesRoot, "*.dll").Length > 0)
            return assembliesRoot;

        return null;
    }

    // Normalizes IDs for comparisons: lowercase and trim
    // Coolnether123
    private static string NormalizeId(string id)
    {
        return string.IsNullOrEmpty(id) ? id : id.Trim().ToLowerInvariant();
    }
}

