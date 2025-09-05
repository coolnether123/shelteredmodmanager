using System.Collections;
using UnityEngine;

/**
 * Coolnether123
 * Runs plugin loading on the Unity main thread via coroutine.
 */
public class ModLoaderCoroutineRunner : MonoBehaviour
{
    private static bool s_initialized = false;

    void Awake()
    {
        // Ensure only one runner instance exists across scene loads
        if (s_initialized)
        {
            MMLog.Write("[ModLoaderCoroutineRunner] Duplicate runner detected. Destroying this instance.");
            Destroy(this.gameObject);
            return;
        }
        s_initialized = true;

        DontDestroyOnLoad(gameObject);
        MMLog.Write("[ModLoaderCoroutineRunner] Awake method called. DontDestroyOnLoad called."); // Added log
    }

    void Start()
    {
        MMLog.Write("[ModLoaderCoroutineRunner] Start method called. Starting Bootstrap coroutine."); // Added log
        StartCoroutine(Bootstrap());
    }

    private IEnumerator Bootstrap()
    {
        MMLog.Write("[ModLoaderCoroutineRunner] Bootstrap coroutine started. Waiting for 2.5 seconds..."); // Added log
        // Give the game a moment to initialize before loading mods
        yield return new WaitForSeconds(2.5f);
        MMLog.Write("[ModLoaderCoroutineRunner] Wait finished. Proceeding with plugin loading."); // Added log

        try
        {
            // Create (or reuse) the persistent root for plugins
            GameObject doorstepGameObject = GameObject.Find("Doorstop");
            if (doorstepGameObject == null)
            {
                doorstepGameObject = new GameObject("Doorstop");
                doorstepGameObject.name = "Doorstop";
                DontDestroyOnLoad(doorstepGameObject);
                MMLog.Write("[ModLoaderCoroutineRunner] Created/Found 'Doorstop' GameObject."); // Added log
            }
            else
            {
                MMLog.Write("[ModLoaderCoroutineRunner] Reusing existing 'Doorstop' GameObject."); // Added log
            }


            // Load the plugins from the plugins-folder if not already loaded by another path
            MMLog.Write("[ModLoaderCoroutineRunner] Getting PluginManager instance.");
            PluginManager pm = PluginManager.getInstance();

            bool alreadyLoaded = false;
            try
            {
                var e = pm.GetPlugins().GetEnumerator();
                if (e.MoveNext()) alreadyLoaded = true;
            }
            catch { }

            if (alreadyLoaded)
            {
                MMLog.Write("[ModLoaderCoroutineRunner] Plugins already loaded. Skipping duplicate load.");
            }
            else
            {
                MMLog.Write("[ModLoaderCoroutineRunner] Calling PluginManager.loadAssemblies().");
                pm.loadAssemblies(doorstepGameObject);
                MMLog.Write("[ModLoaderCoroutineRunner] Plugin assemblies loaded successfully.");
            }
        }
        catch (System.Exception ex)
        {
            try { System.IO.File.WriteAllText("doorstop_entry_error.log", ex.ToString()); } catch { }
            Debug.LogError("[ModLoaderCoroutineRunner] Error during Bootstrap: " + ex.ToString()); // Modified log
            MMLog.Write("[ModLoaderCoroutineRunner] Error during Bootstrap: " + ex.Message); // Added MMLog
        }
    }
}
