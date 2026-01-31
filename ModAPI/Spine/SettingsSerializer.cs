using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Globalization;
using System.Linq;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Spine
{
    [Serializable]
    public class SavedSettingEntry
    {
        public string id;
        public string value;
    }

    [Serializable]
    public class SavedSettingsList
    {
        public SavedSettingEntry[] settings;
    }

    public static class SettingsSerializer
    {
        public static string GetConfigPath(string modId)
        {
            var entry = ModRegistry.GetMod(modId);
            if (entry == null) return null;
            return Path.Combine(entry.RootPath, "Config/spine_settings.json");
        }

        public static void Save(string modId, object settingsObject, IEnumerable<SettingDefinition> definitions)
        {
            if (settingsObject == null) return;
            var path = GetConfigPath(modId);
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                var entries = new List<SavedSettingEntry>();
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

                var defArray = definitions.ToArray();
                MMLog.WriteDebug($"Serializing {defArray.Length} settings for {modId}...");

                // Manual JSON Construction to bypass JsonUtility quirkiness on Arrays
                System.Text.StringBuilder json = new System.Text.StringBuilder();
                json.AppendLine("{");
                json.AppendLine("    \"settings\": [");

                int count = 0;
                foreach (var def in defArray)
                {
                    if (string.IsNullOrEmpty(def.FieldName)) continue;
                    var field = settingsObject.GetType().GetField(def.FieldName, flags);
                    if (field != null)
                    {
                        var val = field.GetValue(settingsObject);
                        string raw = val is Color col ? ColorToHex(col) : Convert.ToString(val, CultureInfo.InvariantCulture);
                        
                        // Escape value string for JSON
                        raw = raw.Replace("\\", "\\\\").Replace("\"", "\\\"");

                        if (count > 0) json.AppendLine(",");
                        json.Append($"        {{ \"id\": \"{def.Id}\", \"value\": \"{raw}\" }}");
                        count++;
                    }
                }
                
                json.AppendLine("");
                json.AppendLine("    ]");
                json.AppendLine("}");

                File.WriteAllText(path, json.ToString());
                MMLog.WriteDebug($"Settings saved successfully to: {path} ({count} entries)");
            }
            catch (Exception ex) { MMLog.WriteError($"Save failed for {modId} at {path}: {ex.Message}"); }
        }

        public static void Load(string modId, object settingsObject, IEnumerable<SettingDefinition> definitions)
        {
            if (settingsObject == null) return;
            var path = GetConfigPath(modId);
            if (!File.Exists(path)) return;

            try
            {
                var json = File.ReadAllText(path);
                SavedSettingsList list = null;
                try 
                {
                    list = JsonUtility.FromJson<SavedSettingsList>(json);
                } 
                catch { }

                var data = new Dictionary<string, string>();
                
                // Fallback manual parsing if JsonUtility fails on the array
                if (list == null || list.settings == null || list.settings.Length == 0)
                {
                    // Simple regex-based parser for the specific format we write
                    // {"id": "ID", "value": "VALUE"}
                    var matches = System.Text.RegularExpressions.Regex.Matches(json, 
                        "\\{\\s*\"id\"\\s*:\\s*\"([^\"]+)\"\\s*,\\s*\"value\"\\s*:\\s*\"([^\"]*)\"\\s*\\}");
                    
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        if (m.Success)
                        {
                            var id = m.Groups[1].Value;
                            var val = m.Groups[2].Value;
                            data[id] = val; // Note: Doesn't handle escaped quotes perfectly, but sufficient for basic types
                        }
                    }
                }
                else
                {
                    foreach (var entry in list.settings) 
                    {
                        if (entry != null && !string.IsNullOrEmpty(entry.id)) data[entry.id] = entry.value;
                    }
                }

                int loadedCount = 0;
                var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
                foreach (var def in definitions)
                {
                    if (data.TryGetValue(def.Id, out var raw))
                    {
                        var field = settingsObject.GetType().GetField(def.FieldName, flags);
                        if (field != null)
                        {
                            object val = ConvertValue(raw, field.FieldType);
                            if (val != null) 
                            { 
                                field.SetValue(settingsObject, val);
                                loadedCount++;
                            }
                        }
                    }
                }
                MMLog.Write($"Settings loaded successfully from: {path} ({loadedCount}/{data.Count} values applied)");
            }
            catch (Exception ex) { MMLog.WriteError($"Load failed for {modId} from {path}: {ex.Message}"); }
        }

        private static object ConvertValue(string raw, Type type)
        {
            if (string.IsNullOrEmpty(raw)) return null;
            try
            {
                if (type == typeof(bool)) return bool.Parse(raw);
                if (type == typeof(int)) return int.Parse(raw);
                if (type == typeof(float)) return float.Parse(raw, CultureInfo.InvariantCulture);
                if (type == typeof(string)) return raw;
                if (type.IsEnum) return Enum.Parse(type, raw);
                if (type == typeof(Color)) return HexToColor(raw);
            }
            catch { }
            return null;
        }

        private static string ColorToHex(Color color)
        {
            return string.Format("#{0:X2}{1:X2}{2:X2}{3:X2}",
                (int)(color.r * 255), (int)(color.g * 255), (int)(color.b * 255), (int)(color.a * 255));
        }

        private static Color HexToColor(string hex)
        {
            hex = hex.TrimStart('#');
            if (hex.Length < 6) return Color.white;
            float r = int.Parse(hex.Substring(0, 2), NumberStyles.HexNumber) / 255f;
            float g = int.Parse(hex.Substring(2, 2), NumberStyles.HexNumber) / 255f;
            float b = int.Parse(hex.Substring(4, 2), NumberStyles.HexNumber) / 255f;
            float a = hex.Length >= 8 ? int.Parse(hex.Substring(6, 2), NumberStyles.HexNumber) / 255f : 1f;
            return new Color(r, g, b, a);
        }
    }
}