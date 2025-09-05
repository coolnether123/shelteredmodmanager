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
    public static readonly string LogFilePath = "mod_manager.log"; // Made public static for Entrypoint to access

    public static void Main(string[] args)
    {
        File.AppendAllText(LogFilePath, "[Loader] Main method called.\n");

        // This initial write is now handled by Entrypoint.Start() to ensure file is cleared.
        // File.AppendAllText(LogFilePath, "ModManager initialized!\n"); 

        File.AppendAllText(LogFilePath, "[Loader] Starting new thread for delayed initialization.\n");
        new Thread(() =>
        {
            File.AppendAllText(LogFilePath, "[Loader Thread] Thread started. Waiting for 2.5 seconds...\n");
            // wait a short amount of time in order to let the game initialize itself
            Thread.Sleep(2500);
            File.AppendAllText(LogFilePath, "[Loader Thread] Wait finished. Creating Doorstop GameObject...\n");
            
            // THIS gameobject is the bridge between operating-system-context and ingame-context!
            GameObject doorstepGameObject = new GameObject("Doorstop");
            doorstepGameObject.name = "Doorstop";
            UnityEngine.Object.DontDestroyOnLoad(doorstepGameObject);
            File.AppendAllText(LogFilePath, "[Loader Thread] Doorstop GameObject created and DontDestroyOnLoad called.\n");

            // Load the plugins from the plugins-folder
            File.AppendAllText(LogFilePath, "[Loader Thread] Getting PluginManager instance and loading assemblies...\n");
            PluginManager pm = PluginManager.getInstance();
            pm.loadAssemblies(doorstepGameObject);
            File.AppendAllText(LogFilePath, "[Loader Thread] Plugin assemblies loaded.\n");

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
                // Clear the log file at the very beginning of each run
                System.IO.File.WriteAllText(global::Loader.LogFilePath, "ModManager initialized!\n");
                System.IO.File.AppendAllText(global::Loader.LogFilePath, "[Doorstop.Entrypoint] Start method called.\n");
                global::Loader.Main(new string[0]);
            }
            catch (System.Exception ex)
            {
                try { System.IO.File.WriteAllText("doorstop_entry_error.log", ex.ToString()); } catch { }
            }
        }
    }
}
