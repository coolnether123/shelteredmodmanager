using System;
using System.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Runtime-safe access to Unity application environment values.
    /// Unity icalls can be unavailable during tests and early bootstrap, so callers should use this facade.
    /// </summary>
    public static class RuntimeEnvironmentInfo
    {
        public const string UnavailableValue = "unavailable";
        public const string UnknownValue = "unknown";

        private static readonly object _probeLock = new object();
        private static IUnityApplicationProbe _unityProbe = new UnityApplicationProbe();

        public static UnityRuntimeInfo GetUnityRuntimeInfo()
        {
            return new UnityRuntimeInfo(
                GetUnityString("dataPath", UnavailableValue),
                GetUnityString("unityVersion", UnknownValue),
                GetUnityString("version", UnknownValue),
                GetUnityPlatform());
        }

        public static string UnityDataPath
        {
            get { return GetUnityString("dataPath", UnavailableValue); }
        }

        public static string UnityVersion
        {
            get { return GetUnityString("unityVersion", UnknownValue); }
        }

        public static string GameVersion
        {
            get { return GetUnityString("version", UnknownValue); }
        }

        public static string UnityPlatform
        {
            get { return GetUnityPlatform(); }
        }

        internal static void SetUnityApplicationProbeForTests(IUnityApplicationProbe probe)
        {
            lock (_probeLock)
            {
                _unityProbe = probe ?? new UnityApplicationProbe();
            }
        }

        private static string GetUnityString(string propertyName, string fallback)
        {
            IUnityApplicationProbe probe = GetProbe();
            string value;
            Exception error;
            if (probe.TryGetStringProperty(propertyName, out value, out error) && !string.IsNullOrEmpty(value))
                return value;
            return fallback;
        }

        private static string GetUnityPlatform()
        {
            IUnityApplicationProbe probe = GetProbe();
            string platform;
            Exception error;
            if (probe.TryGetPlatform(out platform, out error) && !string.IsNullOrEmpty(platform))
                return platform;
            return UnavailableValue;
        }

        private static IUnityApplicationProbe GetProbe()
        {
            lock (_probeLock)
            {
                return _unityProbe;
            }
        }
    }

    public sealed class UnityRuntimeInfo
    {
        public readonly string DataPath;
        public readonly string UnityVersion;
        public readonly string GameVersion;
        public readonly string Platform;

        public UnityRuntimeInfo(string dataPath, string unityVersion, string gameVersion, string platform)
        {
            DataPath = string.IsNullOrEmpty(dataPath) ? RuntimeEnvironmentInfo.UnavailableValue : dataPath;
            UnityVersion = string.IsNullOrEmpty(unityVersion) ? RuntimeEnvironmentInfo.UnknownValue : unityVersion;
            GameVersion = string.IsNullOrEmpty(gameVersion) ? RuntimeEnvironmentInfo.UnknownValue : gameVersion;
            Platform = string.IsNullOrEmpty(platform) ? RuntimeEnvironmentInfo.UnavailableValue : platform;
        }
    }

    internal interface IUnityApplicationProbe
    {
        bool TryGetStringProperty(string propertyName, out string value, out Exception error);
        bool TryGetPlatform(out string platform, out Exception error);
    }

    internal sealed class UnityApplicationProbe : IUnityApplicationProbe
    {
        public bool TryGetStringProperty(string propertyName, out string value, out Exception error)
        {
            value = null;
            error = null;

            try
            {
                PropertyInfo property = typeof(Application).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Static);
                if (property == null)
                    return false;

                value = property.GetValue(null, null) as string;
                return !string.IsNullOrEmpty(value);
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        public bool TryGetPlatform(out string platform, out Exception error)
        {
            platform = null;
            error = null;

            try
            {
                platform = Application.platform.ToString();
                return !string.IsNullOrEmpty(platform);
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }
    }
}
