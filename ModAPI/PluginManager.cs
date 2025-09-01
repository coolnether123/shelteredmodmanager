using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;


/**
* Author: benjaminfoo
* See: https://github.com/benjaminfoo/shelteredmodmanager
* See: https://code.msdn.microsoft.com/windowsdesktop/Creating-a-simple-plugin-b6174b62
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
        MMLog.Write("PluginManager: loadAssemblies started.");
        // The original relative path for the mods directory was unreliable.
        // This has been changed to build a robust, absolute path from the game's data folder.
        string gameRootPath = Directory.GetParent(Application.dataPath).FullName;
        string modsPath = Path.Combine(gameRootPath, "mods");
        DirectoryInfo dir = new DirectoryInfo(Path.Combine(modsPath, "enabled"));
        MMLog.Write(string.Format("PluginManager: Looking for plugins in {0} ...", dir.FullName));

        ICollection<Assembly> assemblies = new List<Assembly>();
        try
        {
            if (!dir.Exists)
            {
                MMLog.Write(string.Format("PluginManager: Directory {0} does not exist. No plugins to load.", dir.FullName));
                return;
            }

            foreach (FileInfo dllFile in dir.GetFiles("*.dll"))
            {
                MMLog.Write(string.Format("PluginManager: Found DLL: {0}", dllFile.Name));
                try
                {
                                        Assembly assembly = Assembly.LoadFile(dllFile.FullName);
                    assemblies.Add(assembly);
                    MMLog.Write(string.Format("PluginManager: ... loaded assembly {0}!", assembly.FullName));
                }
                catch (Exception ex)
                {
                    MMLog.Write(string.Format("PluginManager: Error loading assembly {0}: {1}", dllFile.Name, ex.Message));
                }
            }
        }
        catch (Exception ex)
        {
            MMLog.Write(string.Format("PluginManager: Error getting DLL files from directory {0}: {1}", dir.FullName, ex.Message));
            return;
        }

        Type pluginType = typeof(IPlugin);
        ICollection<Type> pluginTypes = new List<Type>();
        foreach (Assembly assembly in assemblies)
        {
            if (assembly != null)
            {
                try
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
                                MMLog.Write(string.Format("PluginManager: Found IPlugin type: {0}", type.FullName));
                            }
                        }
                    }
                }
                catch (ReflectionTypeLoadException rtle)
                {
                    MMLog.Write(string.Format("PluginManager: ReflectionTypeLoadException for assembly {0}.", assembly.FullName));
                    if (rtle.LoaderExceptions != null)
                    {
                        foreach (var e in rtle.LoaderExceptions)
                        {
                            MMLog.Write(string.Format("PluginManager:   LoaderException: {0}", e.Message));
                        }
                    }
                }
                catch (Exception ex)
                {
                    MMLog.Write(string.Format("PluginManager: Error getting types from assembly {0}: {1}", assembly.FullName, ex.Message));
                }
            }
        }
        MMLog.Write(string.Format("PluginManager: Found {0} IPlugin types.", pluginTypes.Count));

        // initialize the plugins and start them from the unity-context
        foreach (Type type in pluginTypes)
        {
            try
            {
                IPlugin plugin = (IPlugin)Activator.CreateInstance(type);
                plugins.Add(plugin);
                MMLog.Write(string.Format("PluginManager: Initializing plugin: {0}", plugin.Name));
                plugin.initialize();
                plugin.start(doorstepGameObject);
                MMLog.Write(string.Format("PluginManager: Plugin {0} started.", plugin.Name));
            }
            catch (Exception ex)
            {
                MMLog.Write(string.Format("PluginManager: Error initializing or starting plugin {0}: {1}", type.FullName, ex.Message));
            }
        }
        MMLog.Write("PluginManager: loadAssemblies finished.");
    }

}
