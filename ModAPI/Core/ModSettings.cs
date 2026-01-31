using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace ModAPI.Core
{
    /**
     * Coolnether123
     * ModSettings: read/write settings from Config/default.json and Config/user.json.
     * Uses an 'entries' array format to be compatible with Unity's JsonUtility. 
     * 
     * WIP. TODO
     */
    [Serializable]
    public class ModConfigEntry
    {
        public string key;
        public string type;  // string|int|float|bool
        public string value; // serialized value
    }

    [Serializable]
    public class ModConfigFile
    {
        public ModConfigEntry[] entries;
    }

    public class ModSettings
    {
        private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

        private readonly string _modId;           // may be null for legacy
        private readonly string _rootPath;        // mod root folder
        private readonly string _configDir;       // <root>/Config
        private readonly string _defaultPath;     // default.json
        private readonly string _userPath;        // user.json

        // In-memory maps: key -> (type,value) as strings; effective merges user over defaults
        private readonly Dictionary<string, ModConfigEntry> _defaults = new Dictionary<string, ModConfigEntry>(KeyComparer);
        private readonly Dictionary<string, ModConfigEntry> _user = new Dictionary<string, ModConfigEntry>(KeyComparer);
        private readonly Dictionary<string, ModConfigEntry> _effective = new Dictionary<string, ModConfigEntry>(KeyComparer);

        private ModSettings(string modId, string rootPath)
        {
            _modId = modId;
            _rootPath = rootPath;
            _configDir = Path.Combine(_rootPath ?? string.Empty, "Config");
            _defaultPath = Path.Combine(_configDir, "default.json");
            
            // Centralized User Configuration Storage
            // If a valid Mod ID is present, user settings are stored in 'mods/ModAPI/User/<ModID>/user.json'.
            // This ensures user data remains isolated from mod updates and persists correctly across versions.
            if (!string.IsNullOrEmpty(_modId))
            {
                // Construct path relative to game root: <GameRoot>/mods/ModAPI/User/<ModID>
                string gameRoot = Directory.GetParent(Application.dataPath).FullName;
                string modApiUser = Path.Combine(Path.Combine(Path.Combine(gameRoot, "mods"), "ModAPI"), Path.Combine("User", _modId));
                
                // Ensure the directory structure exists before attempting IO operations
                if (!Directory.Exists(modApiUser)) Directory.CreateDirectory(modApiUser);
                _userPath = Path.Combine(modApiUser, "user.json");
            }
            else
            {
                // Legacy Fallback: Store locally in the mod's Config directory
                _userPath = Path.Combine(_configDir, "user.json");
            }

            Reload();
        }

        // Factory: resolve by assembly calling into the API (Coolnether123)
        public static ModSettings ForAssembly(Assembly asm)
        {
            if (asm == null) asm = Assembly.GetCallingAssembly();

            ModEntry entry;
            if (!ModRegistry.TryGetModByAssembly(asm, out entry) || entry == null)
            {
                var asmPath = SafeLocation(asm) ?? "<unknown>";
                throw new InvalidOperationException(
                    $"Assembly {asm.FullName} is not registered with ModRegistry. " +
                    $"Did PluginManager.LoadAssemblies() run? (Assembly location: {asmPath})");
            }

            MMLog.WriteDebug($"Loaded settings for {entry.Id} from {entry.RootPath}");
            return new ModSettings(entry.About != null ? entry.About.id : null, entry.RootPath);
        }

        // Convenience for typical plugin callsites (Coolnether123)
        public static ModSettings ForThisAssembly()
        {
            return ForAssembly(Assembly.GetCallingAssembly());
        }

        // Direct factory for Manager UI or tools: provide a mod root (and optional id). (Coolnether123)
        public static ModSettings ForModRoot(string rootPath, string modId)
        {
            if (string.IsNullOrEmpty(rootPath)) rootPath = Directory.GetCurrentDirectory();
            return new ModSettings(modId, rootPath);
        }

        public void Reload()
        {
            _defaults.Clear();
            _user.Clear();
            _effective.Clear();
            LoadInto(_defaultPath, _defaults);
            LoadInto(_userPath, _user);
            // Merge user over defaults
            foreach (var kv in _defaults)
                _effective[kv.Key] = Clone(kv.Value);
            foreach (var kv in _user)
                _effective[kv.Key] = Clone(kv.Value);
        }

        private static ModConfigEntry Clone(ModConfigEntry e)
        {
            return new ModConfigEntry { key = e.key, type = e.type, value = e.value };
        }

        private void LoadInto(string path, Dictionary<string, ModConfigEntry> target)
        {
            try
            {
                if (!File.Exists(path)) return;
                var json = File.ReadAllText(path);
                var file = JsonUtility.FromJson<ModConfigFile>(json);
                if (file == null || file.entries == null) return;
                foreach (var e in file.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.key)) continue;
                    if (string.IsNullOrEmpty(e.type)) e.type = InferTypeFromValue(e.value);
                    target[e.key] = new ModConfigEntry { key = e.key, type = e.type, value = e.value };
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("Settings load error ('" + path + "'): " + ex.Message);
            }
        }

        private static string InferTypeFromValue(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "string";
            int i; float f; bool b;
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out i)) return "int";
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out f)) return "float";
            if (bool.TryParse(raw, out b)) return "bool";
            return "string";
        }

        private static string SafeLocation(Assembly asm)
        {
            try { return asm.Location; } catch { return null; }
        }

        // Getters (typed) (Coolnether123)

        /// <summary>
        /// Automatically binds public fields of a config object to the settings.
        /// Values are loaded from settings into the object. 
        /// If a setting is missing, the object's current field value is used as the default.
        /// </summary>
        public void AutoBind<T>(T config) where T : class
        {
            if (config == null) return;
            var type = typeof(T);
            foreach (var f in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                var key = f.Name;
                if (f.FieldType == typeof(string)) f.SetValue(config, GetString(key, (string)f.GetValue(config)));
                else if (f.FieldType == typeof(int)) f.SetValue(config, GetInt(key, (int)f.GetValue(config)));
                else if (f.FieldType == typeof(float)) f.SetValue(config, GetFloat(key, (float)f.GetValue(config)));
                else if (f.FieldType == typeof(bool)) f.SetValue(config, GetBool(key, (bool)f.GetValue(config)));
            }
        }

        public string GetString(string key, string fallback)
        {
            ModConfigEntry e; if (!_effective.TryGetValue(key, out e)) return fallback;
            return e.value ?? fallback;
        }

        public int GetInt(string key, int fallback)
        {
            ModConfigEntry e; int v;
            if (_effective.TryGetValue(key, out e) && int.TryParse(e.value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        public float GetFloat(string key, float fallback)
        {
            ModConfigEntry e; float v;
            if (_effective.TryGetValue(key, out e) && float.TryParse(e.value, NumberStyles.Float, CultureInfo.InvariantCulture, out v)) return v;
            return fallback;
        }

        public bool GetBool(string key, bool fallback)
        {
            ModConfigEntry e; bool v;
            if (_effective.TryGetValue(key, out e) && TryParseBool(e.value, out v)) return v;
            return fallback;
        }

        private static bool TryParseBool(string raw, out bool v)
        {
            if (bool.TryParse(raw, out v)) return true;
            if (raw == null) { v = false; return false; }
            var s = raw.Trim().ToLowerInvariant();
            if (s == "1" || s == "yes" || s == "y" || s == "on") { v = true; return true; }
            if (s == "0" || s == "no" || s == "n" || s == "off") { v = false; return true; }
            v = false; return false;
        }

        // Setters (update user map; SaveUser() persists) (Coolnether123)
        public void SetString(string key, string value)
        {
            SetInternal(key, "string", value);
        }

        public void SetInt(string key, int value)
        {
            SetInternal(key, "int", value.ToString(CultureInfo.InvariantCulture));
        }

        public void SetFloat(string key, float value)
        {
            SetInternal(key, "float", value.ToString(CultureInfo.InvariantCulture));
        }

        public void SetBool(string key, bool value)
        {
            SetInternal(key, "bool", value ? "true" : "false");
        }

        private void SetInternal(string key, string type, string raw)
        {
            // Compare against defaults; if equal, remove from user map; else set in user
            ModConfigEntry def;
            bool equalsDefault = false;
            if (_defaults.TryGetValue(key, out def))
            {
                var defType = string.IsNullOrEmpty(def.type) ? "string" : def.type;
                equalsDefault = KeyComparer.Equals(defType, type) && string.Equals((def.value ?? string.Empty), (raw ?? string.Empty), StringComparison.Ordinal);
            }

            if (equalsDefault)
            {
                _user.Remove(key);
            }
            else
            {
                _user[key] = new ModConfigEntry { key = key, type = type, value = raw };
            }

            // Update effective view
            ModConfigEntry newEff = null;
            if (_user.TryGetValue(key, out newEff))
            {
                _effective[key] = Clone(newEff);
            }
            else if (_defaults.TryGetValue(key, out def))
            {
                _effective[key] = Clone(def);
            }
            else
            {
                // No default: user value is the only source. If user removed, drop effective.
                if (_user.ContainsKey(key)) _effective[key] = Clone(_user[key]);
                else _effective.Remove(key);
            }
        }

        // Persist only the keys that differ from defaults (Coolnether123)
        public void SaveUser()
        {
            try
            {
                // Ensure directory for _userPath exists (it might be different from _configDir now)
                string userDir = Path.GetDirectoryName(_userPath);
                if (!string.IsNullOrEmpty(userDir) && !Directory.Exists(userDir)) Directory.CreateDirectory(userDir);

                var file = new ModConfigFile { entries = MapToArray(_user) };
                var json = JsonUtility.ToJson(file, true);
                File.WriteAllText(_userPath, json);
                MMLog.Write($"[ModSettings] Successfully saved user config to: {_userPath}");

                // Also write an INI mirror for external tools (optional consumer)
                try
                {
                    var iniPath = Path.Combine(_configDir, "user.ini");
                    using (var sw = new StreamWriter(iniPath, false))
                    {
                        sw.WriteLine("[Settings]");
                        foreach (var kv in _user)
                        {
                            var val = kv.Value != null ? (kv.Value.value ?? string.Empty) : string.Empty;
                            // Basic sanitation: replace newlines with spaces
                            val = val.Replace('\r', ' ').Replace('\n', ' ');
                            sw.WriteLine(kv.Key + "=" + val);
                        }
                    }
                }
                catch (Exception iniEx)
                {
                    MMLog.Write("Settings INI mirror error: " + iniEx.Message);
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("Settings save error ('" + _userPath + "'): " + ex.Message);
            }
        }

        public void LoadSettings()
        {
            // For now, ModSettings handles its own loading via constructor/singleton pattern
            // but we can expose a reload if needed.
            // _user is equivalent to loading user.json
        }

        public void ResetToDefaults()
        {
            ResetUser();
        }

        public void SaveSettings()
        {
             SaveUser();
        }

        // Clears user overrides (removes all entries) but does not touch defaults. (Coolnether123)
        public void ResetUser()
        {
            _user.Clear();
            // Recompute effective from defaults only
            _effective.Clear();
            foreach (var kv in _defaults)
                _effective[kv.Key] = Clone(kv.Value);
            SaveUser();
        }

        public IEnumerable<string> Keys()
        {
            return _effective.Keys;
        }

        // Returns the declared/inferred type of a key (string|int|float|bool)
        public string GetTypeOf(string key)
        {
            ModConfigEntry e;
            if (_effective.TryGetValue(key, out e))
            {
                return string.IsNullOrEmpty(e.type) ? "string" : e.type;
            }
            return "string";
        }

        private static ModConfigEntry[] MapToArray(Dictionary<string, ModConfigEntry> map)
        {
            var arr = new ModConfigEntry[map.Count];
            int i = 0;
            foreach (var kv in map)
                arr[i++] = Clone(kv.Value);
            return arr;
        }

        /// <summary>Enable transpiler IL dumping (Volatile for thread safety)</summary>
        public static bool DebugTranspilers 
        { 
            get => _debugTranspilers;
            set => _debugTranspilers = value;
        }
        private static volatile bool _debugTranspilers = false;

        /// <summary>SmartWatcher polling interval in frames.</summary>
        public static int SmartWatcherPollInterval 
        { 
            get => _smartWatcherPoll;
            set => _smartWatcherPoll = Mathf.Max(1, value);
        }
        private static int _smartWatcherPoll = 5;

        // Load from config file:
        public static void LoadDebugSettings(string configPath)
        {
            if (File.Exists(configPath))
            {
                var json = File.ReadAllText(configPath);
                var config = JsonUtility.FromJson<DebugConfig>(json);
                if (config != null)
                {
                    _debugTranspilers = config.debugTranspilers;
                    _smartWatcherPoll = config.smartWatcherPollInterval;
                }
            }
        }

        [System.Serializable]
        private class DebugConfig
        {
            public bool debugTranspilers = false;
            public int smartWatcherPollInterval = 5;
        }
    }
}
