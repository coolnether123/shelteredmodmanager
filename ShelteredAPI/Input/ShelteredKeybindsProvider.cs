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
    /// Applies a full validation/conflict/persist pipeline for every bind change.
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
                PersistActionBinding(actions[i].Id, actions[i].DefaultBinding);
            }
            ModPrefs.Save();
        }

        public void Save()
        {
            EnsureLoaded();
            var actions = GetShelteredActions();
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                PersistActionBinding(action.Id, InputActionRegistry.GetBinding(action.Id));
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

                EnsureActionsRegistered();
                LoadFromPrefs();
                _definitions = BuildDefinitions();
                _loaded = true;
            }
        }

        /// <summary>
        /// Pipeline:
        /// Input -> Validate -> Conflict Detection -> Conflict Prompt/Resolution -> Apply -> Persist.
        /// Returns true only when applied immediately in this call.
        /// </summary>
        public bool ApplyBindingWithConflictFlow(string actionId, bool primary, KeyCode keyCode, InputContext context)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(actionId)) return false;

            if (keyCode != KeyCode.None)
            {
                if (!KeyValidationPolicy.IsKeyBindable(keyCode))
                {
                    MMLog.WriteInfo("[Keybinds] Rejected unbindable key " + keyCode + " for " + actionId + ".");
                    return false;
                }

                if (!KeyValidationPolicy.IsValidForContext(keyCode, context))
                {
                    MMLog.WriteInfo("[Keybinds] Rejected key " + keyCode + " for context " + context + " on " + actionId + ".");
                    return false;
                }
            }

            InputBinding current = InputActionRegistry.GetBinding(actionId);
            InputBinding proposed = current;
            KeyCode replacedKey = primary ? current.Primary : current.Secondary;

            if (primary) proposed.Primary = keyCode;
            else proposed.Secondary = keyCode;

            NormalizeSelfOverlap(ref proposed, primary);

            if (keyCode != KeyCode.None)
            {
                KeyConflictDetection detected = KeyConflictResolver.DetectConflicts(keyCode, actionId, context);
                if (detected.Conflicted)
                {
                    if (TryPromptConflictAndApply(actionId, primary, replacedKey, keyCode, proposed, detected))
                    {
                        // Deferred apply via MessageBox callback.
                        return false;
                    }

                    // Fallback (no UI prompt available): apply recommended policy.
                    KeyConflictResolution fallbackResolution = KeyConflictResolver.ResolveConflict(
                        actionId,
                        primary ? KeyBindingSlot.Primary : KeyBindingSlot.Secondary,
                        replacedKey,
                        keyCode,
                        detected,
                        detected.RecommendedChoice);

                    if (!fallbackResolution.Applied)
                    {
                        MMLog.WriteInfo("[Keybinds] Binding cancelled for " + actionId + ". " + fallbackResolution.Message);
                        return false;
                    }

                    PersistAffectedActions(fallbackResolution.AffectedActionIds);
                }
            }

            ApplyAndPersist(actionId, proposed);
            return true;
        }

        private void LoadFromPrefs()
        {
            var actions = GetShelteredActions();
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                var defaults = action.DefaultBinding;
                InputContext context = ResolveContext(action.Id, action.Category);

                string primaryRaw = ModPrefs.GetString(BuildPrefKey(action.Id, true), defaults.Primary.ToString());
                string secondaryRaw = ModPrefs.GetString(BuildPrefKey(action.Id, false), defaults.Secondary.ToString());

                KeyCode primary = KeyValidationPolicy.ParseKeyCodeSafe(primaryRaw, defaults.Primary, action.Id + ".Primary", context);
                KeyCode secondary = KeyValidationPolicy.ParseKeyCodeSafe(secondaryRaw, defaults.Secondary, action.Id + ".Secondary", context);

                var loaded = new InputBinding(primary, secondary);
                if (!KeyValidationPolicy.IsValidForContext(loaded.Primary, context))
                    loaded.Primary = defaults.Primary;
                if (!KeyValidationPolicy.IsValidForContext(loaded.Secondary, context))
                    loaded.Secondary = defaults.Secondary;

                if (loaded.Primary != KeyCode.None && loaded.Primary == loaded.Secondary)
                    loaded.Secondary = KeyCode.None;

                InputActionRegistry.SetBinding(action.Id, loaded);
            }
        }

        private List<SettingDefinition> BuildDefinitions()
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

        private SettingDefinition CreateSlotDefinition(ModInputAction action, bool primary, int sortOrder)
        {
            string slotLabel = primary ? action.Label : (action.Label + " (Alt)");
            InputContext context = ResolveContext(action.Id, action.Category);

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
                Validate = (_, value) =>
                {
                    var binding = InputActionRegistry.GetBinding(action.Id);
                    KeyCode fallback = primary ? binding.Primary : binding.Secondary;
                    KeyCode requested = CoerceToKeyCode(value, fallback, action.Id, context);
                    return ApplyBindingWithConflictFlow(action.Id, primary, requested, context);
                },
                // Setter intentionally becomes a no-op for keybinds:
                // validate phase executes the full apply pipeline.
                Setter = (_, __) => { }
            };
        }

        private bool TryPromptConflictAndApply(
            string actionId,
            bool primary,
            KeyCode replacedKey,
            KeyCode proposedKey,
            InputBinding proposedBinding,
            KeyConflictDetection detected)
        {
            if (UIPanelManager.instance == null) return false;

            try
            {
                string message = BuildConflictPrompt(actionId, proposedKey, detected);
                MessageBox.Show(
                    MessageBoxButtons.YesNo_Buttons,
                    message,
                    delegate(int response)
                    {
                        KeyConflictUserChoice choice = response == 1
                            ? KeyConflictUserChoice.Override
                            : KeyConflictUserChoice.Cancel;

                        KeyConflictResolution resolution = KeyConflictResolver.ResolveConflict(
                            actionId,
                            primary ? KeyBindingSlot.Primary : KeyBindingSlot.Secondary,
                            replacedKey,
                            proposedKey,
                            detected,
                            choice);

                        if (!resolution.Applied)
                        {
                            MMLog.WriteInfo("[Keybinds] Conflict prompt cancelled for " + actionId + ".");
                            return;
                        }

                        PersistAffectedActions(resolution.AffectedActionIds);
                        ApplyAndPersist(actionId, proposedBinding);
                    },
                    null,
                    null,
                    false);

                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[Keybinds] Conflict prompt failed: " + ex.Message);
                return false;
            }
        }

        private static void ApplyAndPersist(string actionId, InputBinding binding)
        {
            InputActionRegistry.SetBinding(actionId, binding);
            PersistActionBinding(actionId, binding);
            ModPrefs.Save();
        }

        private static void PersistAffectedActions(List<string> affectedActionIds)
        {
            if (affectedActionIds == null) return;

            for (int i = 0; i < affectedActionIds.Count; i++)
            {
                string id = affectedActionIds[i];
                if (string.IsNullOrEmpty(id)) continue;
                PersistActionBinding(id, InputActionRegistry.GetBinding(id));
            }

            ModPrefs.Save();
        }

        private static void PersistActionBinding(string actionId, InputBinding binding)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            ModPrefs.SetString(BuildPrefKey(actionId, true), binding.Primary.ToString());
            ModPrefs.SetString(BuildPrefKey(actionId, false), binding.Secondary.ToString());
        }

        private static void NormalizeSelfOverlap(ref InputBinding binding, bool changedPrimary)
        {
            if (binding.Primary == KeyCode.None || binding.Secondary == KeyCode.None) return;
            if (binding.Primary != binding.Secondary) return;

            // Keep the newly changed slot and clear the other one.
            if (changedPrimary) binding.Secondary = KeyCode.None;
            else binding.Primary = KeyCode.None;
        }

        private static string BuildConflictPrompt(string actionId, KeyCode key, KeyConflictDetection detected)
        {
            var sb = new StringBuilder();
            sb.Append("Key ").Append(key).Append(" conflicts with ");
            sb.Append(detected.ActionList.Count).Append(" action(s):\n");

            int maxLines = Mathf.Min(4, detected.ActionList.Count);
            for (int i = 0; i < maxLines; i++)
            {
                KeyConflictEntry c = detected.ActionList[i];
                sb.Append("- ").Append(c.ActionLabel).Append(" [").Append(c.Context).Append("]\n");
            }

            if (detected.ActionList.Count > maxLines)
                sb.Append("- ...\n");

            sb.Append("\n").Append(detected.Recommendation).Append("\n");
            sb.Append("Yes = Override conflicts\nNo = Cancel");
            return sb.ToString();
        }

        private static string BuildPrefKey(string actionId, bool primary)
        {
            return PrefKeyPrefix + actionId + (primary ? ".Primary" : ".Secondary");
        }

        private static KeyCode CoerceToKeyCode(object value, KeyCode fallback, string actionId, InputContext context)
        {
            return KeyValidationPolicy.ParseKeyCodeSafe(value, fallback, actionId, context);
        }

        private static InputContext ResolveContext(string actionId, string category)
        {
            InputContext fromCatalog = ShelteredVanillaInputActions.GetContextForActionId(actionId);
            if (fromCatalog != InputContext.Unknown) return fromCatalog;

            if (!string.IsNullOrEmpty(category))
            {
                if (string.Equals(category, "Menu", StringComparison.OrdinalIgnoreCase))
                    return InputContext.Menu;
                if (string.Equals(category, "System", StringComparison.OrdinalIgnoreCase))
                    return InputContext.System;
            }

            if (ShelteredInputActions.IsShelteredAction(actionId))
                return InputContext.Gameplay;

            return InputContext.Unknown;
        }

        private static List<ModInputAction> GetShelteredActions()
        {
            EnsureActionsRegistered();
            return InputActionRegistry.GetAllActions()
                .Where(a => ShelteredInputActions.IsShelteredAction(a.Id))
                .OrderBy(a => a.Category)
                .ThenBy(a => a.Label)
                .ToList();
        }

        private static void EnsureActionsRegistered()
        {
            ShelteredInputActions.EnsureRegistered();
            ShelteredVanillaInputActions.EnsureRegistered();
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
