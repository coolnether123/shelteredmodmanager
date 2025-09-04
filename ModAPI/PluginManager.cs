using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
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
        string modsRoot = modsPath; // Consistent naming

        ICollection<Assembly> assemblies = new List<Assembly>();
        var activatedTypes = new HashSet<Type>(); // track explicitly activated entry types // Coolnether123

        // Read load order file
        var processedLof = LoadOrderResolver.ReadLoadOrderFile(modsRoot);

        // Discover all mods
        var discovered = ModDiscovery.DiscoverAllMods();

        // Filter mods based on enabled status from loadorder.json
        HashSet<string> enabledIds = null;
        if (processedLof.Mods != null && processedLof.Mods.Any())
        {
            enabledIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var kvp in processedLof.Mods)
            {
                if (kvp.Value.enabled)
                {
                    enabledIds.Add(kvp.Key);
                }
            }
        }

        if (enabledIds != null)
        {
            discovered = discovered.Where(m => enabledIds.Contains(m.Id)).ToList();
        }
        // If processedLof.Mods is null or empty, all discovered mods are treated as enabled by default.

        // Apply load order resolver
        var resolutionResult = LoadOrderResolver.Resolve(discovered, processedLof.Order ?? new string[0]);
        // (CN) TODO: Should display errors and cycle info to the user.
        discovered = resolutionResult.Mods;

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
