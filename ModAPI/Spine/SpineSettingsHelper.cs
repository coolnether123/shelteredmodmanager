using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Spine
{
    /// <summary>
    /// Utility for scanning classes for ModSetting attributes and generating definitions.
    /// </summary>
    public static class SpineSettingsHelper
    {
        /// <summary>
        /// Scans an object instance for fields and properties marked with [ModSetting] 
        /// and converts them into a list of SettingDefinitions.
        /// </summary>
        public static List<SettingDefinition> Scan(object settingsObject)
        {
            var definitions = new List<SettingDefinition>();
            if (settingsObject == null) return definitions;

            var type = settingsObject.GetType();
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            
            MMLog.WriteDebug($"[Spine] Scanning {type.Name} for settings...");

            // Scan Fields
            foreach (var field in type.GetFields(flags))
            {
                try
                {
                    var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(field, typeof(ModSettingAttribute));
                    if (attr != null)
                    {
                        var def = CreateDefinition(attr, field.Name, field.FieldType, type);
                        // Scan for presets
                        var presets = (ModSettingPresetAttribute[])Attribute.GetCustomAttributes(field, typeof(ModSettingPresetAttribute));
                        foreach (var p in presets) def.Presets[p.PresetName] = p.Value;
                        definitions.Add(def);
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"[Spine] Error scanning field '{field.Name}' in '{type.Name}': {ex}");
                }
            }
            

            // Scan Properties
            foreach (var prop in type.GetProperties(flags))
            {
                try
                {
                    var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(prop, typeof(ModSettingAttribute));
                    if (attr != null)
                    {
                        var def = CreateDefinition(attr, prop.Name, prop.PropertyType, type);
                        var presets = (ModSettingPresetAttribute[])Attribute.GetCustomAttributes(prop, typeof(ModSettingPresetAttribute));
                        foreach (var p in presets) def.Presets[p.PresetName] = p.Value;
                        definitions.Add(def);
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"[Spine] Error scanning property '{prop.Name}' in '{type.Name}': {ex}");
                }
            }

            // Scan Methods (Action Buttons)
            foreach (var method in type.GetMethods(flags))
            {
                var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(method, typeof(ModSettingAttribute));
                if (attr != null && method.GetParameters().Length == 0)
                {
                    var def = CreateDefinition(attr, method.Name, typeof(void), type);
                    def.Type = SettingType.Button; 
                    def.OnChanged = (obj) => method.Invoke(obj, null);
                    definitions.Add(def);
                }
            }

            MMLog.Write($"[Spine] Scan complete for {type.Name}. Found {definitions.Count} definitions.");
            // Sort by order
            definitions.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return definitions;
        }

        private static SettingDefinition CreateDefinition(ModSettingAttribute attr, string memberName, Type memberType, Type settingsType)
        {
            var def = new SettingDefinition
            {
                Id = memberName, 
                FieldName = memberName,
                Label = attr.Label ?? memberName,
                Tooltip = attr.Tooltip,
                Mode = attr.Mode,
                AllowExternalWrite = attr.AllowExternalWrite,
                MinValue = attr.MinValue,
                MaxValue = attr.MaxValue,
                StepSize = attr.StepSize,
                Category = attr.Category,
                SortOrder = attr.SortOrder,
                DependsOnId = attr.DependsOnId,
                ControlsChildVisibility = attr.ControlsChildVisibility,
                RequiresRestart = attr.RequiresRestart,
                HeaderColor = string.IsNullOrEmpty(attr.HeaderColor) ? (Color?)null : ParseColor(attr.HeaderColor)
            };

            // Wire up VisibleWhen logic from attribute
            if (!string.IsNullOrEmpty(attr.VisibilityMethod))
            {
                def.VisibleWhen = (obj) => {
                    try {
                        var m = settingsType.GetMethod(attr.VisibilityMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (m != null) return (bool)m.Invoke(obj, null);
                        var p = settingsType.GetProperty(attr.VisibilityMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (p != null) return (bool)p.GetValue(obj, null);
                        MMLog.WriteError($"[Spine] VisibilityMethod '{attr.VisibilityMethod}' not found on {settingsType.Name}");
                        return true;
                    } catch (Exception ex) {
                        MMLog.WriteError($"[Spine] Error executing VisibilityMethod '{attr.VisibilityMethod}': {ex}");
                        return true;
                    }
                };
            }

            // Wire up OptionsSource logic
            if (!string.IsNullOrEmpty(attr.OptionsSource))
            {
                def.GetOptions = (obj) => {
                    try {
                        var m = settingsType.GetMethod(attr.OptionsSource, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (m != null) return (IEnumerable<string>)m.Invoke(obj, null);
                        var p = settingsType.GetProperty(attr.OptionsSource, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (p != null) return (IEnumerable<string>)p.GetValue(obj, null);
                        MMLog.WriteError($"[Spine] OptionsSource '{attr.OptionsSource}' not found on {settingsType.Name}");
                        return Enumerable.Empty<string>();
                    } catch (Exception ex) {
                        MMLog.WriteError($"[Spine] Error executing OptionsSource '{attr.OptionsSource}': {ex}");
                        return Enumerable.Empty<string>();
                    }
                };
            }

            // Wire up Validate logic
            if (!string.IsNullOrEmpty(attr.ValidateMethod))
            {
                def.Validate = (newVal, obj) => {
                    try {
                        var m = settingsType.GetMethod(attr.ValidateMethod, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        if (m != null) return (bool)m.Invoke(obj, new[] { newVal });
                        MMLog.WriteError($"[Spine] ValidateMethod '{attr.ValidateMethod}' not found on {settingsType.Name}");
                        return true;
                    } catch (Exception ex) {
                        MMLog.WriteError($"[Spine] Error executing ValidateMethod '{attr.ValidateMethod}': {ex}");
                        return true;
                    }
                };
            }

            // Map standard C# types to SettingTypes
            if (attr.Type != SettingType.Unknown)
            {
                def.Type = attr.Type;
            }
            else
            {
                if (memberType == typeof(bool)) def.Type = SettingType.Bool;
                else if (memberType == typeof(int)) def.Type = SettingType.Int;
                else if (memberType == typeof(float)) def.Type = SettingType.Float;
                else if (memberType == typeof(string)) def.Type = SettingType.String;
                else if (memberType == typeof(Color)) def.Type = SettingType.Color;
                else if (memberType.IsEnum)
                {
                    def.Type = SettingType.Enum;
                    def.EnumType = memberType;
                }
            }

            return def;
        }

        private static Color ParseColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return Color.white;
            try
            {
                hex = hex.TrimStart('#');
                if (hex.Length < 6) return Color.white;
                float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float a = hex.Length >= 8 ? int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f : 1f;
                return new Color(r, g, b, a);
            }
            catch { return Color.white; }
        }
    }
}
