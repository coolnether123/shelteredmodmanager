using HarmonyLib;
using System.Collections;
using System.IO;
using System.Threading;
using UnityEngine;


/**
 * Author: Coolnether123
 * 
 * A multi-stage, coroutine-based bootstrap for loading mods into Unity,
 * ensuring mods are loaded on the main thread after the game scene is initialized.
 * 
 * Based on the original ShelteredModManager project by benjaminfoo.
 */

#region Entrypoint
namespace Doorstop
{
    public static class Entrypoint
    {
        /// <summary>
        /// This is the initial entry point called by Unity Doorstop.
        /// Its only responsibility is to safely kick off the mod loading process
        /// on a background thread to avoid blocking the game's startup sequence.
        /// All logging and error handling starts here.
        /// </summary>
        public static void Start()
        {
            try
            {
                File.WriteAllText("mod_manager.log", "Mod Loader starting!\n");
                Loader.Launch();
            }
            catch (System.Exception ex)
            {
                try { File.WriteAllText("doorstop_entry_error.log", ex.ToString()); } catch { }
            }
        }
    }
}
#endregion

#region Loader Thread
public static class Loader
{
    /// <summary>
    /// Launches the bootstrap process on a background thread.
    /// Why the background thread: Doorstop injects code extremely early in the game's startup,
    /// before the Unity engine is fully initialized. Attempting to create Unity GameObjects
    /// or run Coroutines directly at this stage will fail.
    /// This thread's purpose is to simply wait for a short period, allowing the game
    /// engine to load, before safely triggering the main-thread portion of the bootstrap.
    /// </summary>
    public static void Launch()
    {
        new Thread(() =>
        {
            File.AppendAllText("mod_manager.log", "[Loader Thread] Started. Waiting 2.5s\n");
            Thread.Sleep(2500);
        try
            {
                //File.AppendAllText("mod_manager.log", "[Loader Thread] Triggering main-thread bootstrap\n");
                DoorstopBootstrap.Trigger();
            }
            catch (System.Exception ex)
            {
                File.AppendAllText("mod_manager.log", $"CRITICAL: Failed to trigger main-thread bootstrap: {ex}\n");
            }

        }).Start();
    }
}
#endregion

#region Main-Thread Bootstrapper
public static class DoorstopBootstrap
{
    private static bool _isTriggered = false;

    /// <summary>
    /// Triggers the creation of the main-thread Coroutine Runner.
    /// This method is called from the background Loader thread. While it creates a GameObject,
    /// it's still happening at a very early, fragile point in initialization. The real
    /// "safe" logic begins in the ModLoaderCoroutineRunner.
    /// The _isTriggered flag ensures this process only ever runs once.
    /// </summary>
    public static void Trigger()
    {
        if (_isTriggered) return;
        _isTriggered = true;

        try
        {
            File.AppendAllText("mod_manager.log", "[Bootstrap] Creating Coroutine Runner\n");
            var go = new GameObject("ModLoaderCoroutineRunner");
            Object.DontDestroyOnLoad(go);
            go.AddComponent<ModLoaderCoroutineRunner>();
        }
        catch (System.Exception ex)
        {
            File.AppendAllText("mod_manager.log", $"CRITICAL: Error creating Coroutine Runner: {ex}\n");
        }
    }
}
#endregion

#region Coroutine Runner
public class ModLoaderCoroutineRunner : MonoBehaviour
{
    private static bool _isInitialized = false;

    void Awake()
     {
         if (_isInitialized)
         {
             Destroy(this.gameObject);
             return;
         }
         _isInitialized = true;
         DontDestroyOnLoad(gameObject);
     }
     
     void Start()
     {
         StartCoroutine(Bootstrap());
     }


    /// <summary>
    /// The main bootstrap coroutine. This is guaranteed to run on the main thread.
    /// It waits for the game to be in a "ready state" before loading plugins.
    /// Current reason for Camera.main: In Unity, Camera.main is only non-null once
    /// a scene has fully loaded and its primary camera is active. Waiting for this is a
    /// reliable signal that the game is ready than a fixed-time delay.
    /// This prevents race conditions and ensures the PluginManager can safely interact
    /// with the game world.
    /// </summary> 
     private IEnumerator Bootstrap()
     {
        File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] Started. Waiting for main camera\n");
     
        float timeout = 15f;
        // This loop is the core of the safe-loading strategy.
        while (Camera.main == null)
        {
            timeout -= Time.deltaTime;
            if (timeout <= 0f)
            {
                File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] ERROR: Timed out waiting for Camera.main.\n");
                yield break;
            }
            yield return null;
        }
        // A small extra delay for safty in case other game scripts need to run their own Awake/Start methods.
        File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] Camera found! Waiting 0.5s\n");
        yield return new WaitForSeconds(0.5f);
     
     
        try
        {
            File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] Handing off to PluginManager have fun!\n");
            // Pass this GameObject (the ModLoaderCoroutineRunner) to the PluginManager.
            // It will serve as the root parent for all mod-related GameObjects and as a host
            // for any global behaviours the modding framework needs, like the PluginRunner.
            PluginManager.getInstance().loadAssemblies(this.gameObject);
        }
        catch (System.Exception ex)
        {
            File.AppendAllText("mod_manager.log", $"CRITICAL: Exception during plugin loading: {ex}\n");
        }
     }  
}
#endregion