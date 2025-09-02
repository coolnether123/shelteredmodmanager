using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;


/**
* Original Author: benjaminfoo
* Maintainer: coolnether123
* See: https://github.com/benjaminfoo/shelteredmodmanager
* See: https://code.msdn.microsoft.com/windowsdesktop/Creating-a-simple-plugin-b6174b62
* 
* 
* This class handles the overall plugin-management (like loading and executing them)
*/
public class PluginManager
{
    private static PluginManager instance;

    private ICollection<IPlugin> plugins;

    private PluginManager() {
        plugins = new List<IPlugin>();
    }

    public ICollection<IPlugin> GetPlugins() {
        return plugins;
            }

    public static PluginManager getInstance() {
        if (instance == null) {
            instance = new PluginManager();
        }

        return instance;
    }

    public void loadAssemblies(GameObject doorstepGameObject) {
        // Build absolute path to the mods directory (from game's data folder)
        string gameRootPath = Directory.GetParent(Application.dataPath).FullName;
        string modsPath = Path.Combine(gameRootPath, "mods");
        string enabledPath = Path.Combine(modsPath, "enabled");
        Debug.Log("Looking for plugins in " + enabledPath + " ...");

        ICollection<Assembly> assemblies = new List<Assembly>();
        var activatedTypes = new HashSet<Type>(); // track explicitly activated entry types // Coolnether123

        // New: About-driven mod discovery (About.json inside About/)
        // Coolnether123
        var discovered = ModDiscovery.DiscoverEnabledMods();
        // Determine mods root (Coolnether123)
        string modsRoot = modsPath;
        // Apply load order resolver (Coolnether123)
        discovered = LoadOrderResolver.Resolve(discovered, modsRoot);

        foreach (var mod in discovered)
        {
            var modAssemblies = ModDiscovery.LoadAssemblies(mod);
            foreach (var asm in modAssemblies)
            {
                assemblies.Add(asm);
                // Register mapping between assembly and its mod for settings resolution (Coolnether123)
                ModRegistry.RegisterAssemblyForMod(asm, mod);
            }

            // Respect explicit entry type if provided (must implement IPlugin)
            // Coolnether123
            try
            {
                if (mod.About != null && !string.IsNullOrEmpty(mod.About.entryType))
                {
                    Type entry = null;
                    foreach (var asm in modAssemblies)
                    {
                        // Try fast path: fully qualified name lookup in this assembly
                        entry = asm.GetType(mod.About.entryType, false);
                        if (entry != null) break;
                        // Slow path: scan types if needed
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.FullName == mod.About.entryType)
                            {
                                entry = t;
                                break;
                            }
                        }
                        if (entry != null) break;
                    }

                    if (entry != null)
                    {
                        if (!entry.IsInterface && !entry.IsAbstract && entry.GetInterface(typeof(IPlugin).FullName) != null)
                        {
                            IPlugin plugin = (IPlugin)Activator.CreateInstance(entry);
                            plugins.Add(plugin);
                            activatedTypes.Add(entry);
                            plugin.initialize();
                            plugin.start(doorstepGameObject);
                        }
                        else
                        {
                            MMLog.Write("Entry type does not implement IPlugin: " + mod.About.entryType);
                        }
                    }
                    else
                    {
                        MMLog.Write("Entry type not found: " + mod.About.entryType + " in mod " + mod.Name);
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("Error activating entryType for mod '" + mod.Name + "': " + ex.Message);
            }
        }

        // Backward compatibility: also load any loose DLLs directly under mods/enabled
        // This supports legacy mods until migrated to About.json format.
        // Coolnether123
        try
        {
            DirectoryInfo dir = new DirectoryInfo(enabledPath);
            if (dir.Exists)
            {
                foreach (FileInfo dllFile in dir.GetFiles("*.dll"))
                {
                    Debug.Log("[Legacy] Loading " + dllFile + " ...");
                    Assembly assembly = Assembly.LoadFile(dllFile.FullName);
                    assemblies.Add(assembly);
                    // Legacy DLLs: no about; still register best-effort so settings can look next to the DLL (Coolnether123)
                    try
                    {
                        var legacyEntry = new ModEntry
                        {
                            Id = dllFile.Name.ToLowerInvariant(),
                            Name = dllFile.Name,
                            Version = null,
                            RootPath = dllFile.DirectoryName,
                            AboutPath = null,
                            AssembliesPath = dllFile.DirectoryName,
                            About = null
                        };
                        ModRegistry.RegisterAssemblyForMod(assembly, legacyEntry);
                    }
                    catch { }
                    Debug.Log("[Legacy] ... loaded " + dllFile + " !");
                }
            }
        }
        catch (Exception ex)
        {
            MMLog.Write("Error loading legacy DLLs: " + ex.Message);
        }

        Type pluginType = typeof(IPlugin);
        ICollection<Type> pluginTypes = new List<Type>();
        foreach (Assembly assembly in assemblies)
        {
            if (assembly != null)
            {
                Type[] types = assembly.GetTypes();
                foreach (Type type in types)
                {
                    if (type.IsInterface || type.IsAbstract)
                    {
                        continue;
                    }
                    else
                    {
                        if (type.GetInterface(pluginType.FullName) != null)
                        {
                            pluginTypes.Add(type);
                        }
                    }
                }
            }
        }

        // initialize the plugins and start them from the unity-context
        // If a mod provided an entryType (About.json), it will still be picked up
        // here as long as that type implements IPlugin. This keeps the system simple
        // while enabling explicit entry points. // Coolnether123
        foreach (Type type in pluginTypes)
        {
            if (activatedTypes.Contains(type)) continue; // skip already started entry types // Coolnether123
            IPlugin plugin = (IPlugin)Activator.CreateInstance(type);
            plugins.Add(plugin);
            plugin.initialize();
            plugin.start(doorstepGameObject);
        }


    }

}
