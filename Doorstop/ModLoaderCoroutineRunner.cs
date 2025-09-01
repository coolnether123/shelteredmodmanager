using System.Collections;
using UnityEngine;
using System; // Added for Exception

/**
 * Runs plugin loading on the Unity main thread via coroutine.
 */
public class ModLoaderCoroutineRunner : MonoBehaviour
{
    void Awake()
    {
        MMLog.Write("ModLoaderCoroutineRunner: Awake called.");
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        MMLog.Write("ModLoaderCoroutineRunner: Start called. Starting Bootstrap coroutine.");
        StartCoroutine(Bootstrap());
    }

    private IEnumerator Bootstrap()
    {
        MMLog.Write("ModLoaderCoroutineRunner: Bootstrap coroutine started. Waiting 2.5 seconds.");
        // Give the game a moment to initialize before loading mods
        yield return new WaitForSeconds(2.5f);

        MMLog.Write("ModLoaderCoroutineRunner: Coroutine resumed. Attempting to load plugins.");
        try
        {
            // Create (or reuse) the persistent root for plugins
            GameObject doorstepGameObject = GameObject.Find("Doorstop");
            if (doorstepGameObject == null)
            {
                MMLog.Write("ModLoaderCoroutineRunner: 'Doorstop' GameObject not found, creating new one.");
                doorstepGameObject = new GameObject("Doorstop");
                doorstepGameObject.name = "Doorstop";
                DontDestroyOnLoad(doorstepGameObject);
                MMLog.Write("ModLoaderCoroutineRunner: New 'Doorstop' GameObject created.");
            }
            else
            {
                MMLog.Write("ModLoaderCoroutineRunner: Found existing 'Doorstop' GameObject.");
            }

            // Load the plugins from the plugins-folder
            PluginManager pm = PluginManager.getInstance();
            MMLog.Write("ModLoaderCoroutineRunner: PluginManager instance obtained. Calling loadAssemblies.");
            pm.loadAssemblies(doorstepGameObject);
            MMLog.Write("ModLoaderCoroutineRunner: loadAssemblies completed.");
        }
        catch (Exception ex) // Changed from System.SystemException to Exception for broader catch
        {
            MMLog.Write(string.Format("ModLoaderCoroutineRunner: FATAL ERROR during plugin loading: {0}", ex.ToString()));
            try { System.IO.File.WriteAllText("doorstop_entry_error.log", ex.ToString()); } catch { } // Keep this for raw error
            // Debug.LogError(ex); // Removed problematic Debug.LogError
        }
        MMLog.Write("ModLoaderCoroutineRunner: Bootstrap coroutine finished.");
    }
}
