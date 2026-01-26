using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Spine;
using ModAPI.Harmony;

namespace ModAPI.Examples
{
    /// <summary>
    /// COMPREHENSIVE SHOWCASE: Spine Settings + Harmony Patching.
    /// Demonstrates all Spine widget types, dependency logic, presets, and runtime patching.
    /// Following SOLID principles: Settings data is decoupled from logic.
    /// </summary>
    public class ShowcaseSettings
    {
        // ---------------------------------------------------------------------
        // 1. Attribute-driven configuration (Metadata for discovery)
        // ---------------------------------------------------------------------

        [ModSetting("Enable Mod Features", Tooltip = "Main master switch for this mod.", Mode = SettingMode.Both)]
        public bool GlobalEnable = true;

        [ModSetting("Heading Text", Mode = SettingMode.Both, Tooltip = "Text to display in the main menu label.")]
        public string MenuLabel = "SPINE SETTINGS ACTIVE";

        [ModSetting("UI Color", Mode = SettingMode.Both, Tooltip = "Direct color control via Spine color picker.")]
        public Color ThemeColor = new Color(0.2f, 0.8f, 1.0f);

        [ModSetting("Vertical Position", MinValue = -400f, MaxValue = 400f, Mode = SettingMode.Advanced)]
        public float VerticalOffset = 250f;

        [ModSetting("Menu Scale", MinValue = 1, MaxValue = 10, Mode = SettingMode.Simple, StepSize = 1)]
        public int ScaleMultiplier = 2;

        [ModSetting("Show Advanced Options", Mode = SettingMode.Both, ControlsChildVisibility = true)]
        public bool ShowSubMenu = false;

        [ModSetting("Sub Option Alpha", DependsOnId = "ShowSubMenu", Mode = SettingMode.Advanced)]
        public bool SubOptionA = true;

        [ModSetting("Sub Option Beta", DependsOnId = "ShowSubMenu", Mode = SettingMode.Advanced)]
        public bool SubOptionB = false;

        [ModSetting("Difficulty Preset", Mode = SettingMode.Simple)]
        public DifficultyLevel Difficulty = DifficultyLevel.Normal;

        [ModSetting("Requires Restart Demo", Mode = SettingMode.Advanced, RequiresRestart = true)]
        public bool ApplyOnRestart = false;

        [ModSetting("Validated Name", Mode = SettingMode.Advanced, ValidateMethod = "ValidateName", Tooltip = "Try entering an empty string or 'Admin'")]
        public string ValidatedString = "Player";

        [ModSetting("Dynamic Zone", Mode = SettingMode.Advanced, Type = SettingType.Choice, OptionsSource = "GetZones")]
        public string SelectedZone = "Shelter";

        public enum DifficultyLevel { Easy, Normal, Hard }

        // --- Advanced Hook Implementation ---
        public bool ValidateName(object newVal) {
            string s = newVal as string;
            if (string.IsNullOrEmpty(s)) return false;
            if (s.Equals("Admin", StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }

        public IEnumerable<string> GetZones() {
            return new List<string> { "Shelter", "Wasteland", "Town", "Radio Tower", "Hidden Bunker" };
        }

        [ModSetting("Manual Action Button", Mode = SettingMode.Advanced, Tooltip = "Triggers a custom method via reflection.")]
        public void DoManualAction() {
            MMLog.WriteInfo("[Showcase] Manual Action Button Triggered!");
        }
    }

    public class SpineHarmonyShowcase : IModPlugin, ISettingsProvider
    {
        // Singleton pattern for easy access within patches (SOLID: Logic access)
        public static ShowcaseSettings Settings { get; private set; }
        
        private IPluginContext _context;
        private List<SettingDefinition> _cachedDefinitions;

        public void Initialize(IPluginContext context)
        {
            _context = context;
            Settings = new ShowcaseSettings();
            
            // 2. Settings are auto-loaded by ModLoader before Start()

            // 3. Initialize Harmony Patches
            try
            {
                var harmony = new HarmonyLib.Harmony(context.Mod.Id);
                HarmonyUtil.PatchAll(harmony, typeof(SpineHarmonyShowcase).Assembly, context);
            }
            catch (Exception ex)
            {
                _context.Log.Error("Harmony initialization failed: " + ex.Message);
            }

            _context.Log.Info("Spine Harmony Showcase Plugin Initialized.");
        }

        public void Start(IPluginContext context) 
        {
            // Mod is ready
        }

        // ---------------------------------------------------------------------
        // 4. ISettingsProvider - Defining the UI structure
        // ---------------------------------------------------------------------
        // 4. ISettingsProvider Implementation - Minimal Boilerplate due to Auto-Persistence
        public IEnumerable<SettingDefinition> GetSettings()
        {
            if (_cachedDefinitions != null) return _cachedDefinitions;
            _cachedDefinitions = SpineSettingsHelper.Scan(Settings);
            return _cachedDefinitions;
        }

        public object GetSettingsObject() => Settings;

        public void OnSettingsLoaded()
        {
            // Optional: Called when the Loader has finished auto-loading properties from JSON.
            // You can perform data validation or dependent logic updates here.
            _context.Log.Info($"[Showcase] Settings loaded! Enable={Settings.GlobalEnable}, Theme={Settings.ThemeColor}");
        }

        public void ResetToDefaults()
        {
            // Just create a fresh object. The Loader handles auto-saving this new state.
            Settings = new ShowcaseSettings(); 
            _context.Log.Info("[Showcase] Reset to defaults.");
        }
    }

    // -------------------------------------------------------------------------
    // 5. Harmony Patches - Applying the settings to the game
    // -------------------------------------------------------------------------

    [HarmonyPatch(typeof(MainMenuPanel), "OnShow")]
    public static class MainMenu_Display_Patch
    {
        static void Postfix(MainMenuPanel __instance)
        {
            // DRY: Access through singleton
            var settings = SpineHarmonyShowcase.Settings;
            if (settings == null || !settings.GlobalEnable) return;

            MMLog.WriteInfo($"[Showcase] Patch active. Scaling: {settings.ScaleMultiplier}, Color: {settings.ThemeColor}");

            // Determine which label is visible based on GameMode
            UILabel targetLabel = __instance.scenarioNameLabel;
            bool isSurvival = false;

            try
            {
                if (GameModeManager.instance != null && GameModeManager.instance.currentGameMode == GameModeManager.GameMode.Survival)
                {
                    isSurvival = true;
                    targetLabel = __instance.difficultyText;
                }
            }
            catch { /* safety */ }

            if (targetLabel != null)
            {
                // In Survival, this replaces "Normal" / "Hardcore" etc.
                // In Scenario, this replaces "Surrounded" etc.
                targetLabel.text = settings.MenuLabel;
                targetLabel.color = settings.ThemeColor;
                
                // Adjust position based on settings
                var pos = targetLabel.transform.localPosition;
                // For difficulty text, default Y is different, so we might want relative or absolute offset. 
                // For this demo, we'll just overwrite Y to demonstrate control.
                pos.y = settings.VerticalOffset; 
                targetLabel.transform.localPosition = pos;

                // Adjust scale based on settings
                targetLabel.transform.localScale = new Vector3(settings.ScaleMultiplier, settings.ScaleMultiplier, 1f);
            }
        }
    }
}
