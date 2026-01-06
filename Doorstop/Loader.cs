using System;
using HarmonyLib;
using System.Collections;
using System.IO;
using System.Linq;
using System.Threading;
using UnityEngine;

#region Entrypoint
namespace Doorstop
{
    public static class Entrypoint
    {
        /// <summary>
        /// Initial entry point invoked by Doorstop. Logs basics then starts the loader handshake.
        /// </summary>
        public static void Start()
        {
            try
            {
                File.WriteAllText("mod_manager.log", "Mod Loader starting!\n");
                var asm = typeof(Entrypoint).Assembly;
                File.AppendAllText("mod_manager.log", $"[Doorstop] Assembly: {asm.GetName().Name} {asm.GetName().Version}\n");
                File.AppendAllText("mod_manager.log", $"[Doorstop] Location: {asm.Location}\n");
                File.AppendAllText("mod_manager.log", $"[Doorstop] Process: {(IntPtr.Size == 8 ? "x64" : "x86")}\n");
                try
                {
                    var unityAsm = typeof(Application).Assembly;
                    File.AppendAllText("mod_manager.log", $"[Doorstop] UnityEngine: {unityAsm.GetName().Name} {unityAsm.GetName().Version}\n");
                    File.AppendAllText("mod_manager.log", $"[Doorstop] UnityEngine location: {unityAsm.Location}\n");
                    File.AppendAllText("mod_manager.log", $"[Doorstop] Unity version: unknown\n");
                }
                catch (System.Exception ex)
                {
                    File.AppendAllText("mod_manager.log", $"[Doorstop] UnityEngine probe failed: {ex}\n");
                }
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
    internal static bool BootstrapTriggered = false;

    /// <summary>
    /// Launches the bootstrap process. We wait for a Unity log callback to ensure
    /// 5.3 internal calls are registered before creating GameObjects.
    /// </summary>
    public static void Launch()
    {
        try
        {
            File.AppendAllText("mod_manager.log", "[Loader] Entrypoint reached; waiting for Unity log callback to confirm init\n");
            UnityInitHook.StartLogHookPoller();
        }
        catch (System.Exception ex)
        {
            File.AppendAllText("mod_manager.log", $"CRITICAL: {ex}\n");
        }
    }
}
#endregion

#region Log-based init (post-engine)
public static class UnityInitHook
{
    private static Thread _poller;
    private static bool _logHookRegistered = false;
    private static Application.LogCallback _logCallback = OnUnityLog;
    private static readonly object _lock = new object();
    private static string _unityVersion;

    public static void StartLogHookPoller()
    {
        if (_poller != null) return;
        _poller = new Thread(() =>
        {
            for (int i = 0; i < 120 && !Loader.BootstrapTriggered; i++)
            {
                TryRegisterLogHook();
                Thread.Sleep(500);
                if (Loader.BootstrapTriggered) break;
            }
            if (!Loader.BootstrapTriggered)
            {
                File.AppendAllText("mod_manager.log", "[Loader] Timed out waiting for Unity log callback\n");
            }
        });
        _poller.IsBackground = true;
        _poller.Start();
    }

    private static void TryRegisterLogHook()
    {
        if (Loader.BootstrapTriggered) return;
        lock (_lock)
        {
            if (_logHookRegistered) return;
            try
            {
                Application.logMessageReceived -= _logCallback;
            }
            catch { }

            try
            {
                Application.logMessageReceived += _logCallback;
                _logHookRegistered = true;
                File.AppendAllText("mod_manager.log", "[Loader] Subscribed to Application.logMessageReceived (retry)\n");
            }
            catch (System.Exception ex)
            {
                File.AppendAllText("mod_manager.log", $"[Loader] logMessageReceived registration failed (retry): {ex}\n");
            }
        }
    }

    private static void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        if (Loader.BootstrapTriggered) return;
        lock (_lock)
        {
            if (Loader.BootstrapTriggered) return;
            Loader.BootstrapTriggered = true;
            _unityVersion = GetUnityVersionSafe();
        }
        try { Application.logMessageReceived -= _logCallback; } catch { }
        try
        {
            if (!string.IsNullOrEmpty(_unityVersion))
                File.AppendAllText("mod_manager.log", $"[Loader] Unity version (post-init): {_unityVersion}\n");
            File.AppendAllText("mod_manager.log", "[Loader] logMessageReceived fired; triggering bootstrap\n");
            DoorstopBootstrap.Trigger();
            File.AppendAllText("mod_manager.log", "[Loader] Trigger completed\n");
        }
        catch (System.Exception ex)
        {
            File.AppendAllText("mod_manager.log", $"CRITICAL: {ex}\n");
        }
    }

    private static string GetUnityVersionSafe()
    {
        try
        {
            var prop = typeof(Application).GetProperty("unityVersion", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null)
            {
                var val = prop.GetValue(null, null) as string;
                if (!string.IsNullOrEmpty(val)) return val;
            }
        }
        catch { }
        return null;
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
            File.AppendAllText("mod_manager.log", "[Bootstrap] Creating GameObject\n");
            var go = new GameObject("ModLoaderCoroutineRunner");
            File.AppendAllText("mod_manager.log", "[Bootstrap] GameObject created\n");

            UnityEngine.Object.DontDestroyOnLoad(go);
            File.AppendAllText("mod_manager.log", "[Bootstrap] DontDestroyOnLoad set\n");

            go.AddComponent<ModLoaderCoroutineRunner>();
            File.AppendAllText("mod_manager.log", "[Bootstrap] Component added\n");
        }
        catch (System.Exception ex)
        {
            File.AppendAllText("mod_manager.log", $"CRITICAL: {ex}\n");
        }
    }
}
#endregion

#region Coroutine Runner
public class ModLoaderCoroutineRunner : MonoBehaviour
{
    private static bool _isInitialized = false;
    private bool _bootstrapStarted = false;

    void Awake()
    {
        File.AppendAllText("mod_manager.log", "[ModLoaderCoroutineRunner] Awake called\n");

        if (_isInitialized)
        {
            File.AppendAllText("mod_manager.log", "[ModLoaderCoroutineRunner] Already initialized, destroying\n");
            Destroy(this.gameObject);
            return;
        }
        _isInitialized = true;
        DontDestroyOnLoad(gameObject);
        File.AppendAllText("mod_manager.log", "[ModLoaderCoroutineRunner] Initialized\n");
    }

    void Start()
    {
        File.AppendAllText("mod_manager.log", "[ModLoaderCoroutineRunner] Start called\n");
        BeginBootstrap("Start");
    }

    void OnLevelWasLoaded(int level)
    {
        BeginBootstrap("OnLevelWasLoaded");
    }

    private void BeginBootstrap(string source)
    {
        if (_bootstrapStarted) return;
        _bootstrapStarted = true;

        try
        {
            File.AppendAllText("mod_manager.log", $"[ModLoaderCoroutineRunner] Starting bootstrap from {source}\n");
            var coroutine = Bootstrap();
            File.AppendAllText("mod_manager.log", "[ModLoaderCoroutineRunner] Coroutine created\n");
            StartCoroutine(coroutine);
            File.AppendAllText("mod_manager.log", "[ModLoaderCoroutineRunner] Coroutine started\n");
        }
        catch (System.Exception ex)
        {
            File.AppendAllText("mod_manager.log", $"[ModLoaderCoroutineRunner] ERROR in {source}: {ex}\n");
        }
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
        File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] Started\n");

        float timeout = 15f;
        int frameCount = 0;
        while (Camera.main == null)
        {
            frameCount++;
            timeout -= Time.deltaTime;
            if (timeout <= 0f)
            {
                File.AppendAllText("mod_manager.log", $"[Bootstrap Coroutine] Timeout after {frameCount} frames\n");
                yield break;
            }
            yield return null;
        }

        File.AppendAllText("mod_manager.log", $"[Bootstrap Coroutine] Camera ready after {frameCount} frames\n");
        yield return new WaitForSeconds(0.5f);

        try
        {
            File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] Handing off to PluginManager have fun!\n");
            // Pass this GameObject (the ModLoaderCoroutineRunner) to the PluginManager.
            // It will serve as the root parent for all mod-related GameObjects and as a host
            // for any global behaviours the modding framework needs, like the PluginRunner.

            // Get the game root and construct paths
            string gameRoot = System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string smmPath = System.IO.Path.Combine(gameRoot, "SMM");
            string smmBinPath = System.IO.Path.Combine(smmPath, "bin");

            // Add an AssemblyResolve handler to find assemblies
            System.AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                // IMPORTANT: Check already-loaded assemblies first to avoid duplicate loads
                var loaded = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
                if (loaded != null)
                {
                    File.AppendAllText("mod_manager.log", $"[AssemblyResolve] Already loaded: {args.Name}\n");
                    return loaded;
                }

                string assemblyName = new System.Reflection.AssemblyName(args.Name).Name;
                string assemblyPath = System.IO.Path.Combine(smmBinPath, assemblyName + ".dll");

                if (System.IO.File.Exists(assemblyPath))
                {
                    File.AppendAllText("mod_manager.log", $"[AssemblyResolve] Found {assemblyName} at {assemblyPath}\n");
                    return System.Reflection.Assembly.LoadFrom(assemblyPath);
                }

                // Also check SMM root
                assemblyPath = System.IO.Path.Combine(smmPath, assemblyName + ".dll");
                if (System.IO.File.Exists(assemblyPath))
                {
                    File.AppendAllText("mod_manager.log", $"[AssemblyResolve] Found {assemblyName} at {assemblyPath}\n");
                    return System.Reflection.Assembly.LoadFrom(assemblyPath);
                }

                return null;
            };

            File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] Loading ModAPI via reflection\n");

            string modApiPath = System.IO.Path.Combine(smmPath, "ModAPI.dll");

            File.AppendAllText("mod_manager.log", $"[Bootstrap Coroutine] ModAPI path: {modApiPath}\n");

            if (!System.IO.File.Exists(modApiPath))
            {
                File.AppendAllText("mod_manager.log", $"[Bootstrap Coroutine] ERROR: ModAPI.dll not found at {modApiPath}\n");
                yield break;
            }

            // Load from the explicit path
            var modApiAsm = System.Reflection.Assembly.LoadFrom(modApiPath);
            File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] ModAPI assembly loaded\n");

            var pmType = modApiAsm.GetType("ModAPI.Core.PluginManager");
            File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] PluginManager type obtained\n");

            var getInstance = pmType.GetMethod("getInstance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var pm = getInstance.Invoke(null, null);
            File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] getInstance called\n");

            var loadMethod = pmType.GetMethod("loadAssemblies");
            loadMethod.Invoke(pm, new object[] { this.gameObject });
            File.AppendAllText("mod_manager.log", "[Bootstrap Coroutine] loadAssemblies completed successfully!\n");
        }
        catch (System.Exception ex)
        {
            File.AppendAllText("mod_manager.log", $"[Bootstrap Coroutine] ERROR: {ex}\n");
        }
    }
}
#endregion
