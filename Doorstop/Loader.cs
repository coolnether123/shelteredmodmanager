using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class Loader
{
    private static bool _bootstrapped = false;

    // Expose a single, absolute path everyone uses
    public static string LogPath { get; private set; }

    public static void Main(string[] args)
    {
        // Decide a stable absolute path ONCE.
        // Prefer persistentDataPath (writable & stable), else fall back to game folder.
        try
        {
            var baseDir = Application.persistentDataPath; // usually: %LocalAppData%/../LocalLow/<Company>/<Product>
            if (string.IsNullOrEmpty(baseDir))
                baseDir = Directory.GetCurrentDirectory();

            Directory.CreateDirectory(baseDir);
            LogPath = Path.Combine(baseDir, "mod_manager.log");
        }
        catch
        {
            // Extreme fallback: current directory
            LogPath = Path.Combine(Directory.GetCurrentDirectory(), "mod_manager.log");
        }

        // Append the "initialized" line using TextWriter (no overwrite!)
        try
        {
            MMLog.Write("ModManager initialized!");
        }
        catch { /* swallow file errors so we don't crash early */ }

        // Subscribe to sceneLoaded to bootstrap on the main thread once a scene is ready
        try
        {
            SceneManager.sceneLoaded -= OnSceneLoaded; // avoid duplicate subscriptions
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        catch (System.Exception ex)
        {
            try { System.IO.File.WriteAllText("doorstop_entry_error.log", "Failed to subscribe sceneLoaded: " + ex); } catch { }
        }
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_bootstrapped) return;
        _bootstrapped = true;

        // Breadcrumb so you can verify append works after scene load too
        try
        {
            using (TextWriter tw = File.AppendText(LogPath))
                tw.WriteLine($"[{System.DateTime.Now:HH:mm:ss}] OnSceneLoaded: {scene.name} ({mode})");
        }
        catch { }

        try
        {
            var runnerGO = new GameObject("ModLoaderCoroutineRunner");
            Object.DontDestroyOnLoad(runnerGO);
            runnerGO.AddComponent<ModLoaderCoroutineRunner>();

            using (TextWriter tw = File.AppendText(LogPath))
                tw.WriteLine($"[{System.DateTime.Now:HH:mm:ss}] Created ModLoaderCoroutineRunner.");
        }
        catch (System.Exception ex)
        {
            try { System.IO.File.WriteAllText("doorstop_entry_error.log", "Failed to create runner: " + ex); } catch { }
            Debug.LogError(ex);
        }
        finally
        {
            try { SceneManager.sceneLoaded -= OnSceneLoaded; } catch { }
        }
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
