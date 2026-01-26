using System;
using System.Collections.Generic;
using UnityEngine;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Spine;
using ModAPI.UI;
using System.Reflection;

namespace PluginHarmony
{
    public class HarmonyExamplePlugin : IModPlugin, ISettingsProvider
    {
        public static ExampleSettings Settings { get; private set; }
        private IPluginContext _context;
        private List<SettingDefinition> _definitions;

        public void Initialize(IPluginContext context)
        {
            _context = context;
            Settings = new ExampleSettings();
            
            // 2. Settings are auto-loaded by ModLoader before Start()

            try
            {
                var harmony = new Harmony(context.Mod.Id);
                harmony.PatchAll(Assembly.GetExecutingAssembly());
                _context.Log.Info("All Harmony patching examples registered.");
            }
            catch (Exception ex)
            {
                _context.Log.Error("Patching failed: " + ex.Message);
            }
        }

        public void Start(IPluginContext context) { }

        public IEnumerable<SettingDefinition> GetSettings()
        {
            if (_definitions != null) return _definitions;

            _definitions = new List<SettingDefinition>();
            var settingsType = typeof(ExampleSettings);
            var fields = settingsType.GetFields(BindingFlags.Public | BindingFlags.Instance);

            foreach (var field in fields)
            {
                var attr = (ModSettingAttribute)Attribute.GetCustomAttribute(field, typeof(ModSettingAttribute));
                if (attr == null) continue;

                var def = new SettingDefinition
                {
                    Id = field.Name,
                    FieldName = field.Name,
                    Label = attr.Label ?? field.Name,
                    Tooltip = attr.Tooltip,
                    Mode = attr.Mode,
                    MinValue = attr.MinValue,
                    MaxValue = attr.MaxValue
                };

                if (attr.Type != SettingType.Unknown)
                {
                    def.Type = attr.Type;
                    // For headers, we might need HeaderColor
                    if (def.Type == SettingType.Header && !string.IsNullOrEmpty(attr.HeaderColor))
                    {
                        if (ColorUtility.TryParseHtmlString("#" + attr.HeaderColor.TrimStart('#'), out Color c))
                            def.HeaderColor = c;
                    }
                }
                else
                {
                    // Auto-detect type if not specified
                    var fieldType = field.FieldType;
                    if (fieldType == typeof(bool)) def.Type = SettingType.Bool;
                    else if (fieldType == typeof(int)) def.Type = SettingType.NumericInt;
                    else if (fieldType == typeof(float)) def.Type = SettingType.Float;
                    else if (fieldType == typeof(string)) def.Type = SettingType.String;
                    else if (fieldType == typeof(Color)) def.Type = SettingType.Color;
                    else if (fieldType.IsEnum) {
                        def.Type = SettingType.Enum;
                        def.EnumType = fieldType; // CRITICAL: Store the Enum Type!
                    }
                    else 
                    {
                        _context?.Log.Info($"[DEBUG] Field {field.Name} has unsupported type {fieldType.Name}");
                        continue;
                    }
                }

                // --- PRESET CONFIGURATION (DEMO) ---
                // Manually assigning presets here since Attributes cannot hold Dictionaries.
                // In a real scenario, this could be a static lookup or a helper method.

                def.Presets = new Dictionary<string, object>();

                switch (field.Name)
                {
                    case nameof(ExampleSettings.EnableVisuals):
                        def.Presets.Add("Easy", true);
                        def.Presets.Add("Normal", true);
                        def.Presets.Add("Hard", false);
                        break;
                    case nameof(ExampleSettings.Difficulty):
                        def.Presets.Add("Easy", DifficultyLevel.Easy);
                        def.Presets.Add("Normal", DifficultyLevel.Normal);
                        def.Presets.Add("Hard", DifficultyLevel.Hard);
                        break;
                    case nameof(ExampleSettings.EnableGameplay):
                        def.Presets.Add("Easy", true);
                        def.Presets.Add("Normal", true);
                        def.Presets.Add("Hard", true);
                        break;
                    case nameof(ExampleSettings.GodMode):
                        def.Presets.Add("Easy", true);
                        def.Presets.Add("Normal", false);
                        def.Presets.Add("Hard", false);
                        break;
                    case nameof(ExampleSettings.LogInventory):
                        def.Presets.Add("Easy", true);
                        def.Presets.Add("Normal", false);
                        def.Presets.Add("Hard", false);
                        break;
                     case nameof(ExampleSettings.LabelYOffset):
                        def.Presets.Add("Easy", 0f);
                        def.Presets.Add("Normal", 50f);
                        def.Presets.Add("Hard", 100f);
                        break;
                }
                
                // If a setting has no presets (like color), it just stays as is in Custom mode.
                // Or we can add them to all if we want a "consistent" preset experience.
                
                _definitions.Add(def);
            }
            return _definitions;
        }

        public object GetSettingsObject() => Settings;
        
        public void OnSettingsLoaded()
        {
            _context.Log.Info("Harmony Example settings auto-loaded.");
        }

        public void ResetToDefaults()
        {
            Settings = new ExampleSettings();
            // No need to save manually; loader handles it after this call
        }
    }

    [HarmonyPatch(typeof(MainMenu), "OnShow")]
    public static class MenuLabelPatch
    {
        static void Postfix(MainMenu __instance)
        {
            var s = HarmonyExamplePlugin.Settings;
            if (!s.EnableVisuals) return;

            // Use CreateLabelQuick instead of CreateLabel with options
            var label = UIUtil.CreateLabelQuick(__instance.gameObject, s.MenuLabel, 18, new Vector3(-20, -20, 0));
            if (label != null)
            {
                label.color = s.LabelColor;
                label.alignment = NGUIText.Alignment.Left;
                label.pivot = UIWidget.Pivot.BottomLeft;
                MMLog.WriteInfo($"[Harmony Demo] Label created at position: {label.transform.localPosition}");
            }
        }
    }
}