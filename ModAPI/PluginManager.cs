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
        DirectoryInfo dir = new DirectoryInfo(@"mods");
        Console.WriteLine("Looking for plugins in " + dir.FullName + " ...");

        ICollection<Assembly> assemblies = new List<Assembly>();
        foreach (FileInfo dllFile in dir.GetFiles("*.dll"))
        {
            Console.WriteLine("Loading " + dllFile + " ...");
            AssemblyName an = AssemblyName.GetAssemblyName(dllFile.FullName);
            Assembly assembly = Assembly.Load(an);
            assemblies.Add(assembly);
            Console.WriteLine("... loaded " + dllFile + " !");

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
        foreach (Type type in pluginTypes)
        {
            IPlugin plugin = (IPlugin)Activator.CreateInstance(type);
            plugins.Add(plugin);
            plugin.initialize();
            plugin.start(doorstepGameObject);
        }


    }

}
