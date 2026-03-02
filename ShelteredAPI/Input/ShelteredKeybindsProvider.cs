using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ModAPI.Core;
using ModAPI.InputActions;
using ModAPI.Spine;
using UnityEngine;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Settings provider bridging Sheltered input actions into the shared Spine settings window.
    /// </summary>
    public sealed class ShelteredKeybindsProvider : ISettingsProvider2
    {
        private const string PrefKeyPrefix = "ShelteredAPI.Keybind.";

        private readonly object _sync = new object();
        private List<SettingDefinition> _definitions;
        private bool _loaded;
        private static readonly ShelteredKeybindsProvider _instance = new ShelteredKeybindsProvider();

        public static ShelteredKeybindsProvider Instance { get { return _instance; } }

        public bool IsReady { get; private set; }

        private ShelteredKeybindsProvider()
        {
            IsReady = true;
        }

        public IEnumerable<SettingDefinition> GetSettings()
        {
            EnsureLoaded();
            lock (_sync)
            {
                if (_definitions == null)
                    _definitions = BuildDefinitions();
                return new List<SettingDefinition>(_definitions);
            }
        }

        public object GetSettingsObject()
        {
            return this;
        }

        public void OnSettingsLoaded()
        {
            EnsureLoaded();
        }

        public void ResetToDefaults()
        {
            EnsureLoaded();
            var actions = GetShelteredActions();
            for (int i = 0; i < actions.Count; i++)
            {
                InputActionRegistry.SetBinding(actions[i].Id, actions[i].DefaultBinding);
            }
            Save();
        }

        public void Save()
        {
            EnsureLoaded();
            var actions = GetShelteredActions();
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var binding = InputActionRegistry.GetBinding(action.Id);

                ModPrefs.SetString(BuildPrefKey(action.Id, true), binding.Primary.ToString());
                ModPrefs.SetString(BuildPrefKey(action.Id, false), binding.Secondary.ToString());
            }
            ModPrefs.Save();
        }

        public string SerializeToJson()
        {
            EnsureLoaded();
            var actions = GetShelteredActions();
            var sb = new StringBuilder();
            sb.Append("{");

            bool first = true;
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var binding = InputActionRegistry.GetBinding(action.Id);

                if (!first) sb.Append(",");
                sb.Append("\"").Append(Escape(action.Id)).Append("\":{");
                sb.Append("\"primary\":\"").Append(Escape(binding.Primary.ToString())).Append("\",");
                sb.Append("\"secondary\":\"").Append(Escape(binding.Secondary.ToString())).Append("\"}");
                first = false;
            }

            sb.Append("}");
            return sb.ToString();
        }

        public void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_sync)
            {
                if (_loaded) return;

                ShelteredInputActions.EnsureRegistered();
                LoadFromPrefs();
                _definitions = BuildDefinitions();
                _loaded = true;
            }
        }

        private void LoadFromPrefs()
        {
            var actions = GetShelteredActions();
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var defaults = action.DefaultBinding;

                KeyCode primary = ParseKeyCode(ModPrefs.GetString(BuildPrefKey(action.Id, true), defaults.Primary.ToString()), defaults.Primary);
                KeyCode secondary = ParseKeyCode(ModPrefs.GetString(BuildPrefKey(action.Id, false), defaults.Secondary.ToString()), defaults.Secondary);
                InputActionRegistry.SetBinding(action.Id, new InputBinding(primary, secondary));
            }
        }

        private static string BuildPrefKey(string actionId, bool primary)
        {
            return PrefKeyPrefix + actionId + (primary ? ".Primary" : ".Secondary");
        }

        private static KeyCode ParseKeyCode(string raw, KeyCode fallback)
        {
            if (string.IsNullOrEmpty(raw)) return fallback;

            try
            {
                if (Enum.IsDefined(typeof(KeyCode), raw))
                    return (KeyCode)Enum.Parse(typeof(KeyCode), raw, true);
            }
            catch { }

            try
            {
                return (KeyCode)Enum.Parse(typeof(KeyCode), raw, true);
            }
            catch
            {
                return fallback;
            }
        }

        private static KeyCode CoerceToKeyCode(object value, KeyCode fallback)
        {
            if (value is KeyCode) return (KeyCode)value;
            if (value is string) return ParseKeyCode((string)value, fallback);
            return fallback;
        }

        private static List<ModInputAction> GetShelteredActions()
        {
            ShelteredInputActions.EnsureRegistered();
            return InputActionRegistry.GetAllActions()
                .Where(a => ShelteredInputActions.IsShelteredAction(a.Id))
                .OrderBy(a => a.Category)
                .ThenBy(a => a.Label)
                .ToList();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static List<SettingDefinition> BuildDefinitions()
        {
            var result = new List<SettingDefinition>();
            var actions = GetShelteredActions();
            int order = 0;

            for (int i = 0; i < actions.Count; i++)
            {
                ModInputAction action = actions[i];
                result.Add(CreateSlotDefinition(action, true, order++));
                result.Add(CreateSlotDefinition(action, false, order++));
            }

            return result;
        }

        private static SettingDefinition CreateSlotDefinition(ModInputAction action, bool primary, int sortOrder)
        {
            string slotLabel = primary ? action.Label : (action.Label + " (Alt)");

            return new SettingDefinition
            {
                Id = action.Id + (primary ? ".primary" : ".secondary"),
                FieldName = action.Id + (primary ? ".primary" : ".secondary"),
                Label = slotLabel,
                Tooltip = string.IsNullOrEmpty(action.Description)
                    ? (primary ? "Primary key binding." : "Secondary key binding.")
                    : (action.Description + (primary ? " Primary key." : " Secondary key.")),
                Type = SettingType.Keybind,
                Mode = SettingMode.Both,
                Category = action.Category,
                SortOrder = sortOrder,
                Scope = SettingsScope.Global,
                DefaultValue = primary ? action.DefaultBinding.Primary : action.DefaultBinding.Secondary,
                Getter = _ =>
                {
                    var binding = InputActionRegistry.GetBinding(action.Id);
                    return primary ? (object)binding.Primary : binding.Secondary;
                },
                Setter = (_, value) =>
                {
                    var binding = InputActionRegistry.GetBinding(action.Id);
                    if (primary) binding.Primary = CoerceToKeyCode(value, binding.Primary);
                    else binding.Secondary = CoerceToKeyCode(value, binding.Secondary);
                    InputActionRegistry.SetBinding(action.Id, binding);
                }
            };
        }
    }
}
