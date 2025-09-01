using System.IO;
using System.Threading;
using UnityEngine;

/**
 * Author: benjaminfoo
 * Maintainer: coolnether123
 * See: https://github.com/benjaminfoo/shelteredmodmanager
 * 
 * The class gets loaded by the UnityDoorstop-hook which is initiated in the winhttp.dll/version.dll
 */
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

        new Thread(() =>
        {
            // wait a short amount of time in order to let the game initialize itself
            Thread.Sleep(2500);
            
            // THIS gameobject is the bridge between operating-system-context and ingame-context!
            GameObject doorstepGameObject = new GameObject("Doorstop");
            doorstepGameObject.name = "Doorstop";
            UnityEngine.Object.DontDestroyOnLoad(doorstepGameObject);

            // Load the plugins from the plugins-folder
            PluginManager pm = PluginManager.getInstance();
            pm.loadAssemblies(doorstepGameObject);

        }).Start();

    }

}

namespace Doorstop
{
    public static class Entrypoint
    {
        public static void Start()
        {
            try
            {
                global::Loader.Main(new string[0]);
            }
            catch (System.Exception ex)
            {
                try { System.IO.File.WriteAllText("doorstop_entry_error.log", ex.ToString()); } catch { }
            }
        }
    }
}