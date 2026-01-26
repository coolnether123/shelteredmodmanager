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
}
