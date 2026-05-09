using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Hosts plugin lifecycle, update ticks, and scene events with 5.3/5.6 compatibility.
    /// </summary>
    internal class PluginRunner : MonoBehaviour
    {
        /// <summary>
        /// True when modern SceneManager event APIs are active.
        /// </summary>
        public static bool IsModernUnity { get; private set; }
        /// <summary>
        /// True during quit flow so systems can avoid risky late-stage work.
        /// </summary>
        public static bool IsQuitting { get; set; }
        /// <summary>
        /// Singleton runtime host attached to the persistent loader root.
        /// </summary>
        public static PluginRunner Instance { get; private set; }
        /// <summary>
        /// Unity main thread managed id captured at runner startup.
        /// </summary>
        public static int MainThreadId { get; private set; }
        /// <summary>
        /// True when called from the Unity main thread.
        /// </summary>
        public static bool IsMainThread
        {
            get { return Thread.CurrentThread.ManagedThreadId == MainThreadId; }
        }

        private readonly Queue<Action> _nextFrame = new Queue<Action>();
        public PluginManager Manager;
        private bool _useModernApi = false;
        private string _currentSceneName;

        /// <summary>Raised after a scene was reported loaded to the plugin manager.</summary>
        public event Action<string> SceneLoaded;
        /// <summary>Raised after a scene was reported unloaded to the plugin manager.</summary>
        public event Action<string> SceneUnloaded;

        private object _sceneLoadedDelegate;
        private object _sceneUnloadedDelegate;
        private bool _unityLogBridgeHooked;
        private float _nextQuitHeartbeatAt;

        /// <summary>
        /// Queues main-thread work for execution in the next <see cref="Update"/> tick.
        /// </summary>
        public void Enqueue(Action action)
        {
            lock (_nextFrame)
            {
                _nextFrame.Enqueue(action);
            }
        }

        /// <summary>
        /// Bootstraps runtime event hooks and determines scene API mode.
        /// </summary>
        private void Awake()
        {
            if (Instance == null) Instance = this;
            MainThreadId = Thread.CurrentThread.ManagedThreadId;
            IsQuitting = false;
            SaveRuntimeAdapters.ResetRuntimeState();
            ModThreads.FlushPendingMainThreadCallbacks();
            HookUnityLogBridge();
            _useModernApi = TryHookModernSceneEvents();
            IsModernUnity = _useModernApi;
            if (!_useModernApi)
            {
                ThrowLegacyFallback();
            }
        }

        /// <summary>
        /// Last managed quit boundary. Marks quitting and runs orderly plugin shutdown.
        /// </summary>
        private void OnApplicationQuit()
        {
            IsQuitting = true;
            SaveExitTracker.Mark("OnApplicationQuit", "Unity is quitting");
            MMLog.WriteInfo("Application is quitting detected. Shutting down plugins...");
            if (Manager != null) Manager.ShutdownAll();
            UnityLogFilter.LogSuppressionSummary("application shutdown");
            MMLog.Flush();
        }

        /// <summary>
        /// Cleans up event hooks to prevent duplicate handlers on domain reload/teardown.
        /// </summary>
        private void OnDestroy()
        {
            UnityLogFilter.LogSuppressionSummary("runner teardown");
            UnhookUnityLogBridge();
            if (_useModernApi && _sceneLoadedDelegate != null)
            {
                RuntimeCompat.TryRemoveSceneLoadedHandler(_sceneLoadedDelegate);
                _sceneLoadedDelegate = null;

                if (_sceneUnloadedDelegate != null)
                {
                    RuntimeCompat.TryRemoveSceneUnloadedHandler(_sceneUnloadedDelegate);
                    _sceneUnloadedDelegate = null;
                }
            }
        }

        private void HookUnityLogBridge()
        {
            if (_unityLogBridgeHooked) return;
            try
            {
                Application.logMessageReceivedThreaded += OnUnityLogMessageReceived;
                AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
                _unityLogBridgeHooked = true;
                MMLog.WriteDebug("Unity log bridge hooked (Player.log mirrored to SMM log).");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PluginRunner.HookUnityLogBridge", "Failed to hook Unity log bridge: " + ex.Message);
            }
        }

        private void UnhookUnityLogBridge()
        {
            if (!_unityLogBridgeHooked) return;
            try
            {
                Application.logMessageReceivedThreaded -= OnUnityLogMessageReceived;
                AppDomain.CurrentDomain.UnhandledException -= OnUnhandledException;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PluginRunner.UnhookUnityLogBridge", "Failed to unhook Unity log bridge: " + ex.Message);
            }
            finally
            {
                _unityLogBridgeHooked = false;
            }
        }

        private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            try
            {
                var ex = e != null ? e.ExceptionObject as Exception : null;
                if (ex != null)
                {
                    MMLog.WriteWithSource(
                        MMLog.LogLevel.Fatal,
                        MMLog.LogCategory.General,
                        "UnityUnhandled",
                        ex.ToString());
                }
                else
                {
                    MMLog.WriteWithSource(
                        MMLog.LogLevel.Fatal,
                        MMLog.LogCategory.General,
                        "UnityUnhandled",
                        "Unhandled exception (non-Exception object).");
                }
            }
            catch
            {
                // GuardrailAllow: SilentCatch - unhandled-exception logging has no safer fallback sink.
            }
        }

        private static void OnUnityLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            try
            {
                var msg = condition ?? string.Empty;
                UnityLogNormalization normalization;

                if (UnityLogNormalizationRegistry.TryNormalize(msg, stackTrace, type, out normalization))
                {
                    if (normalization.Suppress)
                    {
                        if (UnityLogFilter.ShouldSuppressNormalized(msg, type, normalization))
                            return;
                    }
                    else
                    {
                        if (string.IsNullOrEmpty(normalization.OnceKey))
                        {
                            MMLog.WriteWithSource(
                                normalization.Level,
                                MMLog.LogCategory.General,
                                normalization.Source,
                                normalization.Message);
                        }
                        else
                        {
                            MMLog.LogOnce(normalization.OnceKey, delegate
                            {
                                MMLog.WriteWithSource(
                                    normalization.Level,
                                    MMLog.LogCategory.General,
                                    normalization.Source,
                                    normalization.Message);
                            });
                        }

                        return;
                    }
                }

                if (UnityLogFilter.ShouldSuppress(msg, type))
                {
                    return;
                }

                if (type == LogType.Exception && !string.IsNullOrEmpty(stackTrace))
                {
                    msg = msg + "\n" + stackTrace;
                }

                if (type == LogType.Error || type == LogType.Assert || type == LogType.Exception)
                {
                    MMLog.WriteWithSource(MMLog.LogLevel.Error, MMLog.LogCategory.General, "UnityLog", msg);
                }
                else if (type == LogType.Warning)
                {
                    MMLog.WriteWithSource(MMLog.LogLevel.Warning, MMLog.LogCategory.General, "UnityLog", msg);
                }
            }
            catch
            {
                // GuardrailAllow: SilentCatch - Unity log mirroring is best-effort and must not recurse on logger failures.
            }
        }

        private void OnSceneLoadedModern(object scene, object mode)
        {
            try
            {
                if (Manager == null) return;

                var sceneName = TryGetSceneName(scene);
                if (string.IsNullOrEmpty(sceneName))
                {
                    MMLog.WriteDebug("Received loaded-scene callback with unresolved scene name.");
                    return;
                }

                NotifySceneLoaded(sceneName, "OnSceneLoadedModern");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PluginRunner.OnSceneLoadedModern.Error", "OnSceneLoadedModern failed: " + ex.Message);
                if (IsQuitting)
                {
                    SaveExitTracker.Mark("OnSceneLoadedModern.Exception", ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private void OnSceneUnloadedModern(object scene)
        {
            try
            {
                if (Manager == null) return;

                var sceneName = TryGetSceneName(scene);
                if (string.IsNullOrEmpty(sceneName))
                {
                    MMLog.WriteDebug("Received unloaded-scene callback with unresolved scene name.");
                    return;
                }

                NotifySceneUnloaded(sceneName, "OnSceneUnloadedModern");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PluginRunner.OnSceneUnloadedModern.Error", "OnSceneUnloadedModern failed: " + ex.Message);
                if (IsQuitting)
                {
                    SaveExitTracker.Mark("OnSceneUnloadedModern.Exception", ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private static string TryGetSceneName(object scene)
        {
            string sceneName;
            return RuntimeCompat.TryGetSceneName(scene, out sceneName) ? sceneName : string.Empty;
        }

        private void NotifySceneLoaded(string sceneName, string source)
        {
            if (Manager == null) return;

            Manager.OnSceneLoaded(sceneName);
            SceneLoaded?.Invoke(sceneName);
            if (IsQuitting)
            {
                SaveExitTracker.Mark(source, sceneName);
            }
        }

        private void NotifySceneUnloaded(string sceneName, string source)
        {
            if (Manager == null) return;

            Manager.OnSceneUnloaded(sceneName);
            SceneUnloaded?.Invoke(sceneName);
            if (IsQuitting)
            {
                SaveExitTracker.Mark(source, sceneName);
            }
        }

        void OnLevelWasLoaded(int level)
        {
            if (!_useModernApi)
            {
                string newSceneName;
                if (!RuntimeCompat.TryGetActiveSceneName(out newSceneName))
                    newSceneName = string.Empty;

                if (Manager != null && _currentSceneName != newSceneName)
                {
                    if (!string.IsNullOrEmpty(_currentSceneName))
                    {
                        NotifySceneUnloaded(_currentSceneName, "OnLevelWasLoaded.LegacyUnload");
                    }
                    NotifySceneLoaded(newSceneName, "OnLevelWasLoaded.LegacyLoad");
                    _currentSceneName = newSceneName;
                }
            }
        }

        /// <summary>
        /// Drains next-frame queue, emits quit heartbeat diagnostics, and forwards update ticks.
        /// </summary>
        private void Update()
        {
            ModThreads.FlushPendingMainThreadCallbacks();

            if (IsQuitting && Time.realtimeSinceStartup >= _nextQuitHeartbeatAt)
            {
                _nextQuitHeartbeatAt = Time.realtimeSinceStartup + 0.5f;
                string detail = SaveRuntimeAdapters.GetQuitHeartbeatDetail();
                SaveExitTracker.Mark("QuittingHeartbeat", detail);
            }

            Action[] actions = null;
            lock (_nextFrame)
            {
                if (_nextFrame.Count > 0)
                {
                    actions = _nextFrame.ToArray();
                    _nextFrame.Clear();
                }
            }

            if (actions != null)
            {
                for (int i = 0; i < actions.Length; i++)
                {
                    try { actions[i](); }
                    catch (Exception ex) { MMLog.Write($"next-frame action failed: {ex.Message}"); }
                }
            }
            if (Manager != null) Manager.OnUnityUpdate();
        }

        /// <summary>
        /// Attempts to bind runtime scene events through reflection for 5.6+/modern API variants.
        /// </summary>
        private bool TryHookModernSceneEvents()
        {
            try
            {
                if (!RuntimeCompat.IsModernSceneApi)
                {
                    MMLog.WriteDebug("SceneManager modern API not detected (Unity 5.3?).");
                    return false;
                }

                if (!RuntimeCompat.TryAddSceneLoadedHandler(this, "OnSceneLoadedModern", out _sceneLoadedDelegate))
                {
                    MMLog.WriteError("SceneManager.sceneLoaded event not found or unavailable.");
                    return false;
                }

                RuntimeCompat.TryAddSceneUnloadedHandler(this, "OnSceneUnloadedModern", out _sceneUnloadedDelegate);

                IsModernUnity = true;
                MMLog.WriteDebug("Modern scene events hooked successfully.");

                try
                {
                    string activeSceneName;
                    if (RuntimeCompat.TryGetActiveSceneName(out activeSceneName))
                        NotifySceneLoaded(activeSceneName, "PluginRunner.ActiveScene");
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("PluginRunner.ActiveScene", "Failed to read activeScene: " + ex.Message);
                }

                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Failed to hook modern scene events: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// Switches to legacy level callbacks when modern scene APIs are unavailable.
        /// </summary>
        private void ThrowLegacyFallback()
        {
            if (_useModernApi) return;
            IsModernUnity = false;
            MMLog.Write("Modern SceneManager not found. Using legacy OnLevelWasLoaded (Unity 5.3).");

            if (!RuntimeCompat.TryGetActiveSceneName(out _currentSceneName))
                _currentSceneName = string.Empty;

            if (Manager != null && !string.IsNullOrEmpty(_currentSceneName))
            {
                NotifySceneLoaded(_currentSceneName, "PluginRunner.LegacyInitialScene");
            }
        }
    }
}
