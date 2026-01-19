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
                string arch = IntPtr.Size == 8 ? "x64" : "x86";
                string smmLog = Path.Combine("SMM", "mod_manager.log");
                if (!Directory.Exists("SMM")) Directory.CreateDirectory("SMM");
                File.WriteAllText(smmLog, $"[Doorstop] Sheltered Mod Manager starting ({arch})\n");
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
            UnityInitHook.StartLogHookPoller();
        }
        catch (System.Exception ex)
        {
            File.AppendAllText(Path.Combine("SMM", "mod_manager.log"), $"[Loader] CRITICAL: {ex}\n");
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
                File.AppendAllText(Path.Combine("SMM", "mod_manager.log"), "[Loader] ERROR: Timed out waiting for Unity\n");
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
            }
            catch { }
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
                File.AppendAllText(Path.Combine("SMM", "mod_manager.log"), $"[Doorstop] Unity {_unityVersion} detected\n");
            DoorstopBootstrap.Trigger();
        }
        catch (System.Exception ex)
        {
            File.AppendAllText(Path.Combine("SMM", "mod_manager.log"), $"[Loader] CRITICAL: {ex}\n");
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
    /// </summary>
    public static void Trigger()
    {
        if (_isTriggered) return;
        _isTriggered = true;

        try
        {
            var go = new GameObject("ModLoaderCoroutineRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<ModLoaderCoroutineRunner>();
        }
        catch (System.Exception ex)
        {
            File.AppendAllText(Path.Combine("SMM", "mod_manager.log"), $"[Bootstrap] CRITICAL: {ex}\n");
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
        BeginBootstrap();
    }

    void OnLevelWasLoaded(int level)
    {
        BeginBootstrap();
    }

    private void BeginBootstrap()
    {
        if (_bootstrapStarted) return;
        _bootstrapStarted = true;

        try
        {
            StartCoroutine(Bootstrap());
        }
        catch (System.Exception ex)
        {
            File.AppendAllText(Path.Combine("SMM", "mod_manager.log"), $"[Bootstrap] ERROR: {ex}\n");
        }
    }

    /// <summary>
    /// The main bootstrap coroutine. Waits for game to be ready then loads ModAPI.
    /// </summary>
    private IEnumerator Bootstrap()
    {
        float timeout = 15f;
        while (Camera.main == null)
        {
            timeout -= Time.deltaTime;
            if (timeout <= 0f)
            {
                File.AppendAllText(Path.Combine("SMM", "mod_manager.log"), "[Bootstrap] ERROR: Camera timeout\n");
                yield break;
            }
            yield return null;
        }

        yield return new WaitForSeconds(0.5f);

        try
        {
            // Get the game root and construct paths
            string gameRoot = System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string smmPath = System.IO.Path.Combine(gameRoot, "SMM");
            string smmBinPath = System.IO.Path.Combine(smmPath, "bin");

            // Add an AssemblyResolve handler to find assemblies
            System.AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                // Check already-loaded assemblies first
                var loaded = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.FullName == args.Name);
                if (loaded != null) return loaded;

                string assemblyName = new System.Reflection.AssemblyName(args.Name).Name;
                string assemblyPath = System.IO.Path.Combine(smmBinPath, assemblyName + ".dll");

                if (System.IO.File.Exists(assemblyPath))
                    return System.Reflection.Assembly.LoadFrom(assemblyPath);

                // Also check SMM root
                assemblyPath = System.IO.Path.Combine(smmPath, assemblyName + ".dll");
                if (System.IO.File.Exists(assemblyPath))
                    return System.Reflection.Assembly.LoadFrom(assemblyPath);

                return null;
            };

            string modApiPath = System.IO.Path.Combine(smmPath, "ModAPI.dll");

            if (!System.IO.File.Exists(modApiPath))
            {
                File.AppendAllText(Path.Combine("SMM", "mod_manager.log"), $"[Bootstrap] ERROR: ModAPI.dll not found at {modApiPath}\n");
                yield break;
            }

            // Load ModAPI and hand off to PluginManager
            var modApiAsm = System.Reflection.Assembly.LoadFrom(modApiPath);
            var pmType = modApiAsm.GetType("ModAPI.Core.PluginManager");
            var getInstance = pmType.GetMethod("getInstance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            var pm = getInstance.Invoke(null, null);
            var loadMethod = pmType.GetMethod("loadAssemblies");
            loadMethod.Invoke(pm, new object[] { this.gameObject });
            
            File.AppendAllText(Path.Combine("SMM", "mod_manager.log"), "[Doorstop] Handoff to ModAPI complete\n");
        }
        catch (System.Exception ex)
        {
            File.AppendAllText("mod_manager.log", $"[Bootstrap] ERROR: {ex}\n");
        }
    }
}
#endregion
