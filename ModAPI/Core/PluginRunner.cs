using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Hosts plugin lifecycle, update ticks, and scene events with 5.3/5.6 compatibility.
    /// </summary>
    public class PluginRunner : MonoBehaviour
    {
        public static bool IsModernUnity { get; private set; }
        public static bool IsQuitting { get; set; }
        public static PluginRunner Instance { get; private set; }

        private readonly Queue<Action> _nextFrame = new Queue<Action>();
        public PluginManager Manager;
        private bool _useModernApi = false;
        private string _currentSceneName;

        public event Action<string> SceneLoaded;
        public event Action<string> SceneUnloaded;

        private object _sceneLoadedDelegate;
        private object _sceneUnloadedDelegate;
        private bool _unityLogBridgeHooked;
        private float _nextQuitHeartbeatAt;

        public void Enqueue(Action action)
        {
            lock (_nextFrame)
            {
                _nextFrame.Enqueue(action);
            }
        }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            IsQuitting = false;
            ModAPI.Hooks.PlatformSaveProxy.ResetStatus();
            HookUnityLogBridge();
            _useModernApi = TryHookModernSceneEvents();
            IsModernUnity = _useModernApi;
            if (!_useModernApi)
            {
                ThrowLegacyFallback();
            }
        }

        private void OnApplicationQuit()
        {
            IsQuitting = true;
            CrashCorridorTracer.Mark("OnApplicationQuit", "Unity is quitting");
            MMLog.WriteInfo("Application is quitting detected. Shutting down plugins...");
            if (Manager != null) Manager.ShutdownAll();
            MMLog.Flush();
        }

        private void OnDestroy()
        {
            UnhookUnityLogBridge();
            if (_useModernApi && _sceneLoadedDelegate != null)
            {
                try
                {
                    var sceneManagerType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                    var sceneLoadedEvent = sceneManagerType.GetEvent("sceneLoaded");
                    sceneLoadedEvent.GetRemoveMethod().Invoke(null, new object[] { _sceneLoadedDelegate });

                    if (_sceneUnloadedDelegate != null)
                    {
                        var sceneUnloadedEvent = sceneManagerType.GetEvent("sceneUnloaded");
                        sceneUnloadedEvent.GetRemoveMethod().Invoke(null, new object[] { _sceneUnloadedDelegate });
                    }
                }
                catch (Exception ex) { MMLog.WarnOnce("PluginRunner.OnDestroy", "Error unsubscribing from scene events: " + ex.Message); }
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
            catch { }
        }

        private static void OnUnityLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            try
            {
                var msg = condition ?? string.Empty;
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
            catch { }
        }

        private void OnSceneLoadedModern(object scene, object mode)
        {
            try
            {
                if (Manager == null) return;

                var sceneName = TryGetSceneName(scene);
                if (string.IsNullOrEmpty(sceneName))
                {
                    MMLog.WarnOnce("PluginRunner.OnSceneLoadedModern.SceneName", "Received loaded-scene callback with unresolved scene name.");
                    return;
                }

                Manager.OnSceneLoaded(sceneName);
                SceneLoaded?.Invoke(sceneName);
                if (IsQuitting)
                {
                    CrashCorridorTracer.Mark("OnSceneLoadedModern", sceneName);
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PluginRunner.OnSceneLoadedModern.Error", "OnSceneLoadedModern failed: " + ex.Message);
                if (IsQuitting)
                {
                    CrashCorridorTracer.Mark("OnSceneLoadedModern.Exception", ex.GetType().Name + ": " + ex.Message);
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
                    MMLog.WarnOnce("PluginRunner.OnSceneUnloadedModern.SceneName", "Received unloaded-scene callback with unresolved scene name.");
                    return;
                }

                Manager.OnSceneUnloaded(sceneName);
                SceneUnloaded?.Invoke(sceneName);
                if (IsQuitting)
                {
                    CrashCorridorTracer.Mark("OnSceneUnloadedModern", sceneName);
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PluginRunner.OnSceneUnloadedModern.Error", "OnSceneUnloadedModern failed: " + ex.Message);
                if (IsQuitting)
                {
                    CrashCorridorTracer.Mark("OnSceneUnloadedModern.Exception", ex.GetType().Name + ": " + ex.Message);
                }
            }
        }

        private static string TryGetSceneName(object scene)
        {
            if (scene == null) return string.Empty;
            try
            {
                var t = scene.GetType();
                var nameProp = t.GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProp == null) return string.Empty;
                var value = nameProp.GetValue(scene, null);
                return value as string ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        void OnLevelWasLoaded(int level)
        {
            if (!_useModernApi)
            {
                var newSceneName = Application.loadedLevelName;
                if (Manager != null && _currentSceneName != newSceneName)
                {
                    if (!string.IsNullOrEmpty(_currentSceneName))
                    {
                        Manager.OnSceneUnloaded(_currentSceneName);
                        SceneUnloaded?.Invoke(_currentSceneName);
                    }
                    Manager.OnSceneLoaded(newSceneName);
                    SceneLoaded?.Invoke(newSceneName);
                    _currentSceneName = newSceneName;
                }
            }
        }

        private void Update()
        {
            if (IsQuitting && Time.realtimeSinceStartup >= _nextQuitHeartbeatAt)
            {
                _nextQuitHeartbeatAt = Time.realtimeSinceStartup + 0.5f;
                string detail = string.Empty;
                try
                {
                    var sm = SaveManager.instance;
                    if (sm != null)
                    {
                        detail = "isSaving=" + sm.isSaving + ", isLoading=" + sm.isLoading;
                    }
                    else
                    {
                        detail = "SaveManager.instance=null";
                    }
                }
                catch (Exception ex)
                {
                    detail = "SaveManager read failed: " + ex.Message;
                }
                CrashCorridorTracer.Mark("QuittingHeartbeat", detail);
            }

            lock (_nextFrame)
            {
                while (_nextFrame.Count > 0)
                {
                    var a = _nextFrame.Dequeue();
                    try { a(); } catch (Exception ex) { MMLog.Write($"next-frame action failed: {ex.Message}"); }
                }
            }
            if (Manager != null) Manager.OnUnityUpdate();
        }

        private bool TryHookModernSceneEvents()
        {
            try
            {
                if (!RuntimeCompat.IsModernSceneApi)
                {
                    MMLog.WriteDebug("SceneManager modern API not detected (Unity 5.3?).");
                    return false;
                }

                var sceneManagerType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                if (sceneManagerType == null)
                {
                    MMLog.WriteDebug("SceneManager type not found.");
                    return false;
                }

                var sceneLoadedEvent = sceneManagerType.GetEvent("sceneLoaded");
                if (sceneLoadedEvent == null)
                {
                    MMLog.WriteError("SceneManager.sceneLoaded event not found.");
                    return false;
                }

                var sceneUnloadedEvent = sceneManagerType.GetEvent("sceneUnloaded");
                var onLoadedMethod = GetType().GetMethod("OnSceneLoadedModern", BindingFlags.NonPublic | BindingFlags.Instance);
                var onUnloadedMethod = GetType().GetMethod("OnSceneUnloadedModern", BindingFlags.NonPublic | BindingFlags.Instance);
                if (onLoadedMethod == null)
                {
                    MMLog.WriteError("OnSceneLoadedModern method missing.");
                    return false;
                }
                if (onUnloadedMethod == null)
                {
                    MMLog.WriteError("OnSceneUnloadedModern method missing.");
                    return false;
                }

                _sceneLoadedDelegate = Delegate.CreateDelegate(sceneLoadedEvent.EventHandlerType, this, onLoadedMethod);
                sceneLoadedEvent.GetAddMethod().Invoke(null, new object[] { _sceneLoadedDelegate });

                if (sceneUnloadedEvent != null)
                {
                    _sceneUnloadedDelegate = Delegate.CreateDelegate(sceneUnloadedEvent.EventHandlerType, this, onUnloadedMethod);
                    sceneUnloadedEvent.GetAddMethod().Invoke(null, new object[] { _sceneUnloadedDelegate });
                }

                IsModernUnity = true;
                MMLog.WriteDebug("Modern scene events hooked successfully.");

                try
                {
                    var activeSceneProp = sceneManagerType.GetProperty("activeScene");
                    var activeScene = activeSceneProp != null ? activeSceneProp.GetValue(null, null) : null;
                    var isLoadedProp = activeScene != null ? activeScene.GetType().GetProperty("isLoaded") : null;
                    if (activeScene != null && isLoadedProp != null && (bool)isLoadedProp.GetValue(activeScene, null))
                    {
                        OnSceneLoadedModern(activeScene, null);
                    }
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

        private void ThrowLegacyFallback()
        {
            if (_useModernApi) return;
            IsModernUnity = false;
            MMLog.Write("Modern SceneManager not found. Using legacy OnLevelWasLoaded (Unity 5.3).");

            _currentSceneName = Application.loadedLevelName;
            if (Manager != null && !string.IsNullOrEmpty(_currentSceneName))
            {
                Manager.OnSceneLoaded(_currentSceneName);
                SceneLoaded?.Invoke(_currentSceneName);
            }
        }
    }
}
