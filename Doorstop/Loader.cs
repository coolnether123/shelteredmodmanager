using System.IO;
using System.Threading;
using UnityEngine;

public static class Loader
{
    public static void Main(string[] args)
    {
        // create log-file in order to signalize the start of the application
        using (TextWriter tw = File.CreateText("mod_manager.log"))
        {
            tw.WriteLine("ModManager initialized!");
            tw.Flush();
        }

        // wait a short amount of time in order to let the game initialize itself
        new Thread(() =>
        {
            // after 2,5 seconds ...
            Thread.Sleep(2500);

            // THIS gameobject is the bridge between operating-system-context and ingame-context!
            GameObject doorstepGameObject = new GameObject("Doorstop");
            UnityEngine.Object.DontDestroyOnLoad(doorstepGameObject);

            // Load the plugins from the plugins-folder
            PluginManager pm = new PluginManager();
            pm.loadAssemblies();

        }).Start();

    }
}
    
