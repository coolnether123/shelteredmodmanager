using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using ModAPI.Saves;

namespace ModAPI.Core
{
    /// <summary>
    /// A file-based replacement for Unity's PlayerPrefs, storing settings in the ModAPI/User directory.
    /// </summary>
    public static class ModPrefs
    {
        private static Dictionary<string, string> _values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static bool _loaded = false;
        private static string SettingsPath => Path.Combine(DirectoryProvider.UserRoot, "settings.json");
        public static string UserRoot => DirectoryProvider.UserRoot;

        public static bool DebugTranspilers
        {
            get => GetBool("DebugTranspilers", false);
            set => SetBool("DebugTranspilers", value);
        }


        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

            try
            {
                string path = SettingsPath;
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    var data = JsonUtility.FromJson<PrefData>(json);
                    if (data != null && data.Keys != null && data.Values != null)
                    {
                        for (int i = 0; i < data.Keys.Length && i < data.Values.Length; i++)
                        {
                            _values[data.Keys[i]] = data.Values[i];
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ModPrefs] Failed to load settings: " + ex.Message);
            }
        }

        public static int GetInt(string key, int defaultValue = 0)
        {
            EnsureLoaded();
            if (_values.TryGetValue(key, out string val) && int.TryParse(val, out int result))
                return result;
            return defaultValue;
        }

        public static void SetInt(string key, int value)
        {
            EnsureLoaded();
            _values[key] = value.ToString();
        }

        public static bool GetBool(string key, bool defaultValue = false)
        {
            EnsureLoaded();
            if (_values.TryGetValue(key, out string val) && bool.TryParse(val, out bool result))
                return result;
            return defaultValue;
        }

        public static void SetBool(string key, bool value)
        {
            EnsureLoaded();
            _values[key] = value.ToString().ToLower();
        }


        public static string GetString(string key, string defaultValue = "")
        {
            EnsureLoaded();
            if (_values.TryGetValue(key, out string val))
                return val;
            return defaultValue;
        }

        public static void SetString(string key, string value)
        {
            EnsureLoaded();
            _values[key] = value;
        }

        public static void Save()
        {
            try
            {
                var data = new PrefData();
                var keys = new List<string>();
                var values = new List<string>();
                foreach (var kvp in _values)
                {
                    keys.Add(kvp.Key);
                    values.Add(kvp.Value);
                }
                data.Keys = keys.ToArray();
                data.Values = values.ToArray();

                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(SettingsPath, json);
                
                // Also ensure the descriptive .md file exists as requested
                string mdPath = Path.Combine(DirectoryProvider.UserRoot, "settings.md");
                if (!File.Exists(mdPath))
                {
                    File.WriteAllText(mdPath, "# ModAPI User Settings\n\nThis folder contains internal state and user settings for the Sheltered Modding API.\n\n- `settings.json`: Contains technical flags (e.g., tutorial seen status).");
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ModPrefs] Failed to save settings: " + ex.Message);
            }
        }

        [Serializable]
        private class PrefData
        {
            public string[] Keys;
            public string[] Values;
        }
    }
}
