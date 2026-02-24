using System;
using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using UnityEngine;

internal static class LoaderDebugLog
{
    private static readonly object _sync = new object();

    private static string SmmLogPath
    {
        get { return Path.Combine("SMM", "mod_manager.log"); }
    }

    private static string RootLogPath
    {
        get { return "mod_manager.log"; }
    }

    public static void Reset(string message)
    {
        try
        {
            if (!Directory.Exists("SMM")) Directory.CreateDirectory("SMM");
        }
        catch { }

        try
        {
            File.WriteAllText(SmmLogPath, message + Environment.NewLine);
        }
        catch { }
    }

    public static void Write(string message)
    {
        int threadId = Thread.CurrentThread.ManagedThreadId;
        string threadTag = threadId == 1 ? string.Empty : string.Format(" [T{0}]", threadId);
        string line = string.Format("[{0:HH:mm:ss.fff}]{1} {2}{3}",
            DateTime.Now,
            threadTag,
            message,
            Environment.NewLine);

        lock (_sync)
        {
            try
            {
                if (!Directory.Exists("SMM")) Directory.CreateDirectory("SMM");
            }
            catch { }

            try { File.AppendAllText(SmmLogPath, line); } catch { }
            try { File.AppendAllText(RootLogPath, line); } catch { }
        }
    }
}

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
                LoaderDebugLog.Reset(string.Format("[Doorstop] Sheltered Mod Manager starting ({0})", arch));
                LoaderDebugLog.Write(string.Format("[Doorstop] Process={0}", System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName));
                LoaderDebugLog.Write(string.Format("[Doorstop] BaseDir={0}", AppDomain.CurrentDomain.BaseDirectory));
                LoaderDebugLog.Write(string.Format("[Doorstop] CurrentDir={0}", Environment.CurrentDirectory));
                Loader.Launch();
            }
            catch (System.Exception ex)
            {
                LoaderDebugLog.Write("[Doorstop] Entrypoint.Start exception: " + ex);
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
            LoaderDebugLog.Write("[Loader] Launch started. Initializing Unity log-hook poller.");
            UnityInitHook.StartLogHookPoller();
        }
        catch (System.Exception ex)
        {
            LoaderDebugLog.Write("[Loader] Fatal startup error during launch initialization: " + ex);
        }
    }
}
#endregion

#region Log-based init (post-engine)
public static class UnityInitHook
{
    private enum LogHookMode
    {
        None,
        EventLogMessageReceived,
        EventLogMessageReceivedThreaded,
        LegacyRegisterLogCallback,
        LegacyRegisterLogCallbackThreaded
    }

    private static Thread _poller;
    private static bool _logHookRegistered = false;
    private static Application.LogCallback _logCallback = OnUnityLog;
    private static readonly object _lock = new object();
    private static string _unityVersion;
    private static int _pollAttempts;
    private static bool _firstLogCallbackSeen;
    private static LogHookMode _logHookMode = LogHookMode.None;
    private static string _lastRegistrationError;
    private static int _sameRegistrationErrorCount;

    public static void StartLogHookPoller()
    {
        if (_poller != null) return;
        LoaderDebugLog.Write("[Loader] Poller thread started (max 120 attempts, 500ms interval).");
        _poller = new Thread(() =>
        {
            for (int i = 0; i < 120 && !Loader.BootstrapTriggered; i++)
            {
                _pollAttempts = i + 1;
                TryRegisterLogHook();
                if ((_pollAttempts % 20) == 0 && !Loader.BootstrapTriggered)
                {
                    LoaderDebugLog.Write(string.Format("[Loader] Waiting for Unity log callback (attempt {0}/120).", _pollAttempts));
                }
                Thread.Sleep(500);
                if (Loader.BootstrapTriggered) break;
            }
            if (!Loader.BootstrapTriggered)
            {
                LoaderDebugLog.Write(string.Format("[Loader] Timeout: Unity log callback was not received after {0} attempts.", _pollAttempts));
            }
        });
        _poller.IsBackground = true;
        _poller.Name = "Doorstop.UnityInitHookPoller";
        _poller.Start();
    }

    private static void TryRegisterLogHook()
    {
        if (Loader.BootstrapTriggered) return;
        lock (_lock)
        {
            if (_logHookRegistered) return;

            Exception eventMainEx;
            if (TryRegisterEventHandler("logMessageReceived", out eventMainEx))
            {
                _logHookRegistered = true;
                _logHookMode = LogHookMode.EventLogMessageReceived;
                _lastRegistrationError = null;
                _sameRegistrationErrorCount = 0;
                LoaderDebugLog.Write(string.Format("[Loader] Registered Unity log callback via Application.logMessageReceived (attempt {0}).", _pollAttempts));
                return;
            }

            Exception eventThreadedEx;
            if (TryRegisterEventHandler("logMessageReceivedThreaded", out eventThreadedEx))
            {
                _logHookRegistered = true;
                _logHookMode = LogHookMode.EventLogMessageReceivedThreaded;
                _lastRegistrationError = null;
                _sameRegistrationErrorCount = 0;
                LoaderDebugLog.Write(string.Format("[Loader] Registered Unity log callback via Application.logMessageReceivedThreaded (attempt {0}).", _pollAttempts));
                return;
            }

            Exception legacyMainEx;
            string legacyMainSignature;
            if (TryRegisterLegacyCallback("RegisterLogCallback", out legacyMainEx, out legacyMainSignature))
            {
                _logHookRegistered = true;
                _logHookMode = LogHookMode.LegacyRegisterLogCallback;
                _lastRegistrationError = null;
                _sameRegistrationErrorCount = 0;
                LoaderDebugLog.Write(string.Format("[Loader] Registered Unity log callback via legacy API {0} (attempt {1}).", legacyMainSignature, _pollAttempts));
                return;
            }

            Exception legacyThreadedEx;
            string legacyThreadedSignature;
            if (TryRegisterLegacyCallback("RegisterLogCallbackThreaded", out legacyThreadedEx, out legacyThreadedSignature))
            {
                _logHookRegistered = true;
                _logHookMode = LogHookMode.LegacyRegisterLogCallbackThreaded;
                _lastRegistrationError = null;
                _sameRegistrationErrorCount = 0;
                LoaderDebugLog.Write(string.Format("[Loader] Registered Unity log callback via legacy API {0} (attempt {1}).", legacyThreadedSignature, _pollAttempts));
                return;
            }

            string combinedError = string.Join(" | ",
                new string[]
                {
                    FormatRegistrationError("logMessageReceived", eventMainEx),
                    FormatRegistrationError("logMessageReceivedThreaded", eventThreadedEx),
                    FormatRegistrationError("RegisterLogCallback", legacyMainEx),
                    FormatRegistrationError("RegisterLogCallbackThreaded", legacyThreadedEx)
                });

            if (_lastRegistrationError == combinedError)
            {
                _sameRegistrationErrorCount++;
            }
            else
            {
                _lastRegistrationError = combinedError;
                _sameRegistrationErrorCount = 1;
            }

            if (_sameRegistrationErrorCount == 1 || (_sameRegistrationErrorCount % 10) == 0)
            {
                LoaderDebugLog.Write(string.Format("[Loader] Unity log callback registration failed ({0}x): {1}", _sameRegistrationErrorCount, combinedError));

                if (_sameRegistrationErrorCount == 1)
                {
                    if (eventMainEx != null) LoaderDebugLog.Write("[Loader] Failure detail [logMessageReceived]: " + eventMainEx);
                    if (eventThreadedEx != null) LoaderDebugLog.Write("[Loader] Failure detail [logMessageReceivedThreaded]: " + eventThreadedEx);
                    if (legacyMainEx != null) LoaderDebugLog.Write("[Loader] Failure detail [RegisterLogCallback]: " + legacyMainEx);
                    if (legacyThreadedEx != null) LoaderDebugLog.Write("[Loader] Failure detail [RegisterLogCallbackThreaded]: " + legacyThreadedEx);
                }
            }
        }
    }

    private static void OnUnityLog(string condition, string stackTrace, LogType type)
    {
        if (!_firstLogCallbackSeen)
        {
            _firstLogCallbackSeen = true;
            string snippet = condition ?? string.Empty;
            if (snippet.Length > 120) snippet = snippet.Substring(0, 120) + "...";
            LoaderDebugLog.Write(string.Format("[Loader] First Unity log callback received. Type={0}, Condition='{1}'", type, snippet.Replace('\n', ' ')));
            LoaderDebugLog.Write(string.Format("[Loader] Active log-hook mode: {0}.", DescribeHookMode(_logHookMode)));
        }

        if (Loader.BootstrapTriggered) return;
        lock (_lock)
        {
            if (Loader.BootstrapTriggered) return;
            Loader.BootstrapTriggered = true;
            _unityVersion = GetUnityVersionSafe();
        }
        UnregisterLogHook();
        try
        {
            if (!string.IsNullOrEmpty(_unityVersion))
                LoaderDebugLog.Write(string.Format("[Doorstop] Unity {0} detected", _unityVersion));
            LoaderDebugLog.Write("[Loader] Triggering Doorstop bootstrap.");
            DoorstopBootstrap.Trigger();
        }
        catch (System.Exception ex)
        {
            LoaderDebugLog.Write("[Loader] Fatal error while processing Unity log callback: " + ex);
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

    private static bool TryRegisterEventHandler(string eventName, out Exception failure)
    {
        failure = null;
        try
        {
            var evt = typeof(Application).GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
            if (evt == null)
            {
                failure = new MissingMemberException("UnityEngine.Application", eventName);
                return false;
            }

            try { evt.RemoveEventHandler(null, _logCallback); } catch { }
            evt.AddEventHandler(null, _logCallback);
            return true;
        }
        catch (Exception ex)
        {
            failure = UnwrapInvocationException(ex);
            return false;
        }
    }

    private static bool TryRegisterLegacyCallback(string methodName, out Exception failure, out string matchedSignature)
    {
        failure = null;
        matchedSignature = null;

        Exception clearEx;
        string clearSignature;
        TryInvokeLegacyCallback(methodName, null, out clearEx, out clearSignature);

        Exception registerEx;
        string registerSignature;
        if (TryInvokeLegacyCallback(methodName, _logCallback, out registerEx, out registerSignature))
        {
            matchedSignature = registerSignature;
            return true;
        }

        matchedSignature = registerSignature ?? clearSignature;
        failure = registerEx ?? clearEx;
        return false;
    }

    private static bool TryInvokeLegacyCallback(string methodName, Application.LogCallback callback, out Exception failure, out string matchedSignature)
    {
        failure = null;
        matchedSignature = null;

        var methods = typeof(Application).GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
        for (int i = 0; i < methods.Length; i++)
        {
            var method = methods[i];
            if (method.Name != methodName) continue;

            var parameters = method.GetParameters();
            if (parameters.Length != 1) continue;
            if (parameters[0].ParameterType != typeof(Application.LogCallback)) continue;

            matchedSignature = DescribeMethod(method);
            try
            {
                method.Invoke(null, new object[] { callback });
                return true;
            }
            catch (Exception ex)
            {
                failure = UnwrapInvocationException(ex);
                return false;
            }
        }

        failure = new MissingMethodException("UnityEngine.Application", methodName);
        return false;
    }

    private static void UnregisterLogHook()
    {
        lock (_lock)
        {
            if (!_logHookRegistered) return;

            try
            {
                switch (_logHookMode)
                {
                    case LogHookMode.EventLogMessageReceived:
                        TryRemoveEventHandler("logMessageReceived");
                        break;
                    case LogHookMode.EventLogMessageReceivedThreaded:
                        TryRemoveEventHandler("logMessageReceivedThreaded");
                        break;
                    case LogHookMode.LegacyRegisterLogCallback:
                    {
                        Exception unregisterEx;
                        string unregisterSignature;
                        TryInvokeLegacyCallback("RegisterLogCallback", null, out unregisterEx, out unregisterSignature);
                        break;
                    }
                    case LogHookMode.LegacyRegisterLogCallbackThreaded:
                    {
                        Exception unregisterEx;
                        string unregisterSignature;
                        TryInvokeLegacyCallback("RegisterLogCallbackThreaded", null, out unregisterEx, out unregisterSignature);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                LoaderDebugLog.Write("[Loader] Failed to unregister Unity log callback: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                _logHookRegistered = false;
                _logHookMode = LogHookMode.None;
            }
        }
    }

    private static void TryRemoveEventHandler(string eventName)
    {
        try
        {
            var evt = typeof(Application).GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
            if (evt != null) evt.RemoveEventHandler(null, _logCallback);
        }
        catch { }
    }

    private static Exception UnwrapInvocationException(Exception ex)
    {
        var tie = ex as TargetInvocationException;
        if (tie != null && tie.InnerException != null) return tie.InnerException;
        return ex;
    }

    private static string DescribeMethod(MethodInfo method)
    {
        var parameters = method.GetParameters();
        string parameterType = parameters.Length == 1 ? parameters[0].ParameterType.Name : "?";
        return string.Format("{0}.{1}({2})", method.DeclaringType.Name, method.Name, parameterType);
    }

    private static string FormatRegistrationError(string source, Exception ex)
    {
        if (ex == null) return source + ": none";
        return string.Format("{0}: {1}: {2}", source, ex.GetType().Name, ex.Message);
    }

    private static string DescribeHookMode(LogHookMode mode)
    {
        switch (mode)
        {
            case LogHookMode.EventLogMessageReceived:
                return "Application.logMessageReceived";
            case LogHookMode.EventLogMessageReceivedThreaded:
                return "Application.logMessageReceivedThreaded";
            case LogHookMode.LegacyRegisterLogCallback:
                return "Application.RegisterLogCallback";
            case LogHookMode.LegacyRegisterLogCallbackThreaded:
                return "Application.RegisterLogCallbackThreaded";
            default:
                return "Unknown";
        }
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
        LoaderDebugLog.Write("[Bootstrap] Trigger called. Creating ModLoaderCoroutineRunner GameObject.");

        try
        {
            var go = new GameObject("ModLoaderCoroutineRunner");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<ModLoaderCoroutineRunner>();
            LoaderDebugLog.Write("[Bootstrap] ModLoaderCoroutineRunner created successfully.");
        }
        catch (System.Exception ex)
        {
            LoaderDebugLog.Write("[Bootstrap] CRITICAL while creating runner: " + ex);
        }
    }
}
#endregion

#region Coroutine Runner
public class ModLoaderCoroutineRunner : MonoBehaviour
{
    private static bool _isInitialized = false;
    private static readonly object _resolveLogLock = new object();
    private static readonly HashSet<string> _resolveMissLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _resolveLoadPathLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> _resolveVersionMismatchLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
        LoaderDebugLog.Write("[Bootstrap] BeginBootstrap invoked. Starting bootstrap coroutine.");

        try
        {
            StartCoroutine(Bootstrap());
        }
        catch (System.Exception ex)
        {
            LoaderDebugLog.Write("[Bootstrap] ERROR starting coroutine: " + ex);
        }
    }

    /// <summary>
    /// The main bootstrap coroutine. Waits for game to be ready then loads ModAPI.
    /// </summary>
    private IEnumerator Bootstrap()
    {
        LoaderDebugLog.Write("[Bootstrap] Coroutine entered. Waiting for Camera.main.");
        float timeout = 15f;
        float waited = 0f;
        while (Camera.main == null)
        {
            timeout -= Time.deltaTime;
            waited += Time.deltaTime;
            if (timeout <= 0f)
            {
                LoaderDebugLog.Write(string.Format("[Bootstrap] ERROR: Camera timeout after {0:0.00}s.", waited));
                yield break;
            }
            yield return null;
        }
        LoaderDebugLog.Write(string.Format("[Bootstrap] Camera.main detected after {0:0.00}s.", waited));

        yield return new WaitForSeconds(0.5f);

        try
        {
            // Get the game root and construct paths
            string gameRoot = System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            string smmPath = System.IO.Path.Combine(gameRoot, "SMM");
            string smmBinPath = System.IO.Path.Combine(smmPath, "bin");
            LoaderDebugLog.Write(string.Format("[Bootstrap] Paths resolved. gameRoot={0}", gameRoot));
            LoaderDebugLog.Write(string.Format("[Bootstrap] Paths resolved. smmPath={0}", smmPath));
            LoaderDebugLog.Write(string.Format("[Bootstrap] Paths resolved. smmBinPath={0}", smmBinPath));

            // Add an AssemblyResolve handler to find assemblies
            System.AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
            {
                var requestedName = new System.Reflection.AssemblyName(args.Name);
                string assemblyName = requestedName.Name;
                
                // Check already-loaded assemblies first (Lenient match for ModAPI)
                var loaded = System.AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => 
                {
                    var aName = a.GetName();
                    // If names match, strict version check unless it's ModAPI/0Harmony where we allow any version
                    if (aName.Name == requestedName.Name)
                    {
                         if (requestedName.Name == "ModAPI" || requestedName.Name == "0Harmony") return true; 
                         return aName.Version == requestedName.Version;
                    }
                    return false;
                });
                if (loaded != null)
                {
                    if (assemblyName == "0Harmony")
                    {
                        var loadedVersion = loaded.GetName().Version;
                        var requestedVersion = requestedName.Version;
                        if (requestedVersion != null && loadedVersion != null && loadedVersion != requestedVersion)
                        {
                            string key = requestedVersion + "->" + loadedVersion;
                            bool shouldLog;
                            lock (_resolveLogLock) shouldLog = _resolveVersionMismatchLogged.Add(key);
                            if (shouldLog)
                            {
                                LoaderDebugLog.Write(string.Format(
                                    "[Bootstrap] Harmony version mismatch tolerated: requested {0}, using loaded {1}.",
                                    requestedVersion, loadedVersion));
                            }
                        }
                    }
                    return loaded;
                }

                string assemblyPath = System.IO.Path.Combine(smmBinPath, assemblyName + ".dll");

                if (System.IO.File.Exists(assemblyPath))
                {
                    bool shouldLog;
                    lock (_resolveLogLock) shouldLog = _resolveLoadPathLogged.Add("bin|" + assemblyName);
                    if (shouldLog)
                    {
                        LoaderDebugLog.Write(string.Format("[Bootstrap] AssemblyResolve loaded from SMM/bin: {0}", assemblyName));
                    }
                    return System.Reflection.Assembly.LoadFrom(assemblyPath);
                }

                // Also check SMM root
                assemblyPath = System.IO.Path.Combine(smmPath, assemblyName + ".dll");
                if (System.IO.File.Exists(assemblyPath))
                {
                     bool shouldLog;
                     lock (_resolveLogLock) shouldLog = _resolveLoadPathLogged.Add("root|" + assemblyName);
                     if (shouldLog)
                     {
                         LoaderDebugLog.Write(string.Format("[Bootstrap] AssemblyResolve loaded from SMM root: {0}", assemblyName));
                     }
                     return System.Reflection.Assembly.LoadFrom(assemblyPath);
                }

                bool shouldLogMiss;
                lock (_resolveLogLock) shouldLogMiss = _resolveMissLogged.Add(assemblyName);
                if (shouldLogMiss)
                {
                    LoaderDebugLog.Write(string.Format("[Bootstrap] AssemblyResolve miss: {0}", args.Name));
                }
                return null;
            };

            string modApiPath = System.IO.Path.Combine(smmPath, "ModAPI.dll");
            LoaderDebugLog.Write(string.Format("[Bootstrap] Looking for ModAPI at: {0}", modApiPath));

            if (!System.IO.File.Exists(modApiPath))
            {
                LoaderDebugLog.Write(string.Format("[Bootstrap] ERROR: ModAPI.dll not found at {0}", modApiPath));
                yield break;
            }

            // Load ModAPI and hand off to PluginManager
            // Use LoadFrom to preserve Location context
            LoaderDebugLog.Write("[Bootstrap] Loading ModAPI assembly.");
            var modApiAsm = System.Reflection.Assembly.LoadFrom(modApiPath);
            LoaderDebugLog.Write(string.Format("[Bootstrap] ModAPI loaded: {0}", modApiAsm.FullName));
            
            var pmType = modApiAsm.GetType("ModAPI.Core.PluginManager");
            if (pmType == null)
            {
                LoaderDebugLog.Write("[Bootstrap] ERROR: ModAPI.Core.PluginManager type not found.");
                yield break;
            }
            var getInstance = pmType.GetMethod("getInstance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (getInstance == null)
            {
                LoaderDebugLog.Write("[Bootstrap] ERROR: getInstance method not found.");
                yield break;
            }
            var pm = getInstance.Invoke(null, null);
            var loadMethod = pmType.GetMethod("loadAssemblies");
            if (loadMethod == null)
            {
                LoaderDebugLog.Write("[Bootstrap] ERROR: loadAssemblies method not found.");
                yield break;
            }
            LoaderDebugLog.Write("[Bootstrap] Invoking PluginManager.loadAssemblies.");
            loadMethod.Invoke(pm, new object[] { this.gameObject });
            
            LoaderDebugLog.Write("[Doorstop] Handoff to ModAPI complete");
        }
        catch (System.Exception ex)
        {
            LoaderDebugLog.Write("[Bootstrap] ERROR: " + ex);
        }
    }
}
#endregion
