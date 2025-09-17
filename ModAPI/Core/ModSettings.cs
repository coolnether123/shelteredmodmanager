using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEngine;

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
        _userPath = Path.Combine(_configDir, "user.json");
        Reload();
    }

    // Factory: resolve by assembly calling into the API (Coolnether123)
    public static ModSettings ForAssembly(Assembly asm)
    {
        if (asm == null) asm = Assembly.GetCallingAssembly();
        ModEntry entry;
        if (ModRegistry.TryGetModByAssembly(asm, out entry))
        {
            return new ModSettings(entry.About != null ? entry.About.id : null, entry.RootPath);
        }

        // Fallback: best-effort guess by walking up from the assembly path
        string asmPath = SafeLocation(asm);
        if (!string.IsNullOrEmpty(asmPath))
        {
            try
            {
                var dir = new DirectoryInfo(Path.GetDirectoryName(asmPath));
                // Expect .../mods/enabled/<ModName>/Assemblies/(tfm?)/This.dll
                for (var cursor = dir; cursor != null; cursor = cursor.Parent)
                {
                    var aboutJson = Path.Combine(cursor.FullName, "About");
                    if (Directory.Exists(aboutJson))
                    {
                        string aboutFile = Path.Combine(aboutJson, "About.json");
                        string modId = null;
                        try
                        {
                            if (File.Exists(aboutFile))
                            {
                                var text = File.ReadAllText(aboutFile);
                                var about = JsonUtility.FromJson<ModAbout>(text);
                                modId = about != null ? about.id : null;
                            }
                        }
                        catch (Exception ex) { MMLog.WarnOnce("ModSettings.ForAssembly.ReadAbout", "Error reading About.json: " + ex.Message); }
                        return new ModSettings(modId, cursor.FullName);
                    }
                }
            }
            catch (Exception ex) { MMLog.WarnOnce("ModSettings.ForAssembly.Probe", "Error probing for mod root: " + ex.Message); }
        }

        // Last resort: no known root (legacy loose DLLs). Root is dll folder.
        try
        {
            var root = Path.GetDirectoryName(asmPath);
            return new ModSettings(null, root);
        }
        catch
        {
            // As a final fallback, use current directory
            return new ModSettings(null, Directory.GetCurrentDirectory());
        }
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
            Directory.CreateDirectory(_configDir);
            var file = new ModConfigFile { entries = MapToArray(_user) };
            var json = JsonUtility.ToJson(file, true);
            File.WriteAllText(_userPath, json);

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
}