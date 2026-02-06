using System.Collections.Generic;

namespace ModAPI.Spine
{
    /// <summary>
    /// Interface for mods to provide their settings to the Mod Manager.
    /// </summary>
    public interface ISettingsProvider
    {
        /// <summary>
        /// Returns a list of SettingDefinitions. Usually implemented by calling <see cref="SpineSettingsHelper.Scan(object)"/>.
        /// </summary>
        IEnumerable<SettingDefinition> GetSettings();
        
        /// <summary>
        /// Returns the object instance that holds the setting values (likely 'this' or a specific settings POCO).
        /// </summary>
        object GetSettingsObject();
        
        /// <summary>
        /// Optional hook called after settings have been loaded from disk but before the mod starts.
        /// </summary>
        void OnSettingsLoaded(); 
        
        /// <summary>
        /// Called when the user clicks 'Defaults'. Implementation should reset the settings object to its initial state.
        /// </summary>
        void ResetToDefaults();
    }

    /// <summary>
    /// Extended interface for version 1.2+ settings providers.
    /// </summary>
    public interface ISettingsProvider2 : ISettingsProvider
    {
        /// <summary>Writes all dirty settings to disk using atomic operations.</summary>
        void Save();
        
        /// <summary>True if the controller has finished scanning and loading defaults.</summary>
        bool IsReady { get; }

        /// <summary>Returns a manual JSON representation of the current settings state.</summary>
        string SerializeToJson();
    }

    public static class SettingsProviderExtensions
    {
        public static bool GetBool(this ISettingsProvider provider, string id, bool defaultValue = false)
        {
            if (provider == null) return defaultValue;
            var obj = provider.GetSettingsObject();
            if (obj == null) return defaultValue;

            var type = obj.GetType();
            var field = type.GetField(id, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field != null && field.FieldType == typeof(bool)) return (bool)field.GetValue(obj);

            var prop = type.GetProperty(id, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (prop != null && prop.PropertyType == typeof(bool) && prop.CanRead) return (bool)prop.GetValue(obj, null);

            return defaultValue;
        }
    }
}

