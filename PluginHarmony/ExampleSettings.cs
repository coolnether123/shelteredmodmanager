using System;
using ModAPI.Spine;
using UnityEngine;

namespace PluginHarmony
{
    public class ExampleSettings
    {
        // --- VISUAL SETTINGS ---
        
        [ModSetting("VISUAL CONFIGURATION", Type = SettingType.Header, HeaderColor = "FFD700")] // Gold Header
        public bool HeaderVisuals; // Dummy field for header

        [ModSetting("Enable Visual Patches", Tooltip = "Toggle the main menu label modification.", Mode = SettingMode.Both)]
        public bool EnableVisuals = true;

        [ModSetting("Menu Label Text")]
        public string MenuLabel = "Harmony Active";

        [ModSetting("Label Color")]
        public Color LabelColor = Color.cyan;

        [ModSetting("Label Vertical Offset", MinValue = -100f, MaxValue = 400f, Mode = SettingMode.Advanced)]
        public float LabelYOffset = 0f;

        // --- GAMEPLAY SETTINGS ---

        [ModSetting("GAMEPLAY TWEAKS", Type = SettingType.Header, HeaderColor = "00FF7F")] // Spring Green Header
        public bool HeaderGameplay;

        [ModSetting("Enable Gameplay Patches", Tooltip = "Master toggle for inventory and interaction tweaks.", Mode = SettingMode.Both)]
        public bool EnableGameplay = true;

        [ModSetting("Difficulty Preset", Tooltip = "Controls overall game balance.", Mode = SettingMode.Simple)]
        public DifficultyLevel Difficulty = DifficultyLevel.Normal;

        [ModSetting("Custom Crafting Recipes", Tooltip = "Injects a custom Hinge recipe into the Tier 1 workbench.", Mode = SettingMode.Both)]
        public bool EnableRecipes = true;

        // --- DEBUG SETTINGS ---

        [ModSetting("DEBUG TOOLS", Type = SettingType.Header, HeaderColor = "FF4500")] // Orange Red Header
        public bool HeaderDebug;

        [ModSetting("Log Inventory Changes", Tooltip = "Prints to console whenever an item is added to inventory.", Mode = SettingMode.Advanced)]
        public bool LogInventory = false;

        [ModSetting("Log Expedition Route", Tooltip = "Periodically logs the length of the current travel route.", Mode = SettingMode.Advanced)]
        public bool LogRoute = false;

        [ModSetting("God Mode Interaction", Tooltip = "Allows using objects even if they are 100% damaged.", Mode = SettingMode.Advanced)]
        public bool GodMode = false;
    }

    public enum DifficultyLevel
    {
        Easy,
        Normal,
        Hard
    }
}