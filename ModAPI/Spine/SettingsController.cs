using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Spine
{
    /// <summary>
    /// Core manager for ModAPI settings. Replaces ModSettings and AutoSettingsProvider.
    /// Handles manual JSON serialization, delegate caching, and New Game+ logic.
    /// </summary>
    public class SettingsController : ISettingsProvider, ISettingsProvider2, ISettingsProvider3
    {
        private readonly object _owner;
        private readonly object _notificationTarget;
        private readonly ModEntry _mod;
        private readonly IPluginContext _context;
        private readonly List<SettingDefinition> _definitions;
        private readonly Dictionary<string, SettingDefinition> _defById;
        private readonly Dictionary<string, object> _loadedGlobalValues = new Dictionary<string, object>();
        private readonly Dictionary<string, object> _loadedPerSaveValues = new Dictionary<string, object>();
        
        private bool _isDirty;
        private float _lastWriteTime;
        private string _serializedCache;
        private const float DebounceTime = 2.0f;

        public bool IsReady { get; private set; }

        public SettingsController(IPluginContext context, object owner)
            : this(context, owner, null)
        {
        }

        public SettingsController(IPluginContext context, object owner, object notificationTarget)
        {
            _context = context;
            _mod = context != null ? context.Mod : null;
            _owner = owner;
            _notificationTarget = notificationTarget;
            _definitions = Scan(owner);
            _defById = _definitions.ToDictionary(d => d.Id);
            IsReady = true;
        }

        public IEnumerable<SettingDefinition> GetSettings() => _definitions;
        public object GetSettingsObject() => _owner;
        public string SerializeToJson() => SerializeJsonInternal();

        public IEnumerable<SettingValueSnapshot> GetValueSnapshots()
        {
            foreach (var def in _definitions)
            {
                if (def.Type == SettingType.Button)
                    continue;

                object current = def.Getter != null ? def.Getter(_owner) : null;
                object globalValue;
                bool hasGlobal = _loadedGlobalValues.TryGetValue(def.Id, out globalValue);
                object saveValue;
                bool hasSave = _loadedPerSaveValues.TryGetValue(def.Id, out saveValue);

                object activeValue = def.DefaultValue;
                SettingsValueSource source = SettingsValueSource.Default;
                if (def.Scope == SettingsScope.Global)
                {
                    if (hasGlobal)
                    {
                        activeValue = globalValue;
                        source = SettingsValueSource.Global;
                    }
                }
                else
                {
                    if (hasSave)
                    {
                        activeValue = saveValue;
                        source = SettingsValueSource.ActiveSave;
                    }
                    else if (hasGlobal)
                    {
                        activeValue = globalValue;
                        source = SettingsValueSource.Global;
                    }
                }

                yield return new SettingValueSnapshot
                {
                    SettingId = def.Id,
                    Scope = def.Scope,
                    DefaultValue = def.DefaultValue,
                    CurrentValue = current,
                    GlobalValue = hasGlobal ? globalValue : null,
                    HasGlobalValue = hasGlobal,
                    ActiveSaveValue = hasSave ? saveValue : null,
                    HasActiveSaveValue = hasSave,
                    ActivePersistedValue = activeValue,
                    ActiveSource = source,
                    IsTweakedFromActive = !ValuesEqual(current, activeValue)
                };
            }
        }

        public bool TrySaveSetting(string settingId, object value, SettingsWriteTarget target)
        {
            SettingDefinition def;
            if (string.IsNullOrEmpty(settingId) || !_defById.TryGetValue(settingId, out def) || def.Type == SettingType.Button)
                return false;

            object converted;
            try
            {
                converted = ConvertValue(value, ResolveSettingType(def));
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Failed to convert setting '" + settingId + "': " + ex.Message);
                return false;
            }

            if (def.Validate != null && !def.Validate(converted, _owner))
                return false;

            if (target == SettingsWriteTarget.DeclaredScope)
                target = def.Scope == SettingsScope.PerSave ? SettingsWriteTarget.ActiveSave : SettingsWriteTarget.GlobalDefaults;

            if (target == SettingsWriteTarget.GlobalDefaults)
            {
                _loadedGlobalValues[def.Id] = converted;
                if (def.Scope == SettingsScope.Global)
                {
                    ApplySettingValue(def, converted);
                    NotifySettingChanged(def);
                }

                WriteToDisk(SettingsScope.Global);
                return true;
            }

            if (target == SettingsWriteTarget.ActiveSave)
            {
                ApplySettingValue(def, converted);
                NotifySettingChanged(def);

                if (def.Scope == SettingsScope.PerSave)
                {
                    if (WriteToDisk(SettingsScope.PerSave))
                        _loadedPerSaveValues[def.Id] = converted;
                }
                else if (WriteToDisk(SettingsScope.Global))
                {
                    _loadedGlobalValues[def.Id] = converted;
                }

                return true;
            }

            return false;
        }

        public void OnSettingsLoaded()
        {
            ModManagerBase manager = _owner as ModManagerBase;
            if (manager != null)
            {
                manager.OnSettingsLoaded();
                return;
            }

            manager = _notificationTarget as ModManagerBase;
            if (manager != null) manager.OnSettingsLoaded();
        }

        public void ResetToDefaults()
        {
            foreach (var def in _definitions)
            {
                if (def.Setter != null) def.Setter(_owner, def.DefaultValue);
            }
            _loadedGlobalValues.Clear();
            _loadedPerSaveValues.Clear();
            _isDirty = true;
            Save();
        }

        #region Scanning & Delegate Caching

        private List<SettingDefinition> Scan(object owner)
        {
            var definitions = new List<SettingDefinition>();
            var type = owner.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            // Fields
            foreach (var field in type.GetFields(flags))
            {
                var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(field, typeof(ModSettingAttribute));
                if (attr != null)
                {
                    var def = SettingDefinitionFactory.Create(attr, field, type);
                    def.Getter = SettingDefinitionFactory.CreateGetter(field);
                    def.Setter = SettingDefinitionFactory.CreateSetter(field);
                    def.DefaultValue = def.Getter(owner);
                    SettingDefinitionFactory.ApplyPresets(field, def);
                    definitions.Add(def);
                }
            }

            // Properties
            foreach (var prop in type.GetProperties(flags))
            {
                var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(prop, typeof(ModSettingAttribute));
                if (attr != null && prop.CanRead && prop.CanWrite)
                {
                    var def = SettingDefinitionFactory.Create(attr, prop, type);
                    def.Getter = SettingDefinitionFactory.CreateGetter(prop);
                    def.Setter = SettingDefinitionFactory.CreateSetter(prop);
                    def.DefaultValue = def.Getter(owner);
                    SettingDefinitionFactory.ApplyPresets(prop, def);
                    definitions.Add(def);
                }
            }

            // Methods (Buttons)
            foreach (var method in type.GetMethods(flags))
            {
                var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(method, typeof(ModSettingAttribute));
                if (attr != null && method.GetParameters().Length == 0)
                {
                    var def = SettingDefinitionFactory.Create(attr, method, type);
                    def.Type = SettingType.Button;
                    def.OnChanged = (obj) => method.Invoke(owner, null);
                    definitions.Add(def);
                }
            }

            return definitions.OrderBy(d => d.SortOrder).ThenBy(d => d.Label).ToList();
        }

        #endregion

        #region Persistence

        public void Load()
        {
            ResetDefinitionsToDefaults();
            _loadedGlobalValues.Clear();
            _loadedPerSaveValues.Clear();

            // Load Global
            string globalPath = GetPath(SettingsScope.Global);
            if (File.Exists(globalPath)) ApplyJson(File.ReadAllText(globalPath), SettingsScope.Global);

            // Load PerSave if available
            string perSavePath = GetPath(SettingsScope.PerSave);
            if (!string.IsNullOrEmpty(perSavePath) && File.Exists(perSavePath)) ApplyJson(File.ReadAllText(perSavePath), SettingsScope.PerSave);
            
            _serializedCache = SerializeJsonInternal();
            OnSettingsLoaded();
        }

        public void Save()
        {
            _serializedCache = SerializeJsonInternal();
            if (WriteToDisk(SettingsScope.Global))
                CapturePersistedValues(SettingsScope.Global);
            if (WriteToDisk(SettingsScope.PerSave))
                CapturePersistedValues(SettingsScope.PerSave);
            _isDirty = false;
        }

        private bool WriteToDisk(SettingsScope scope)
        {
            string path = GetPath(scope);
            if (string.IsNullOrEmpty(path)) return false;

            string dir = Path.GetDirectoryName(path);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string json = SerializeScope(scope);
            
            // Atomic Write
            string tmp = path + ".tmp";
            int retries = 3;
            while (retries > 0)
            {
                try
                {
                    File.WriteAllText(tmp, json);
                    if (File.Exists(path)) File.Delete(path);
                    File.Move(tmp, path);
                    return true;
                }
                catch (IOException)
                {
                    retries--;
                    if (retries == 0) throw;
                    System.Threading.Thread.Sleep(100);
                }
            }

            return false;
        }

        private string GetPath(SettingsScope scope)
        {
            if (scope == SettingsScope.Global)
            {
                // UserRoot is already Mods/ModAPI/User
                string modFolder = Path.Combine(ModPrefs.UserRoot, _mod.Id);
                if (!Directory.Exists(modFolder)) Directory.CreateDirectory(modFolder);
                return Path.Combine(modFolder, "settings.json");
            }
            else
            {
                // Root/Saves/Slot_X/mods/{ModId}/settings.json
                if (_context == null || _context.SaveSystem == null) return null;
                string slotPath = _context.SaveSystem.GetCurrentSlotPath();
                if (string.IsNullOrEmpty(slotPath)) return null;

                string modDataFolder = Path.Combine(Path.Combine(slotPath, "mods"), _mod.Id);
                if (!Directory.Exists(modDataFolder)) Directory.CreateDirectory(modDataFolder);
                return Path.Combine(modDataFolder, "settings.json");
            }
        }


        #endregion

        #region Manual JSON Serialization

        private string SerializeJsonInternal()
        {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var def in _definitions)
            {
                if (def.Type == SettingType.Button) continue;
                if (!first) sb.Append(",");
                sb.Append("\"").Append(Escape(def.Id)).Append("\":").Append(ValueToJson(def.Getter(_owner)));
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }


        private string SerializeScope(SettingsScope scope)
        {
            var sb = new StringBuilder();
            sb.Append("{");
            bool first = true;
            foreach (var def in _definitions)
            {
                if (def.Type == SettingType.Button) continue;

                object value;
                if (!TryGetValueForPersistedScope(def, scope, out value))
                    continue;

                if (!first) sb.Append(",");
                sb.Append("\"").Append(Escape(def.Id)).Append("\":").Append(ValueToJson(value));
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
        }

        private bool TryGetValueForPersistedScope(SettingDefinition def, SettingsScope scope, out object value)
        {
            value = null;
            if (def == null)
                return false;

            if (scope == SettingsScope.Global)
            {
                if (def.Scope == SettingsScope.Global)
                {
                    value = def.Getter(_owner);
                    return true;
                }

                return _loadedGlobalValues.TryGetValue(def.Id, out value);
            }

            if (def.Scope != SettingsScope.PerSave)
                return false;

            value = def.Getter(_owner);
            return true;
        }

        private string ValueToJson(object val)
        {
            if (val == null) return "null";
            if (val is bool b) return b ? "true" : "false";
            if (val is string s) return $"\"{Escape(s)}\"";
            if (val is float f) return f.ToString("R", CultureInfo.InvariantCulture);
            if (val is double d) return d.ToString("R", CultureInfo.InvariantCulture);
            if (val is int i) return i.ToString(CultureInfo.InvariantCulture);
            if (val is long l) return l.ToString(CultureInfo.InvariantCulture);
            if (val.GetType().IsEnum) return $"\"{val}\"";
            
            // Complex types fallback to simple recursive JSON
            return SerializeComplex(val, new HashSet<object>());
        }

        private string SerializeComplex(object obj, HashSet<object> seen)
        {
            if (obj == null) return "null";
            if (seen.Contains(obj)) throw new InvalidOperationException("Circular reference detected in settings.");
            seen.Add(obj);

            var type = obj.GetType();
            if (obj is IEnumerable en && !(obj is string))
            {
                var sb = new StringBuilder("[");
                bool first = true;
                foreach (var item in en)
                {
                    if (!first) sb.Append(",");
                    sb.Append(ValueToJson(item));
                    first = false;
                }
                sb.Append("]");
                return sb.ToString();
            }

            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            var res = new StringBuilder("{");
            bool f1 = true;
            foreach (var f in fields)
            {
                if (!f1) res.Append(",");
                res.Append("\"").Append(Escape(f.Name)).Append("\":").Append(ValueToJson(f.GetValue(obj)));
                f1 = false;
            }
            res.Append("}");
            return res.ToString();
        }

        private string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");
        }

        #endregion

        #region Manual JSON Deserialization (Simple Regex/Recursive Descent)

        private void ApplyJson(string json, SettingsScope scope)
        {
            if (string.IsNullOrEmpty(json)) return;
            var data = ParseJson(json);
            foreach (var kvp in data)
            {
                SettingDefinition def;
                if (!_defById.TryGetValue(kvp.Key, out def))
                    continue;

                if (scope == SettingsScope.PerSave && def.Scope != SettingsScope.PerSave)
                    continue;

                if (scope == SettingsScope.Global && def.Scope != SettingsScope.Global && def.Scope != SettingsScope.PerSave)
                    continue;

                if (scope == SettingsScope.Global || def.Scope == scope)
                {
                    try
                    {
                        Type targetType = ResolveSettingType(def);
                        object val = ConvertValue(kvp.Value, targetType);
                        if (scope == SettingsScope.Global)
                            _loadedGlobalValues[def.Id] = val;
                        else
                            _loadedPerSaveValues[def.Id] = val;

                        ApplySettingValue(def, val);
                    }
                    catch (Exception ex) { MMLog.WriteError($"Failed to apply setting {kvp.Key}: {ex.Message}"); }
                }
            }
        }

        private Type ResolveSettingType(SettingDefinition def)
        {
            if (def == null) return typeof(string);
            if (def.EnumType != null) return def.EnumType;

            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            Type ownerType = _owner != null ? _owner.GetType() : null;

            if (ownerType != null && !string.IsNullOrEmpty(def.FieldName))
            {
                var field = ownerType.GetField(def.FieldName, flags);
                if (field != null) return field.FieldType;

                var prop = ownerType.GetProperty(def.FieldName, flags);
                if (prop != null) return prop.PropertyType;
            }

            if (def.DefaultValue != null) return def.DefaultValue.GetType();
            if (def.Getter != null)
            {
                try
                {
                    object current = def.Getter(_owner);
                    if (current != null) return current.GetType();
                }
                catch { }
            }

            return typeof(string);
        }

        private Dictionary<string, object> ParseJson(string json)
        {
            int i = 0;
            object root = ReadValue(json, ref i);
            Dictionary<string, object> result = root as Dictionary<string, object>;
            return result ?? new Dictionary<string, object>();
        }

        private string ReadString(string json, ref int i)
        {
            if (i < json.Length && json[i] == '\"')
                i++;

            var sb = new StringBuilder();
            while (i < json.Length)
            {
                char c = json[i++];
                if (c == '\"')
                    break;

                if (c == '\\' && i < json.Length)
                {
                    char escaped = json[i++];
                    switch (escaped)
                    {
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case '\\': sb.Append('\\'); break;
                        case '"': sb.Append('"'); break;
                        default: sb.Append(escaped); break;
                    }
                }
                else
                {
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        private Dictionary<string, object> ReadObject(string json, ref int i)
        {
            var result = new Dictionary<string, object>();
            if (i < json.Length && json[i] == '{')
                i++;

            while (i < json.Length)
            {
                SkipWhitespace(json, ref i);
                if (i >= json.Length)
                    break;
                if (json[i] == '}')
                {
                    i++;
                    break;
                }

                string key = ReadString(json, ref i);
                SkipWhitespace(json, ref i);
                if (i < json.Length && json[i] == ':')
                    i++;

                result[key] = ReadValue(json, ref i);

                SkipWhitespace(json, ref i);
                if (i < json.Length && json[i] == ',')
                    i++;
            }

            return result;
        }

        private List<object> ReadArray(string json, ref int i)
        {
            var result = new List<object>();
            if (i < json.Length && json[i] == '[')
                i++;

            while (i < json.Length)
            {
                SkipWhitespace(json, ref i);
                if (i >= json.Length)
                    break;
                if (json[i] == ']')
                {
                    i++;
                    break;
                }

                result.Add(ReadValue(json, ref i));
                SkipWhitespace(json, ref i);
                if (i < json.Length && json[i] == ',')
                    i++;
            }

            return result;
        }

        private static void SkipWhitespace(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i]))
                i++;
        }

        private static bool MatchLiteral(string json, ref int i, string literal, object value, out object result)
        {
            result = null;
            if (i + literal.Length > json.Length)
                return false;

            for (int j = 0; j < literal.Length; j++)
            {
                if (json[i + j] != literal[j])
                    return false;
            }

            i += literal.Length;
            result = value;
            return true;
        }

        private object ReadValue(string json, ref int i)
        {
            SkipWhitespace(json, ref i);
            if (i >= json.Length) return null;

            if (json[i] == '\"') return ReadString(json, ref i);
            if (json[i] == '{') return ReadObject(json, ref i);
            if (json[i] == '[') return ReadArray(json, ref i);

            object literal;
            if (MatchLiteral(json, ref i, "true", true, out literal)) return literal;
            if (MatchLiteral(json, ref i, "false", false, out literal)) return literal;
            if (MatchLiteral(json, ref i, "null", null, out literal)) return literal;
            
            // Number
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-' || json[i] == '+' || json[i] == 'e' || json[i] == 'E')) i++;
            string s = json.Substring(start, i - start);
            return s;
        }

        private object ConvertValue(object val, Type targetType)
        {
            if (val == null) return null;
            if (targetType == null || targetType == typeof(object)) return val;
            if (targetType.IsInstanceOfType(val)) return val;
            if (targetType.IsEnum) return Enum.Parse(targetType, val.ToString());
            if (targetType == typeof(bool)) return val.ToString().ToLower() == "true";
            if (targetType == typeof(string)) return val.ToString();
            if (targetType == typeof(int)) return int.Parse(val.ToString(), CultureInfo.InvariantCulture);
            if (targetType == typeof(float)) return float.Parse(val.ToString(), CultureInfo.InvariantCulture);
            if (targetType == typeof(double)) return double.Parse(val.ToString(), CultureInfo.InvariantCulture);
            if (targetType == typeof(long)) return long.Parse(val.ToString(), CultureInfo.InvariantCulture);
            if (targetType == typeof(Color)) return ConvertColor(val);
            return val;
        }

        private static Color ConvertColor(object val)
        {
            Dictionary<string, object> data = val as Dictionary<string, object>;
            if (data != null)
            {
                return new Color(
                    ReadFloat(data, "r", 1f),
                    ReadFloat(data, "g", 1f),
                    ReadFloat(data, "b", 1f),
                    ReadFloat(data, "a", 1f));
            }

            string text = val as string;
            if (!string.IsNullOrEmpty(text))
            {
                Color parsed;
                if (ColorUtility.TryParseHtmlString(text, out parsed))
                    return parsed;
            }

            return Color.white;
        }

        private static float ReadFloat(Dictionary<string, object> data, string key, float fallback)
        {
            object raw;
            if (data == null || !data.TryGetValue(key, out raw) || raw == null)
                return fallback;

            float value;
            return float.TryParse(raw.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                ? value
                : fallback;
        }

        private void ResetDefinitionsToDefaults()
        {
            foreach (var def in _definitions)
            {
                if (def.Type == SettingType.Button)
                    continue;

                ApplySettingValue(def, def.DefaultValue);
            }
        }

        private void CapturePersistedValues(SettingsScope scope)
        {
            if (scope == SettingsScope.PerSave)
                _loadedPerSaveValues.Clear();

            foreach (var def in _definitions)
            {
                if (def.Type == SettingType.Button)
                    continue;

                if (scope == SettingsScope.Global)
                {
                    if (def.Scope == SettingsScope.Global)
                        _loadedGlobalValues[def.Id] = def.Getter(_owner);
                }
                else if (def.Scope == SettingsScope.PerSave)
                {
                    _loadedPerSaveValues[def.Id] = def.Getter(_owner);
                }
            }
        }

        private void ApplySettingValue(SettingDefinition def, object value)
        {
            if (def == null || def.Setter == null)
                return;

            def.Setter(_owner, value);
        }

        private void NotifySettingChanged(SettingDefinition def)
        {
            if (def != null && def.OnChanged != null)
                def.OnChanged(_owner);
        }

        private static bool ValuesEqual(object left, object right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            if (left is IConvertible && right is IConvertible)
                return string.Equals(
                    Convert.ToString(left, CultureInfo.InvariantCulture),
                    Convert.ToString(right, CultureInfo.InvariantCulture),
                    StringComparison.Ordinal);

            return left.Equals(right);
        }

        #endregion

        #region New Game+ Math Engine

        public Dictionary<string, string> GetCarryOverData()
        {
            var data = new Dictionary<string, string>();
            foreach (var def in _definitions.Where(d => d.Scope == SettingsScope.PerSave && d.CarryOverToNewGamePlus))
            {
                data[def.Id] = ValueToJson(def.Getter(_owner));
            }
            return data;
        }

        public void ApplyCarryOverData(Dictionary<string, string> data)
        {
            foreach (var kvp in data)
            {
                if (_defById.TryGetValue(kvp.Key, out var def) && def.CarryOverToNewGamePlus)
                {
                    try
                    {
                        var carryJson = kvp.Value;
                        int index = 0;
                        var rawValue = ReadValue(carryJson, ref index);
                        double carryVal = Convert.ToDouble(ConvertValue(rawValue, typeof(double)), CultureInfo.InvariantCulture);
                        
                        MergeSetting(def, carryVal);
                    }
                    catch (Exception ex) { MMLog.WriteError($"NG+ Merge failed for {kvp.Key}: {ex.Message}"); }
                }
            }
            Save();
        }

        private void MergeSetting(SettingDefinition def, double carryVal)
        {
            double currentVal = Convert.ToDouble(def.Getter(_owner), CultureInfo.InvariantCulture);
            double newVal;
            
            switch (def.NewGamePlusMerge)
            {
                case MergeStrategy.Add: newVal = currentVal + carryVal; break;
                case MergeStrategy.Multiply: newVal = currentVal * carryVal; break;
                default: newVal = carryVal; break;
            }

            // Implementation Rule: perform math in double, but convert back with Math.Round and clamping.
            object finalVal;
            Type t = def.Getter(_owner).GetType();
            
            if (t == typeof(int)) 
                finalVal = (int)Math.Max(def.MinValue ?? int.MinValue, Math.Min(def.MaxValue ?? int.MaxValue, Math.Round(newVal)));
            else if (t == typeof(long)) 
                finalVal = (long)Math.Max(def.MinValue ?? (float)long.MinValue, Math.Min(def.MaxValue ?? (float)long.MaxValue, Math.Round(newVal)));
            else if (t == typeof(float)) 
                finalVal = (float)Math.Max(def.MinValue ?? float.MinValue, Math.Min(def.MaxValue ?? float.MaxValue, newVal));
            else if (t == typeof(double)) 
                finalVal = Math.Max(def.MinValue ?? double.MinValue, Math.Min(def.MaxValue ?? double.MaxValue, newVal));
            else 
                finalVal = Convert.ChangeType(newVal, t);

            def.Setter(_owner, finalVal);
        }

        #endregion
    }
}
