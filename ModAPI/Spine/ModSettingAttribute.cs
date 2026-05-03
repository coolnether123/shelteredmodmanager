using System;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Spine
{
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property | AttributeTargets.Method)]
    public class ModSettingAttribute : Attribute
    {
        /// <summary>The display name shown in the UI.</summary>
        public string Label;

        /// <summary>Display text key supplied by the active runtime. If provided, Label is used as fallback.</summary>
        public string LabelKey;
        
        /// <summary>Text shown when hovering over the setting name/widget.</summary>
        public string Tooltip;

        /// <summary>Where this setting is stored (Global vs PerSave).</summary>
        public SettingsScope Scope = SettingsScope.Global;
        
        /// <summary>Whether this setting appears in Simple or Advanced view (default: Advanced).</summary>
        public SettingMode Mode = SettingMode.Advanced;
        
        /// <summary>If true, other mods can modify this setting via ModSettingsDatabase.</summary>
        public bool AllowExternalWrite = false;
        
        /// <summary>Minimum value for numeric sliders.</summary>
        public float MinValue = 0f;
        
        /// <summary>Maximum value for numeric sliders.</summary>
        public float MaxValue = 100f;
        
        /// <summary>Step increment for +/- buttons, and for sliders when SliderStepMode is Stepped.</summary>
        public float StepSize = 0f; 

        /// <summary>Whether slider dragging is granular or snapped to StepSize. Defaults to granular.</summary>
        public SliderStepMode SliderStepMode = SliderStepMode.Granular;

        /// <summary>Numeric display format, such as "0.0", "P0", or "0.###".</summary>
        public string ValueFormat;

        /// <summary>Text appended after displayed values, such as "%", " days", or "x".</summary>
        public string UnitSuffix;

        /// <summary>Text shown for true boolean values. Defaults to ON.</summary>
        public string TrueLabel;

        /// <summary>Text shown for false boolean values. Defaults to OFF.</summary>
        public string FalseLabel;

        /// <summary>Text shown on action button widgets. Defaults to Execute.</summary>
        public string ActionLabel;

        /// <summary>Placeholder text shown for empty string settings.</summary>
        public string Placeholder;

        /// <summary>Whether numeric settings expose direct text entry in addition to dragging and buttons.</summary>
        public bool ShowValueInput = true;

        /// <summary>Whether numeric settings show +/- stepper buttons.</summary>
        public bool ShowStepperButtons = true;

        /// <summary>Optional small step used by +/- buttons. Falls back to StepSize or range-derived defaults.</summary>
        public float FineStepSize = 0f;

        /// <summary>Optional larger step used by +/- buttons while Shift is held.</summary>
        public float LargeStepSize = 0f;
        
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

        /// <summary>If true, this PerSave setting carries over to New Game+.</summary>
        public bool CarryOverToNewGamePlus = false;

        /// <summary>How to merge this setting during New Game+ carry-over.</summary>
        public MergeStrategy NewGamePlusMerge = MergeStrategy.Replace;
        
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

        /// <summary>Minimum value for numeric sliders. Proxy for MinValue.</summary>
        public float Min { get { return MinValue; } set { MinValue = value; } }

        /// <summary>Maximum value for numeric sliders. Proxy for MaxValue.</summary>
        public float Max { get { return MaxValue; } set { MaxValue = value; } }

        /// <summary>Name of the method to call when the value changes.</summary>
        public string OnChanged;

        /// <summary>Multiplayer synchronization mode.</summary>
        public SyncMode SyncMode = SyncMode.LocalOnly;

        /// <summary>
        /// Marks a field, property, or method as a configurable setting in the Spine UI.
        /// </summary>
        /// <param name="label">The display text for the setting.</param>
        public ModSettingAttribute(string label)
        {
            Label = label;
        }

        public ModSettingAttribute() { }
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
    /// Inherit from this to handle your own host UI drawing entirely, skipping the auto-grid.
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

