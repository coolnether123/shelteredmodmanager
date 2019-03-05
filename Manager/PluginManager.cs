using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

/**
 * See: https://code.msdn.microsoft.com/windowsdesktop/Creating-a-simple-plugin-b6174b62
 */
public class PluginManager
{

    private ICollection<IPlugin> plugins;

    public PluginManager() {
        plugins = new List<IPlugin>();
    }

    public void loadAssemblies() {
        DirectoryInfo dir = new DirectoryInfo("C:\\Program Files (x86)\\Steam\\steamapps\\common\\Sheltered\\manager_mods\\");
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

        foreach (Type type in pluginTypes)
        {
            IPlugin plugin = (IPlugin)Activator.CreateInstance(type);
            plugins.Add(plugin);

            plugin.initialize();
        }


    }

}
