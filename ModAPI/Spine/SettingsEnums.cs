using System;

namespace ModAPI.Spine
{
    /// <summary>
    /// Determines where the setting is stored and how it persists.
    /// </summary>
    public enum SettingsScope
    {
        /// <summary>Stored in mods/ModAPI/User/{ModId}/settings.json. Persists across all saves.</summary>
        Global,
        /// <summary>Stored in save slot mod_data. Scoped to the current game/save.</summary>
        PerSave
    }

    /// <summary>
    /// Strategy for merging settings during New Game+ carry-over.
    /// </summary>
    public enum MergeStrategy
    {
        /// <summary>The new value completely replaces the old value (Default).</summary>
        Replace,
        /// <summary>The new value is added to the old value (Numeric types only).</summary>
        Add,
        /// <summary>The new value is multiplied by the old value (Numeric types only).</summary>
        Multiply
    }

    /// <summary>
    /// Categorization for UI display in the Mod Management panel.
    /// </summary>
    public enum SettingMode
    {
        Simple,
        Advanced,
        Both
    }

    /// <summary>
    /// The type of widget to use for the setting in the UI.
    /// </summary>
    public enum SettingType
    {
        Unknown,
        Bool,
        Int,
        Float,
        String,
        Enum,
        Color,
        Button,
        Header,
        Spacer,
        NumericInt,
        Choice
    }

}
