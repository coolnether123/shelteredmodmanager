using System;
using System.IO;
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
    }
}
