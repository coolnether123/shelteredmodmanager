using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
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
    public class SettingsController : ISettingsProvider, ISettingsProvider2
    {
        private readonly object _owner;
        private readonly ModEntry _mod;
        private readonly IPluginContext _context;
        private readonly List<SettingDefinition> _definitions;
        private readonly Dictionary<string, SettingDefinition> _defById;
        
        private bool _isDirty;
        private float _lastWriteTime;
        private string _serializedCache;
        private const float DebounceTime = 2.0f;

        public bool IsReady { get; private set; }

        public SettingsController(IPluginContext context, object owner)
        {
            _context = context;
            _mod = context.Mod;
            _owner = owner;
            _definitions = Scan(owner);
            _defById = _definitions.ToDictionary(d => d.Id);
            IsReady = true;
        }

        public IEnumerable<SettingDefinition> GetSettings() => _definitions;
        public object GetSettingsObject() => _owner;
        public string SerializeToJson() => SerializeJsonInternal();

        public void OnSettingsLoaded()
        {
            ModManagerBase manager = _owner as ModManagerBase;
            if (manager != null) manager.OnSettingsLoaded();
        }

        public void ResetToDefaults()
        {
            foreach (var def in _definitions)
            {
                if (def.Setter != null) def.Setter(_owner, def.DefaultValue);
            }
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
                    var def = CreateDefinition(attr, field);
                    def.Getter = CreateGetter(field);
                    def.Setter = CreateSetter(field);
                    def.DefaultValue = def.Getter(owner);
                    ProcessPresets(field, def);
                    definitions.Add(def);
                }
            }

            // Properties
            foreach (var prop in type.GetProperties(flags))
            {
                var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(prop, typeof(ModSettingAttribute));
                if (attr != null && prop.CanRead && prop.CanWrite)
                {
                    var def = CreateDefinition(attr, prop);
                    def.Getter = CreateGetter(prop);
                    def.Setter = CreateSetter(prop);
                    def.DefaultValue = def.Getter(owner);
                    ProcessPresets(prop, def);
                    definitions.Add(def);
                }
            }

            // Methods (Buttons)
            foreach (var method in type.GetMethods(flags))
            {
                var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(method, typeof(ModSettingAttribute));
                if (attr != null && method.GetParameters().Length == 0)
                {
                    var def = CreateDefinition(attr, method);
                    def.Type = SettingType.Button;
                    def.OnChanged = (obj) => method.Invoke(owner, null);
                    definitions.Add(def);
                }
            }

            return definitions.OrderBy(d => d.SortOrder).ThenBy(d => d.Label).ToList();
        }

        private SettingDefinition CreateDefinition(ModSettingAttribute attr, MemberInfo member)
        {
            Type memberType = (member is FieldInfo f) ? f.FieldType : (member is PropertyInfo p) ? p.PropertyType : typeof(void);
            
            var def = new SettingDefinition
            {
                Id = member.Name,
                FieldName = member.Name,
                Label = attr.Label ?? member.Name,
                Tooltip = attr.Tooltip,
                Mode = attr.Mode,
                Scope = attr.Scope,
                CarryOverToNewGamePlus = attr.CarryOverToNewGamePlus,
                NewGamePlusMerge = attr.NewGamePlusMerge,
                AllowExternalWrite = attr.AllowExternalWrite,
                MinValue = attr.MinValue,
                MaxValue = attr.MaxValue,
                StepSize = attr.StepSize,
                Category = attr.Category,
                SortOrder = attr.SortOrder,
                DependsOnId = attr.DependsOnId,
                ControlsChildVisibility = attr.ControlsChildVisibility,
                RequiresRestart = attr.RequiresRestart,
                SyncMode = attr.SyncMode
            };

            // Map Types
            if (attr.Type != SettingType.Unknown) def.Type = attr.Type;
            else if (memberType == typeof(bool)) def.Type = SettingType.Bool;
            else if (memberType == typeof(int)) def.Type = SettingType.Int;
            else if (memberType == typeof(float)) def.Type = SettingType.Float;
            else if (memberType == typeof(string)) def.Type = SettingType.String;
            else if (memberType.IsEnum) { def.Type = SettingType.Enum; def.EnumType = memberType; }

            return def;
        }

        private void ProcessPresets(MemberInfo member, SettingDefinition def)
        {
            var presets = Attribute.GetCustomAttributes(member, typeof(ModSettingPresetAttribute));
            if (presets != null)
            {
                foreach (var attr in presets)
                {
                    ModSettingPresetAttribute p = attr as ModSettingPresetAttribute;
                    if (p != null)
                    {
                        def.Presets[p.PresetName] = p.Value;
                    }
                }
            }
        }

        private Func<object, object> CreateGetter(MemberInfo member)
        {
            var targetParam = Expression.Parameter(typeof(object), "target");
            Expression body;
            if (member is FieldInfo field) body = Expression.Field(Expression.Convert(targetParam, field.DeclaringType), field);
            else if (member is PropertyInfo prop) body = Expression.Property(Expression.Convert(targetParam, prop.DeclaringType), prop);
            else return null;

            return Expression.Lambda<Func<object, object>>(Expression.Convert(body, typeof(object)), targetParam).Compile();
        }

        private Action<object, object> CreateSetter(MemberInfo member)
        {
            if (member is FieldInfo field)
            {
                return (target, value) => {
                    try { field.SetValue(target, value); }
                    catch (Exception ex) { MMLog.WriteError("Error setting field " + field.Name + ": " + ex.Message); }
                };
            }
            
            if (member is PropertyInfo prop)
            {
                var setMethod = prop.GetSetMethod(true);
                if (setMethod == null) return null;

                var targetParam = Expression.Parameter(typeof(object), "target");
                var valueParam = Expression.Parameter(typeof(object), "value");
                Type memberType = prop.PropertyType;

                Expression body = Expression.Call(
                    Expression.Convert(targetParam, prop.DeclaringType),
                    setMethod,
                    Expression.Convert(valueParam, memberType)
                );

                return Expression.Lambda<Action<object, object>>(body, targetParam, valueParam).Compile();
            }

            return null;
        }

        #endregion

        #region Persistence

        public void Load()
        {
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
            WriteToDisk(SettingsScope.Global);
            WriteToDisk(SettingsScope.PerSave);
            _isDirty = false;
        }

        private void WriteToDisk(SettingsScope scope)
        {
            string path = GetPath(scope);
            if (string.IsNullOrEmpty(path)) return;

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
                    break;
                }
                catch (IOException)
                {
                    retries--;
                    if (retries == 0) throw;
                    System.Threading.Thread.Sleep(100);
                }
            }
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
                sb.Append($"\"{def.Id}\":{ValueToJson(def.Getter(_owner))}");
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
            foreach (var def in _definitions.Where(d => d.Scope == scope))
            {
                if (def.Type == SettingType.Button) continue;
                if (!first) sb.Append(",");
                sb.Append($"\"{def.Id}\":{ValueToJson(def.Getter(_owner))}");
                first = false;
            }
            sb.Append("}");
            return sb.ToString();
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
                res.Append($"\"{f.Name}\":{ValueToJson(f.GetValue(obj))}");
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
                if (_defById.TryGetValue(kvp.Key, out var def) && def.Scope == scope)
                {
                    try
                    {
                        object val = ConvertValue(kvp.Value, def.Getter(_owner).GetType());
                        def.Setter(_owner, val);
                    }
                    catch (Exception ex) { MMLog.WriteError($"Failed to apply setting {kvp.Key}: {ex.Message}"); }
                }
            }
        }

        private Dictionary<string, object> ParseJson(string json)
        {
            // Extremely simplified JSON parser for flat KV pairs. 
            // In a real implementation, this would be a full recursive descent parser.
            var results = new Dictionary<string, object>();
            int i = 0;
            while (i < json.Length)
            {
                if (json[i] == '\"')
                {
                    string key = ReadString(json, ref i);
                    while (i < json.Length && json[i] != ':') i++;
                    i++; // skip colon
                    object val = ReadValue(json, ref i);
                    results[key] = val;
                }
                i++;
            }
            return results;
        }

        private string ReadString(string json, ref int i)
        {
            i++; // skip start quote
            var sb = new StringBuilder();
            while (i < json.Length && json[i] != '\"')
            {
                if (json[i] == '\\' && i + 1 < json.Length) { i++; sb.Append(json[i]); }
                else sb.Append(json[i]);
                i++;
            }
            return sb.ToString();
        }

        private object ReadValue(string json, ref int i)
        {
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            if (i >= json.Length) return null;

            if (json[i] == '\"') return ReadString(json, ref i);
            if (json[i] == 't') { i += 3; return true; }
            if (json[i] == 'f') { i += 4; return false; }
            if (json[i] == 'n') { i += 3; return null; }
            
            // Number
            int start = i;
            while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' || json[i] == '-' || json[i] == '+' || json[i] == 'e' || json[i] == 'E')) i++;
            string s = json.Substring(start, i - start);
            i--; // back up for outer loop
            return s;
        }

        private object ConvertValue(object val, Type targetType)
        {
            if (val == null) return null;
            if (targetType.IsEnum) return Enum.Parse(targetType, val.ToString());
            if (targetType == typeof(bool)) return val.ToString().ToLower() == "true";
            if (targetType == typeof(int)) return int.Parse(val.ToString(), CultureInfo.InvariantCulture);
            if (targetType == typeof(float)) return float.Parse(val.ToString(), CultureInfo.InvariantCulture);
            if (targetType == typeof(double)) return double.Parse(val.ToString(), CultureInfo.InvariantCulture);
            if (targetType == typeof(long)) return long.Parse(val.ToString(), CultureInfo.InvariantCulture);
            return val;
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
                        var rawValue = ReadValue(carryJson, ref (new int[]{0}[0])); // Hacky way to pass 0 by ref
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
