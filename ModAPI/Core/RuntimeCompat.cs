using System;
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
        private static bool? _isModernSceneApi;

        public static string UnityVersion
        {
            get
            {
                if (!string.IsNullOrEmpty(_unityVersion)) return _unityVersion;
                _unityVersion = TryGetAppStringProperty("unityVersion") ?? "unknown";
                return _unityVersion;
            }
        }

        public static string GameVersion
        {
            get
            {
                if (!string.IsNullOrEmpty(_gameVersion)) return _gameVersion;
                _gameVersion = TryGetAppStringProperty("version") ?? "unknown";
                return _gameVersion;
            }
        }

        /// <summary>
        /// True when modern SceneManager events exist (Unity 5.4+); false on 5.3.
        /// </summary>
        public static bool IsModernSceneApi
        {
            get
            {
                if (_isModernSceneApi.HasValue) return _isModernSceneApi.Value;
                try
                {
                    var evt = typeof(UnityEngine.SceneManagement.SceneManager).GetEvent("sceneLoaded");
                    _isModernSceneApi = evt != null;
                }
                catch
                {
                    _isModernSceneApi = false;
                }
                return _isModernSceneApi.Value;
            }
        }

        private static string TryGetAppStringProperty(string name)
        {
            try
            {
                var prop = typeof(Application).GetProperty(name, BindingFlags.Public | BindingFlags.Static);
                if (prop == null) return null;
                var val = prop.GetValue(null, null) as string;
                if (!string.IsNullOrEmpty(val)) return val;
            }
            catch
            {
                // Swallow; older Unity builds may not expose these properties or their icalls.
            }
            return null;
        }
    }
}
