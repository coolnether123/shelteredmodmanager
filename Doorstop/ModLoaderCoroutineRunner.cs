using System.Collections;
using UnityEngine;

/**
 * Coolnether123
 * Runs plugin loading on the Unity main thread via coroutine.
 */
public class ModLoaderCoroutineRunner : MonoBehaviour
{
    void Awake()
    {
        DontDestroyOnLoad(gameObject);
    }

    void Start()
    {
        StartCoroutine(Bootstrap());
    }

    private IEnumerator Bootstrap()
    {
        // Give the game a moment to initialize before loading mods
        yield return new WaitForSeconds(2.5f);

        try
        {
            // Create (or reuse) the persistent root for plugins
            GameObject doorstepGameObject = GameObject.Find("Doorstop");
            if (doorstepGameObject == null)
            {
                doorstepGameObject = new GameObject("Doorstop");
                doorstepGameObject.name = "Doorstop";
                DontDestroyOnLoad(doorstepGameObject);
            }

            // Load the plugins from the plugins-folder
            PluginManager pm = PluginManager.getInstance();
            pm.loadAssemblies(doorstepGameObject);
        }
        catch (System.SystemException ex)
        {
            try { System.IO.File.WriteAllText("doorstop_entry_error.log", ex.ToString()); } catch { }
            Debug.LogError(ex);
        }
    }
}