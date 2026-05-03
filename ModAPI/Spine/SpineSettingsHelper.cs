using System;
using System.Collections.Generic;
using System.Reflection;
using System.Linq;
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
            
            MMLog.WriteDebug($"Scanning {type.Name} for settings...");

            // Scan Fields
            foreach (var field in type.GetFields(flags))
            {
                try
                {
                    // 1. Try standard [ModSetting]
                    var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(field, typeof(ModSettingAttribute));
                    if (attr != null)
                    {
                        var def = SettingDefinitionFactory.Create(attr, field, type);
                        SettingDefinitionFactory.ApplyPresets(field, def);
                        definitions.Add(def);
                        continue;
                    }


                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"Error scanning field '{field.Name}' in '{type.Name}': {ex}");
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
                        var def = SettingDefinitionFactory.Create(attr, prop, type);
                        SettingDefinitionFactory.ApplyPresets(prop, def);
                        definitions.Add(def);
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"Error scanning property '{prop.Name}' in '{type.Name}': {ex}");
                }
            }

            // Scan Methods (Action Buttons)
            foreach (var method in type.GetMethods(flags))
            {
                var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(method, typeof(ModSettingAttribute));
                if (attr != null && method.GetParameters().Length == 0)
                {
                    var def = SettingDefinitionFactory.Create(attr, method, type);
                    def.Type = SettingType.Button; 
                    def.OnChanged = (obj) => method.Invoke(obj, null);
                    definitions.Add(def);
                }
            }

            MMLog.Write($"Scan complete for {type.Name}. Found {definitions.Count} definitions.");
            // Sort by order
            definitions.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
            return definitions;
        }
    }
}
