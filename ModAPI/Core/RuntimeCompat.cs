using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Compatibility helpers for supporting both Unity 5.3 (x86) and 5.6 (x64).
    /// Keep version-conditional logic here so mods can call one API surface.
    /// </summary>
    public static class RuntimeCompat
    {
        private static string _unityVersion;
        private static string _gameVersion;
        private static string _unityDataPath;
        private static string _gameRoot;
        private static bool? _isModernSceneApi;

        public static string UnityVersion
        {
            get
            {
                if (!string.IsNullOrEmpty(_unityVersion)) return _unityVersion;
                _unityVersion = RuntimeEnvironmentInfo.UnityVersion;
                return _unityVersion;
            }
        }

        public static string GameVersion
        {
            get
            {
                if (!string.IsNullOrEmpty(_gameVersion)) return _gameVersion;
                _gameVersion = RuntimeEnvironmentInfo.GameVersion;
                return _gameVersion;
            }
        }

        public static string ModApiVersion
        {
            get
            {
                var version = typeof(RuntimeCompat).Assembly.GetName().Version;
                return string.Format("{0}.{1}.{2}", version.Major, version.Minor, version.Build);
            }
        }

        public static string Architecture
        {
            get
            {
                return IntPtr.Size == 8 ? "x64" : "x86";
            }
        }

        public static string UnityDataPath
        {
            get
            {
                if (!string.IsNullOrEmpty(_unityDataPath)) return _unityDataPath;
                _unityDataPath = RuntimeEnvironmentInfo.UnityDataPath;
                return _unityDataPath;
            }
        }

        public static string GameRoot
        {
            get
            {
                if (!string.IsNullOrEmpty(_gameRoot)) return _gameRoot;

                string dataPath = UnityDataPath;
                if (!string.IsNullOrEmpty(dataPath)
                    && !string.Equals(dataPath, RuntimeEnvironmentInfo.UnavailableValue, StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        DirectoryInfo parent = Directory.GetParent(dataPath);
                        if (parent != null)
                        {
                            _gameRoot = parent.FullName;
                            return _gameRoot;
                        }
                    }
                    catch
                    {
                        // GuardrailAllow: SilentCatch - Unity dataPath may be unavailable during early loader probes; callers get a current-directory fallback.
                    }
                }

                _gameRoot = Directory.GetCurrentDirectory();
                return _gameRoot;
            }
        }

        public static Rect ZeroRect()
        {
            return new Rect(0f, 0f, 0f, 0f);
        }

        /// <summary>
        /// True when modern SceneManager events exist (Unity 5.4+); false on 5.3.
        /// </summary>
        public static bool IsModernSceneApi
        {
            get
            {
                if (_isModernSceneApi.HasValue) return _isModernSceneApi.Value;
                EventInfo evt;
                _isModernSceneApi = TryGetSceneEvent("sceneLoaded", out evt)
                    && evt.EventHandlerType != null
                    && evt.GetAddMethod() != null
                    && evt.GetRemoveMethod() != null;
                return _isModernSceneApi.Value;
            }
        }

        /// <summary>
        /// Attaches a modern SceneManager.sceneLoaded handler through reflection.
        /// The target method should accept (object scene, object mode).
        /// </summary>
        public static bool TryAddSceneLoadedHandler(object target, string methodName, out object handler)
        {
            return TryAddSceneEventHandler("sceneLoaded", target, methodName, out handler);
        }

        /// <summary>
        /// Detaches a handler returned by <see cref="TryAddSceneLoadedHandler"/>.
        /// </summary>
        public static bool TryRemoveSceneLoadedHandler(object handler)
        {
            return TryRemoveSceneEventHandler("sceneLoaded", handler);
        }

        /// <summary>
        /// Attaches a modern SceneManager.sceneUnloaded handler through reflection.
        /// The target method should accept (object scene).
        /// </summary>
        public static bool TryAddSceneUnloadedHandler(object target, string methodName, out object handler)
        {
            return TryAddSceneEventHandler("sceneUnloaded", target, methodName, out handler);
        }

        /// <summary>
        /// Detaches a handler returned by <see cref="TryAddSceneUnloadedHandler"/>.
        /// </summary>
        public static bool TryRemoveSceneUnloadedHandler(object handler)
        {
            return TryRemoveSceneEventHandler("sceneUnloaded", handler);
        }

        /// <summary>
        /// Reads a Unity Scene-like object's name without statically binding to Scene.
        /// </summary>
        public static bool TryGetSceneName(object scene, out string sceneName)
        {
            sceneName = string.Empty;
            if (scene == null)
                return false;

            try
            {
                PropertyInfo nameProperty = scene.GetType().GetProperty("name", BindingFlags.Public | BindingFlags.Instance);
                if (nameProperty == null)
                    return false;

                object value = nameProperty.GetValue(scene, null);
                sceneName = value as string ?? string.Empty;
                return !string.IsNullOrEmpty(sceneName);
            }
            catch
            {
                sceneName = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// Reads the active scene name using modern SceneManager reflection or the legacy Application fallback.
        /// </summary>
        public static bool TryGetActiveSceneName(out string sceneName)
        {
            sceneName = string.Empty;

            object activeScene;
            if (TryGetActiveScene(out activeScene) && TryGetSceneName(activeScene, out sceneName))
                return true;

            if (TryGetLegacyLoadedLevelName(out sceneName))
                return true;

            return false;
        }

        /// <summary>
        /// Reads the active scene root objects through SceneManager reflection when available.
        /// </summary>
        public static bool TryGetActiveSceneRootGameObjects(out GameObject[] roots)
        {
            roots = new GameObject[0];

            try
            {
                object activeScene;
                if (!TryGetActiveScene(out activeScene) || activeScene == null)
                    return false;

                MethodInfo getRootGameObjects = activeScene.GetType().GetMethod(
                    "GetRootGameObjects",
                    BindingFlags.Public | BindingFlags.Instance,
                    null,
                    Type.EmptyTypes,
                    null);
                if (getRootGameObjects == null)
                    return false;

                object value = getRootGameObjects.Invoke(activeScene, null);
                GameObject[] directRoots = value as GameObject[];
                if (directRoots != null)
                {
                    roots = directRoots;
                    return true;
                }

                Array array = value as Array;
                if (array == null)
                    return false;

                GameObject[] converted = new GameObject[array.Length];
                for (int i = 0; i < array.Length; i++)
                    converted[i] = array.GetValue(i) as GameObject;

                roots = converted;
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("RuntimeCompat.ActiveScene.Roots", "Failed to read active SceneManager roots: " + DescribeException(ex));
                roots = new GameObject[0];
                return false;
            }
        }

        /// <summary>
        /// Loads a scene using modern SceneManager reflection or the legacy Application fallback.
        /// </summary>
        public static bool TryLoadScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName))
                return false;

            try
            {
                Type sceneManagerType = GetSceneManagerType();
                MethodInfo loadScene = sceneManagerType != null
                    ? sceneManagerType.GetMethod(
                        "LoadScene",
                        BindingFlags.Public | BindingFlags.Static,
                        null,
                        new[] { typeof(string) },
                        null)
                    : null;

                if (loadScene != null)
                {
                    loadScene.Invoke(null, new object[] { sceneName });
                    return true;
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("RuntimeCompat.LoadScene.Modern", "Failed to load scene through SceneManager: " + DescribeException(ex));
            }

            return TryLoadLegacyLevel(sceneName);
        }

        /// <summary>
        /// Seeds Unity's global random generator using InitState where available or the legacy seed member otherwise.
        /// </summary>
        public static bool TrySetUnityRandomSeed(int seed)
        {
            try
            {
                Type randomType = Type.GetType("UnityEngine.Random, UnityEngine");
                if (randomType == null)
                {
                    MMLog.WarnOnce("RuntimeCompat.UnityRandomSeed.TypeMissing",
                        "UnityEngine.Random was not available; continuing without global Unity RNG seeding.");
                    return false;
                }

                MethodInfo initState = randomType.GetMethod(
                    "InitState",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(int) },
                    null);
                if (initState != null)
                {
                    initState.Invoke(null, new object[] { seed });
                    return true;
                }

                PropertyInfo seedProperty = randomType.GetProperty("seed", BindingFlags.Public | BindingFlags.Static);
                if (seedProperty != null && seedProperty.CanWrite)
                {
                    seedProperty.SetValue(null, seed, null);
                    return true;
                }

                FieldInfo seedField = randomType.GetField("seed", BindingFlags.Public | BindingFlags.Static);
                if (seedField != null)
                {
                    seedField.SetValue(null, seed);
                    return true;
                }

                MMLog.WarnOnce("RuntimeCompat.UnityRandomSeed.Unavailable",
                    "Unity random seed API was not found; continuing without global Unity RNG seeding.");
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("RuntimeCompat.UnityRandomSeed.Failed",
                    "Failed to set Unity random seed: " + DescribeException(ex));
                return false;
            }
        }

        private static bool TryGetLegacyLoadedLevelName(out string sceneName)
        {
            sceneName = string.Empty;

            try
            {
                PropertyInfo loadedLevelName = typeof(Application).GetProperty(
                    "loadedLevelName",
                    BindingFlags.Public | BindingFlags.Static);
                if (loadedLevelName == null)
                    return false;

                object value = loadedLevelName.GetValue(null, null);
                sceneName = value as string ?? string.Empty;
                return !string.IsNullOrEmpty(sceneName);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("RuntimeCompat.ActiveScene.Legacy", "Failed to read Application.loadedLevelName: " + DescribeException(ex));
                sceneName = string.Empty;
                return false;
            }
        }

        private static bool TryLoadLegacyLevel(string sceneName)
        {
            try
            {
                MethodInfo loadLevel = typeof(Application).GetMethod(
                    "LoadLevel",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new[] { typeof(string) },
                    null);
                if (loadLevel == null)
                    return false;

                loadLevel.Invoke(null, new object[] { sceneName });
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("RuntimeCompat.LoadScene.Legacy", "Failed to load scene through Application.LoadLevel: " + DescribeException(ex));
                return false;
            }
        }

        private static bool TryAddSceneEventHandler(string eventName, object target, string methodName, out object handler)
        {
            handler = null;
            if (target == null || string.IsNullOrEmpty(methodName))
                return false;

            try
            {
                EventInfo sceneEvent;
                if (!TryGetSceneEvent(eventName, out sceneEvent))
                    return false;

                MethodInfo addMethod = sceneEvent.GetAddMethod();
                if (sceneEvent.EventHandlerType == null || addMethod == null)
                    return false;

                MethodInfo callback = target.GetType().GetMethod(
                    methodName,
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                if (callback == null)
                    return false;

                Delegate created = Delegate.CreateDelegate(sceneEvent.EventHandlerType, target, callback);
                addMethod.Invoke(null, new object[] { created });
                handler = created;
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("RuntimeCompat.SceneEvent." + eventName + ".Add",
                    "Failed to attach SceneManager." + eventName + ": " + DescribeException(ex));
                handler = null;
                return false;
            }
        }

        private static bool TryRemoveSceneEventHandler(string eventName, object handler)
        {
            if (handler == null)
                return true;

            try
            {
                EventInfo sceneEvent;
                if (!TryGetSceneEvent(eventName, out sceneEvent))
                    return false;

                MethodInfo removeMethod = sceneEvent.GetRemoveMethod();
                if (removeMethod == null)
                    return false;

                removeMethod.Invoke(null, new[] { handler });
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("RuntimeCompat.SceneEvent." + eventName + ".Remove",
                    "Failed to detach SceneManager." + eventName + ": " + DescribeException(ex));
                return false;
            }
        }

        private static bool TryGetSceneEvent(string eventName, out EventInfo sceneEvent)
        {
            sceneEvent = null;
            Type sceneManagerType = GetSceneManagerType();
            if (sceneManagerType == null || string.IsNullOrEmpty(eventName))
                return false;

            sceneEvent = sceneManagerType.GetEvent(eventName, BindingFlags.Public | BindingFlags.Static);
            return sceneEvent != null;
        }

        private static Type GetSceneManagerType()
        {
            try
            {
                return Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
            }
            catch
            {
                return null;
            }
        }

        private static bool TryGetActiveScene(out object activeScene)
        {
            activeScene = null;

            try
            {
                Type sceneManagerType = GetSceneManagerType();
                if (sceneManagerType == null)
                    return false;

                MethodInfo getActiveScene = sceneManagerType.GetMethod(
                    "GetActiveScene",
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    Type.EmptyTypes,
                    null);
                if (getActiveScene != null)
                {
                    activeScene = getActiveScene.Invoke(null, null);
                    return activeScene != null;
                }

                PropertyInfo activeSceneProperty = sceneManagerType.GetProperty("activeScene", BindingFlags.Public | BindingFlags.Static);
                if (activeSceneProperty != null)
                {
                    activeScene = activeSceneProperty.GetValue(null, null);
                    return activeScene != null;
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("RuntimeCompat.ActiveScene.Modern", "Failed to read active SceneManager scene: " + DescribeException(ex));
            }

            return false;
        }

        private static string DescribeException(Exception exception)
        {
            TargetInvocationException invocation = exception as TargetInvocationException;
            if (invocation != null && invocation.InnerException != null)
                exception = invocation.InnerException;

            return exception.GetType().Name + ": " + exception.Message;
        }
    }
}
