using System;
using UnityEngine;

namespace ModAPI.Util
{
    /// <summary>
    /// Unified scene event helper that works on both Unity 5.3 (legacy) and 5.4+ (modern).
    /// Call once per mod to receive scene load/unload callbacks without branching on engine version.
    /// </summary>
    public static class SceneCompat
    {
        /// <summary>
        /// Registers scene load/unload callbacks on the provided GameObject.
        /// Handles legacy OnLevelWasLoaded for 5.3 and SceneManager events for 5.4+.
        /// </summary>
        public static void Register(GameObject host, Action<string> onLoaded, Action<string> onUnloaded = null)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            var bridge = host.GetComponent<SceneCompatBridge>() ?? host.AddComponent<SceneCompatBridge>();
            bridge.Init(onLoaded, onUnloaded);
        }

        private class SceneCompatBridge : MonoBehaviour
        {
            private Action<string> _onLoaded;
            private Action<string> _onUnloaded;
            private bool _modern;
            private string _currentScene;

            public void Init(Action<string> onLoaded, Action<string> onUnloaded)
            {
                _onLoaded = onLoaded;
                _onUnloaded = onUnloaded;
                Setup();
            }

            private void Setup()
            {
                _modern = ModAPI.Core.RuntimeCompat.IsModernSceneApi;
                if (_modern)
                {
                    var sceneManagerType = Type.GetType("UnityEngine.SceneManagement.SceneManager, UnityEngine");
                    var sceneLoaded = sceneManagerType?.GetEvent("sceneLoaded");
                    var sceneUnloaded = sceneManagerType?.GetEvent("sceneUnloaded");

                    if (sceneLoaded != null)
                    {
                        var handler = Delegate.CreateDelegate(sceneLoaded.EventHandlerType, this, GetType().GetMethod(nameof(OnSceneLoadedModern), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                        sceneLoaded.GetAddMethod().Invoke(null, new object[] { handler });
                    }
                    if (sceneUnloaded != null)
                    {
                        var handler = Delegate.CreateDelegate(sceneUnloaded.EventHandlerType, this, GetType().GetMethod(nameof(OnSceneUnloadedModern), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                        sceneUnloaded.GetAddMethod().Invoke(null, new object[] { handler });
                    }

                    // Fire for the active scene if already loaded.
                    try
                    {
                        var activeSceneProp = sceneManagerType?.GetProperty("activeScene");
                        var activeScene = activeSceneProp != null ? activeSceneProp.GetValue(null, null) : null;
                        var isLoadedProp = activeScene != null ? activeScene.GetType().GetProperty("isLoaded") : null;
                        var nameProp = activeScene != null ? activeScene.GetType().GetProperty("name") : null;
                        if (activeScene != null && isLoadedProp != null && nameProp != null && (bool)isLoadedProp.GetValue(activeScene, null))
                        {
                            OnSceneLoadedModern(activeScene, null);
                        }
                    }
                    catch { }
                }
                else
                {
                    _currentScene = Application.loadedLevelName;
                    if (!string.IsNullOrEmpty(_currentScene))
                    {
                        _onLoaded?.Invoke(_currentScene);
                    }
                }
            }

            private void OnSceneLoadedModern(object scene, object mode)
            {
                var nameProp = scene?.GetType().GetProperty("name");
                var sceneName = nameProp != null ? nameProp.GetValue(scene, null) as string : null;
                if (!string.IsNullOrEmpty(sceneName))
                {
                    _currentScene = sceneName;
                    _onLoaded?.Invoke(sceneName);
                }
            }

            private void OnSceneUnloadedModern(object scene)
            {
                var nameProp = scene?.GetType().GetProperty("name");
                var sceneName = nameProp != null ? nameProp.GetValue(scene, null) as string : null;
                if (!string.IsNullOrEmpty(sceneName))
                {
                    _onUnloaded?.Invoke(sceneName);
                }
            }

            private void OnLevelWasLoaded(int level)
            {
                if (_modern) return;
                var newScene = Application.loadedLevelName;
                if (string.IsNullOrEmpty(newScene)) return;

                if (!string.IsNullOrEmpty(_currentScene) && _currentScene != newScene)
                {
                    _onUnloaded?.Invoke(_currentScene);
                }

                _currentScene = newScene;
                _onLoaded?.Invoke(newScene);
            }
        }
    }
}
