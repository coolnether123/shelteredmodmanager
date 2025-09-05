using System.IO;
using UnityEngine;

/**
 * Ensures mod loading starts on the Unity main thread after a scene is ready.
 */
public static class DoorstopBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void OnAfterSceneLoad()
    {
        // TODO(coolnether123): Investigate coroutine vs thread bootstrap timing; keep this as a guard only for now.
        MMLog.Write("[Bootstrap] OnAfterSceneLoad method called."); // Added log
        try
        {
            var go = new GameObject("ModLoaderCoroutineRunner");
            go.name = "ModLoaderCoroutineRunner";
            Object.DontDestroyOnLoad(go);
            MMLog.Write("[Bootstrap] Created ModLoaderCoroutineRunner GameObject."); // Added log
            go.AddComponent<ModLoaderCoroutineRunner>();
            MMLog.Write("[Bootstrap] Added ModLoaderCoroutineRunner component."); // Added log
        }
        catch (System.Exception ex)
        {
            try { File.WriteAllText("doorstop_entry_error.log", ex.ToString()); } catch { }
            Debug.LogError("[Bootstrap] Error in OnAfterSceneLoad: " + ex.ToString()); // Modified log
            MMLog.Write("[Bootstrap] Error in OnAfterSceneLoad: " + ex.Message); // Added MMLog
        }
    }
}
