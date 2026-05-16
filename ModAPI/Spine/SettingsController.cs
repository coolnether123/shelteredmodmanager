using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using ModAPI.Core;
using ModAPI.Util;

namespace ModAPI.Spine
{
    /// <summary>
    /// Core manager for ModAPI settings. Replaces ModSettings and AutoSettingsProvider.
    /// Handles runtime-safe JSON serialization, delegate caching, and New Game+ logic.
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
                return def.Scope == SettingsScope.Global
                    ? TryApplyAndPersist(def, converted, SettingsScope.Global)
                    : TryPersistGlobalDefault(def, converted);
            }

            if (target == SettingsWriteTarget.ActiveSave)
            {
                return def.Scope == SettingsScope.PerSave
                    ? TryApplyAndPersist(def, converted, SettingsScope.PerSave)
                    : TryApplyAndPersist(def, converted, SettingsScope.Global);
            }

            return false;
        }

        private bool TryPersistGlobalDefault(SettingDefinition def, object value)
        {
            if (def == null)
                return false;

            object previous;
            bool hadPrevious = _loadedGlobalValues.TryGetValue(def.Id, out previous);
            _loadedGlobalValues[def.Id] = value;

            if (TryWriteScope(SettingsScope.Global, def.Id))
                return true;

            if (hadPrevious)
                _loadedGlobalValues[def.Id] = previous;
            else
                _loadedGlobalValues.Remove(def.Id);

            return false;
        }

        private bool TryApplyAndPersist(SettingDefinition def, object value, SettingsScope scope)
        {
            if (def == null)
                return false;

            if (!CanResolvePath(scope))
                return false;

            object previous = null;
            bool hadPrevious = false;
            if (def.Getter != null)
            {
                try
                {
                    previous = def.Getter(_owner);
                    hadPrevious = true;
                }
                catch
                {
                    hadPrevious = false;
                }
            }

            try
            {
                ApplySettingValue(def, value);
                if (!DidApplySettingValue(def, value))
                {
                    RestoreSettingValue(def, previous, hadPrevious);
                    return false;
                }

                if (!TryWriteScope(scope, def.Id))
                {
                    RestoreSettingValue(def, previous, hadPrevious);
                    return false;
                }

                CapturePersistedValue(def, scope, value);
                NotifySettingChanged(def);
                return true;
            }
            catch (Exception ex)
            {
                RestoreSettingValue(def, previous, hadPrevious);
                MMLog.WriteError("Failed to persist setting '" + def.Id + "': " + ex.Message);
                return false;
            }
        }

        private bool CanResolvePath(SettingsScope scope)
        {
            try
            {
                return !string.IsNullOrEmpty(GetPath(scope));
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Failed to resolve settings path for " + scope + ": " + ex.Message);
                return false;
            }
        }

        private bool TryWriteScope(SettingsScope scope, string settingId)
        {
            try
            {
                return WriteToDisk(scope);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Failed to write setting '" + settingId + "' to " + scope + " settings: " + ex.Message);
                return false;
            }
        }

        private void CapturePersistedValue(SettingDefinition def, SettingsScope scope, object value)
        {
            if (scope == SettingsScope.Global)
                _loadedGlobalValues[def.Id] = value;
            else if (scope == SettingsScope.PerSave)
                _loadedPerSaveValues[def.Id] = value;
        }

        private void RestoreSettingValue(SettingDefinition def, object previous, bool hasPrevious)
        {
            if (hasPrevious)
                ApplySettingValue(def, previous);
        }

        private bool DidApplySettingValue(SettingDefinition def, object value)
        {
            if (def == null || def.Getter == null)
                return true;

            try
            {
                return ValuesEqual(def.Getter(_owner), value);
            }
            catch
            {
                return false;
            }
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
            string modId = GetModId();
            if (string.IsNullOrEmpty(modId))
                return null;

            if (scope == SettingsScope.Global)
            {
                // UserRoot is already Mods/ModAPI/User
                string modFolder = Path.Combine(ModPrefs.UserRoot, modId);
                if (!Directory.Exists(modFolder)) Directory.CreateDirectory(modFolder);
                return Path.Combine(modFolder, "settings.json");
            }
            else
            {
                // Root/Saves/Slot_X/mods/{ModId}/settings.json
                if (_context == null || _context.SaveSystem == null) return null;
                string slotPath = _context.SaveSystem.GetCurrentSlotPath();
                if (string.IsNullOrEmpty(slotPath)) return null;

                string modDataFolder = Path.Combine(Path.Combine(slotPath, "mods"), modId);
                if (!Directory.Exists(modDataFolder)) Directory.CreateDirectory(modDataFolder);
                return Path.Combine(modDataFolder, "settings.json");
            }
        }

        private string GetModId()
        {
            return _mod != null ? _mod.Id : null;
        }


        #endregion

        #region Settings JSON

        private string SerializeJsonInternal()
        {
            ManualJsonObject root = new ManualJsonObject();
            foreach (var def in _definitions)
            {
                if (def.Type == SettingType.Button) continue;
                root.Set(def.Id, ToJsonValue(def.Getter(_owner), new HashSet<object>()));
            }

            return ManualJson.Serialize(root, false);
        }


        private string SerializeScope(SettingsScope scope)
        {
            ManualJsonObject root = new ManualJsonObject();
            foreach (var def in _definitions)
            {
                if (def.Type == SettingType.Button) continue;

                object value;
                if (!TryGetValueForPersistedScope(def, scope, out value))
                    continue;

                root.Set(def.Id, ToJsonValue(value, new HashSet<object>()));
            }

            return ManualJson.Serialize(root, false);
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
            return ManualJson.Serialize(ToJsonValue(val, new HashSet<object>()), false);
        }

        private ManualJsonValue ToJsonValue(object val, HashSet<object> seen)
        {
            if (val == null) return ManualJsonValue.Null();
            if (val is bool b) return ManualJsonValue.Boolean(b);
            if (val is string s) return ManualJsonValue.String(s);
            if (val is float f) return ManualJsonValue.Number(f.ToString("R", CultureInfo.InvariantCulture));
            if (val is double d) return ManualJsonValue.Number(d.ToString("R", CultureInfo.InvariantCulture));
            if (val is int i) return ManualJsonValue.Number(i);
            if (val is long l) return ManualJsonValue.Number(l);
            if (val.GetType().IsEnum) return ManualJsonValue.String(val.ToString());

            if (seen.Contains(val)) throw new InvalidOperationException("Circular reference detected in settings.");
            seen.Add(val);

            try
            {
                if (val is IEnumerable en && !(val is string))
                {
                    ManualJsonArray array = new ManualJsonArray();
                    foreach (var item in en)
                    {
                        array.Add(ToJsonValue(item, seen));
                    }

                    return ManualJsonValue.Array(array);
                }

                Type type = val.GetType();
                ManualJsonObject obj = new ManualJsonObject();
                FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
                for (int fieldIndex = 0; fieldIndex < fields.Length; fieldIndex++)
                {
                    FieldInfo field = fields[fieldIndex];
                    obj.Set(field.Name, ToJsonValue(field.GetValue(val), seen));
                }

                return ManualJsonValue.Object(obj);
            }
            finally
            {
                seen.Remove(val);
            }
        }

        #endregion

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
            object root = ParseJsonValue(json);
            Dictionary<string, object> result = root as Dictionary<string, object>;
            return result ?? new Dictionary<string, object>();
        }

        private object ParseJsonValue(string json)
        {
            ManualJsonValue value;
            string error;
            if (!ManualJson.TryParse(json, out value, out error))
            {
                MMLog.WriteWarning("Settings JSON parse failed: " + error);
                return null;
            }

            return ManualJson.ToObjectGraph(value);
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
                        var rawValue = ParseJsonValue(carryJson);
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
