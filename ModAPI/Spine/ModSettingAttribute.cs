using System;
using UnityEngine;

namespace ModAPI.Spine
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class ModSettingAttribute : Attribute
    {
        /// <summary>The display name shown in the UI.</summary>
        public string Label;
        
        /// <summary>Text shown when hovering over the setting name/widget.</summary>
        public string Tooltip;
        
        /// <summary>Whether this setting appears in Simple or Advanced view (default: Advanced).</summary>
        public SettingMode Mode = SettingMode.Advanced;
        
        /// <summary>If true, other mods can modify this setting via ModSettingsDatabase.</summary>
        public bool AllowExternalWrite = false;
        
        /// <summary>Minimum value for numeric sliders.</summary>
        public float MinValue = 0f;
        
        /// <summary>Maximum value for numeric sliders.</summary>
        public float MaxValue = 100f;
        
        /// <summary>Step increment for sliders/buttons. 0 uses defaults (1 for int, 0.1 for float).</summary>
        public float StepSize = 0f; 
        
        /// <summary>Group settings into collapsible sections. Items with the same Category are grouped together.</summary>
        public string Category;
        
        /// <summary>Controls vertical sorting. Lower numbers appear first. Default is 0.</summary>
        public int SortOrder = 0;
        
        /// <summary>ID of another Boolean setting that must be TRUE for this setting to be eligible/enabled.</summary>
        public string DependsOnId;
        
        /// <summary>If this is a Boolean setting, setting this to true will hide all dependent children when this is FALSE.</summary>
        public bool ControlsChildVisibility = false;
        
        /// <summary>If true, shows a "Restart Required" warning when changed.</summary>
        public bool RequiresRestart = false;
        
        // Advanced Hooks (Method names)
        
        /// <summary>Force a specific widget type (e.g., use numeric input instead of slider).</summary>
        public SettingType Type = SettingType.Unknown;
        
        /// <summary>Hex color (e.g., "#FF0000") for Header widgets.</summary>
        public string HeaderColor;
        
        /// <summary>Name of a method/property returning bool to determine runtime visibility.</summary>
        public string VisibilityMethod;
        
        /// <summary>Name of a method/property returning IEnumerable&lt;string&gt; for Choice widgets.</summary>
        public string OptionsSource;
        
        /// <summary>Name of a method (object defined -> bool) used to validate input before applying.</summary>
        public string ValidateMethod;

        /// <summary>
        /// Marks a field, property, or method as a configurable setting in the Spine UI.
        /// </summary>
        /// <param name="label">The display text for the setting.</param>
        public ModSettingAttribute(string label)
        {
            Label = label;
        }
    }
    
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
    public class ModSettingPresetAttribute : Attribute
    {
        /// <summary>The display name for the preset option (e.g. "Easy", "Hard").</summary>
        public string PresetName;
        
        /// <summary>The value this field should take when the preset is selected.</summary>
        public object Value;

        /// <summary>
        /// Defines a preset value for this setting, allowing it to be controlled by the global preset bar.
        /// </summary>
        public ModSettingPresetAttribute(string name, object value)
        {
            PresetName = name;
            Value = value;
        }
    }

    /// <summary>
    /// Inherit from this to handle your own NGUI drawing entirely, skipping the auto-grid.
    /// </summary>
    public interface ICustomSettingsUI
    {
        /// <summary>
        /// Called when this mod's settings should be drawn. 
        /// The mod is responsible for creating all UI widgets under 'parent'.
        /// </summary>
        void DrawSettings(GameObject parent, float width, float height);
    }
}
