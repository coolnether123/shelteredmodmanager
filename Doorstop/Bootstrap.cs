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
        try
        {
            var go = new GameObject("ModLoaderCoroutineRunner");
            go.name = "ModLoaderCoroutineRunner";
            Object.DontDestroyOnLoad(go);
            go.AddComponent<ModLoaderCoroutineRunner>();
        }
        catch (System.Exception ex)
        {
            try { File.WriteAllText("doorstop_entry_error.log", ex.ToString()); } catch { }
            Debug.LogError(ex);
        }
    }
}

