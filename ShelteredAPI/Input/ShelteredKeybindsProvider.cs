using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using ModAPI.Core;
using ModAPI.InputActions;
using ModAPI.Spine;
using ModAPI.UI;
using ShelteredAPI.UI;
using UnityEngine;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Settings provider bridging Sheltered and mod-defined input actions into the shared Spine settings window.
    /// Applies a full validation/conflict/persist pipeline for every bind change.
    /// </summary>
    public sealed class ShelteredKeybindsProvider : ISettingsProvider2
    {
        private const string PrefKeyPrefix = "ShelteredAPI.Keybind.";
        private const string ZoomSpeedPrefKey = PrefKeyPrefix + "ZoomSpeed";
        private const string TouchpadMovementSpeedPrefKey = PrefKeyPrefix + "TouchpadMovementSpeed";
        private const string MouseScrollSpeedPrefKey = PrefKeyPrefix + "MouseScrollSpeed";
        private const string TuningCategory = "Input";
        private const string ModActionsHeaderLabel = "Mods Keybindings";
        private const int TuningSortBase = 10000;

        private readonly object _sync = new object();
        private List<SettingDefinition> _definitions;
        private bool _loaded;
        private static readonly ShelteredKeybindsProvider _instance = new ShelteredKeybindsProvider();

        /// <summary>
        /// Gets the singleton controls provider used by the Sheltered controls screen.
        /// </summary>
        public static ShelteredKeybindsProvider Instance { get { return _instance; } }

        /// <summary>
        /// Gets a value indicating whether the provider can serve settings to the UI.
        /// </summary>
        public bool IsReady { get; private set; }

        /// <summary>
        /// Gets or sets the runtime zoom-speed scale used by Sheltered input routing.
        /// </summary>
        public float ZoomSpeed { get { return ShelteredInputTuning.ZoomSpeed; } set { ShelteredInputTuning.ZoomSpeed = value; } }

        /// <summary>
        /// Gets or sets the runtime movement-speed scale used for indirect touchpad panning.
        /// </summary>
        public float TouchpadMovementSpeed { get { return ShelteredInputTuning.TouchpadMovementSpeed; } set { ShelteredInputTuning.TouchpadMovementSpeed = value; } }

        /// <summary>
        /// Gets or sets the runtime scale applied to mouse-wheel driven list and zoom input.
        /// </summary>
        public float MouseScrollSpeed { get { return ShelteredInputTuning.MouseScrollSpeed; } set { ShelteredInputTuning.MouseScrollSpeed = value; } }

        private ShelteredKeybindsProvider()
        {
            IsReady = true;
        }

        /// <summary>
        /// Returns the current Sheltered controls definitions, including built-in, mod-defined, and tuning entries.
        /// </summary>
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

        /// <summary>
        /// Returns the provider instance used as the settings data object for the shared settings UI.
        /// </summary>
        public object GetSettingsObject()
        {
            return this;
        }

        /// <summary>
        /// Applies persisted runtime tuning after the shared settings UI has finished loading definitions.
        /// </summary>
        public void OnSettingsLoaded()
        {
            EnsureLoaded();
            ApplyRuntimeTuning();
        }

        /// <summary>
        /// Restores all displayed bindings and Sheltered input tuning values to their shipped defaults.
        /// </summary>
        public void ResetToDefaults()
        {
            EnsureLoaded();
            var actions = GetDisplayedActions();
            for (int i = 0; i < actions.Count; i++)
            {
                InputActionRegistry.SetBinding(actions[i].Id, actions[i].DefaultBinding);
                PersistActionBinding(actions[i].Id, actions[i].DefaultBinding);
            }
            ZoomSpeed = ShelteredInputTuning.DefaultZoomSpeed;
            TouchpadMovementSpeed = ShelteredInputTuning.DefaultTouchpadMovementSpeed;
            MouseScrollSpeed = ShelteredInputTuning.DefaultMouseScrollSpeed;
            PersistRuntimeTuning();
            ModPrefs.Save();
        }

        /// <summary>
        /// Persists all displayed bindings and Sheltered input tuning values to ModPrefs.
        /// </summary>
        public void Save()
        {
            EnsureLoaded();
            var actions = GetDisplayedActions();
            for (int i = 0; i < actions.Count; i++)
            {
                var action = actions[i];
                PersistActionBinding(action.Id, InputActionRegistry.GetBinding(action.Id));
            }
            PersistRuntimeTuning();
            ModPrefs.Save();
            MMLog.WriteInfo("[ShelteredKeybindsProvider] Save persisted " + actions.Count + " displayed keybind actions and runtime input tuning.");
        }

        /// <summary>
        /// Serializes the current binding and tuning state into a compact JSON object for export/debug workflows.
        /// </summary>
        public string SerializeToJson()
        {
            EnsureLoaded();
            var actions = GetDisplayedActions();
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

            AppendFloatJsonProperty(sb, ref first, "zoomSpeed", ZoomSpeed);
            AppendFloatJsonProperty(sb, ref first, "touchpadMovementSpeed", TouchpadMovementSpeed);
            AppendFloatJsonProperty(sb, ref first, "mouseScrollSpeed", MouseScrollSpeed);

            sb.Append("}");
            return sb.ToString();
        }

        /// <summary>
        /// Registers Sheltered input actions, loads persisted values, and builds UI definitions on first use.
        /// </summary>
        public void EnsureLoaded()
        {
            if (_loaded) return;
            lock (_sync)
            {
                if (_loaded) return;

                MMLog.WriteDebug("[ShelteredKeybindsProvider] EnsureLoaded starting.");
                EnsureActionsRegistered();
                LoadFromPrefs();
                _definitions = BuildDefinitions();
                _loaded = true;
                MMLog.WriteDebug("[ShelteredKeybindsProvider] EnsureLoaded complete. Definitions=" + (_definitions != null ? _definitions.Count : 0));
            }
        }

        /// <summary>
        /// Pipeline:
        /// Input -> Validate -> Conflict Detection -> Conflict Prompt/Resolution -> Apply -> Persist.
        /// Returns true only when applied immediately in this call.
        /// </summary>
        /// <param name="actionId">The registered input action identifier to update.</param>
        /// <param name="primary"><see langword="true"/> to change the primary slot; otherwise the alternate slot.</param>
        /// <param name="keyCode">The requested key to bind, or <see cref="KeyCode.None"/> to clear the slot.</param>
        /// <param name="context">The logical context used for validation and conflict policy decisions.</param>
        /// <returns>
        /// <see langword="true"/> when the binding was applied immediately; otherwise <see langword="false"/>
        /// when the request was rejected or deferred to a conflict prompt callback.
        /// </returns>
        public bool ApplyBindingWithConflictFlow(string actionId, bool primary, KeyCode keyCode, InputContext context)
        {
            EnsureLoaded();
            if (string.IsNullOrEmpty(actionId)) return false;
            MMLog.WriteInfo("[Keybinds] Apply request action=" + actionId + ", slot=" + (primary ? "Primary" : "Secondary")
                + ", key=" + keyCode + ", context=" + context + ".");

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
                        MMLog.WriteInfo("[Keybinds] Conflict prompt shown for action=" + actionId + " key=" + keyCode + ".");
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
                    MMLog.WriteInfo("[Keybinds] Fallback conflict resolution applied for action=" + actionId + ". Affected="
                        + fallbackResolution.AffectedActionIds.Count + ".");
                }
            }

            ApplyAndPersist(actionId, proposed);
            MMLog.WriteInfo("[Keybinds] Apply complete action=" + actionId + " => primary=" + proposed.Primary + ", secondary=" + proposed.Secondary + ".");
            return true;
        }

        private void LoadFromPrefs()
        {
            var actions = GetDisplayedActions();
            int normalizedCount = 0;
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
                {
                    loaded.Secondary = KeyCode.None;
                    normalizedCount++;
                }

                InputActionRegistry.SetBinding(action.Id, loaded);
            }
            ZoomSpeed = LoadFloatPref(ZoomSpeedPrefKey, ShelteredInputTuning.DefaultZoomSpeed);
            TouchpadMovementSpeed = LoadFloatPref(TouchpadMovementSpeedPrefKey, ShelteredInputTuning.DefaultTouchpadMovementSpeed);
            MouseScrollSpeed = LoadFloatPref(MouseScrollSpeedPrefKey, ShelteredInputTuning.DefaultMouseScrollSpeed);
            ApplyRuntimeTuning();
            MMLog.WriteDebug("[ShelteredKeybindsProvider] Loaded " + actions.Count + " actions from ModPrefs. NormalizedDuplicates=" + normalizedCount + ".");
        }

        private List<SettingDefinition> BuildDefinitions()
        {
            var result = new List<SettingDefinition>();
            var actions = GetDisplayedActions();
            var managedActions = actions.Where(a => IsManagedActionId(a.Id)).ToList();
            var modActions = actions.Where(a => !IsManagedActionId(a.Id)).ToList();
            int order = 0;

            for (int i = 0; i < managedActions.Count; i++)
            {
                ModInputAction action = managedActions[i];
                result.Add(CreateSlotDefinition(action, true, order++));
                result.Add(CreateSlotDefinition(action, false, order++));
            }

            if (modActions.Count > 0)
            {
                result.Add(CreateHeaderDefinition("mods_keybindings", ModActionsHeaderLabel, order++));

                for (int i = 0; i < modActions.Count; i++)
                {
                    ModInputAction action = modActions[i];
                    result.Add(CreateSlotDefinition(action, true, order++));
                    result.Add(CreateSlotDefinition(action, false, order++));
                }
            }

            result.Add(CreateSpeedSettingDefinition(
                "sheltered.input.zoom_speed",
                "Zoom Speed",
                "ZoomSpeed",
                "Changes how fast zoom input affects the map. 1.00 matches the current game default.",
                ShelteredInputTuning.DefaultZoomSpeed,
                () => ZoomSpeed,
                value => ZoomSpeed = value,
                TuningSortBase));
            result.Add(CreateSpeedSettingDefinition(
                "sheltered.input.touchpad_movement_speed",
                "Touchpad Movement Speed",
                "TouchpadMovementSpeed",
                "Changes how fast indirect touchpad movement pans the camera and menus. 2.00 matches the current game default.",
                ShelteredInputTuning.DefaultTouchpadMovementSpeed,
                () => TouchpadMovementSpeed,
                value => TouchpadMovementSpeed = value,
                TuningSortBase + 1));
            result.Add(CreateSpeedSettingDefinition(
                "sheltered.input.mouse_scroll_speed",
                "Mouse Scroll Speed",
                "MouseScrollSpeed",
                "Changes how fast mouse wheel scrolling moves lists and zooms where supported. 1.00 matches the current game default.",
                ShelteredInputTuning.DefaultMouseScrollSpeed,
                () => MouseScrollSpeed,
                value => MouseScrollSpeed = value,
                TuningSortBase + 2));

            return result;
        }

        private SettingDefinition CreateSlotDefinition(ModInputAction action, bool primary, int sortOrder)
        {
            string slotLabel = primary ? action.Label : (action.Label + " (Alternate)");
            InputContext context = ResolveContext(action.Id, action.Category);

            return new SettingDefinition
            {
                Id = action.Id + (primary ? ".primary" : ".secondary"),
                FieldName = action.Id + (primary ? ".primary" : ".secondary"),
                Label = slotLabel,
                Tooltip = string.IsNullOrEmpty(action.Description)
                    ? "Key binding for this action."
                    : action.Description,
                Type = SettingType.Keybind,
                Mode = SettingMode.Both,
                Category = GetDisplayCategory(action),
                SortOrder = sortOrder,
                Scope = SettingsScope.Global,
                DefaultValue = primary ? action.DefaultBinding.Primary : action.DefaultBinding.Secondary,
                Getter = _ =>
                {
                    var binding = InputActionRegistry.GetBinding(action.Id);
                    return primary ? (object)binding.Primary : binding.Secondary;
                },
                Validate = (value, _) =>
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

        private static SettingDefinition CreateHeaderDefinition(string idSuffix, string label, int sortOrder)
        {
            return new SettingDefinition
            {
                Id = "header." + idSuffix,
                FieldName = "header." + idSuffix,
                Label = label,
                Tooltip = string.Empty,
                Type = SettingType.Header,
                Mode = SettingMode.Both,
                Category = null,
                SortOrder = sortOrder,
                Scope = SettingsScope.Global,
                HeaderColor = new Color(0.7f, 0.9f, 1f)
            };
        }

        private SettingDefinition CreateSpeedSettingDefinition(
            string id,
            string label,
            string fieldName,
            string tooltip,
            float defaultValue,
            Func<float> getter,
            Action<float> setter,
            int sortOrder)
        {
            return new SettingDefinition
            {
                Id = id,
                FieldName = fieldName,
                Label = label,
                Tooltip = tooltip,
                Type = SettingType.Float,
                Mode = SettingMode.Both,
                Category = TuningCategory,
                SortOrder = sortOrder,
                Scope = SettingsScope.Global,
                DefaultValue = defaultValue,
                MinValue = ShelteredInputTuning.MinSpeedScale,
                MaxValue = ShelteredInputTuning.MaxSpeedScale,
                StepSize = ShelteredInputTuning.SpeedStep,
                Getter = _ => getter(),
                Setter = (_, value) =>
                {
                    setter(CoerceSpeedValue(value, defaultValue));
                    ApplyRuntimeTuning();
                }
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
                ModSettingsPanel.PushExternalInputLock();
                ShelteredKeybindConflictDialog.Show(
                    "KEY CONFLICT",
                    message,
                    "OVERRIDE",
                    "CANCEL",
                    delegate
                    {
                        try
                        {
                            MMLog.WriteInfo("[Keybinds] Conflict prompt response=override for " + actionId + ".");
                            KeyConflictResolution resolution = KeyConflictResolver.ResolveConflict(
                                actionId,
                                primary ? KeyBindingSlot.Primary : KeyBindingSlot.Secondary,
                                replacedKey,
                                proposedKey,
                                detected,
                                KeyConflictUserChoice.Override);

                            if (!resolution.Applied)
                            {
                                MMLog.WriteInfo("[Keybinds] Conflict prompt cancelled for " + actionId + ".");
                                return;
                            }

                            PersistAffectedActions(resolution.AffectedActionIds);
                            ApplyAndPersist(actionId, proposedBinding);
                        }
                        finally
                        {
                            ModSettingsPanel.PopExternalInputLock();
                        }
                    },
                    delegate
                    {
                        try
                        {
                            MMLog.WriteInfo("[Keybinds] Conflict prompt response=cancel for " + actionId + ".");
                            KeyConflictResolution resolution = KeyConflictResolver.ResolveConflict(
                                actionId,
                                primary ? KeyBindingSlot.Primary : KeyBindingSlot.Secondary,
                                replacedKey,
                                proposedKey,
                                detected,
                                KeyConflictUserChoice.Cancel);

                            if (!resolution.Applied)
                                MMLog.WriteInfo("[Keybinds] Conflict prompt cancelled for " + actionId + ".");
                        }
                        finally
                        {
                            ModSettingsPanel.PopExternalInputLock();
                        }
                    });

                return true;
            }
            catch (Exception ex)
            {
                ModSettingsPanel.PopExternalInputLock();
                MMLog.WriteWarning("[Keybinds] Conflict prompt failed: " + ex.Message);
                return false;
            }
        }

        private static void ApplyAndPersist(string actionId, InputBinding binding)
        {
            InputActionRegistry.SetBinding(actionId, binding);
            PersistActionBinding(actionId, binding);
            ModPrefs.Save();
            MMLog.WriteInfo("[ShelteredKeybindsProvider] Persisted action=" + actionId + " primary=" + binding.Primary + " secondary=" + binding.Secondary + ".");
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
            MMLog.WriteInfo("[ShelteredKeybindsProvider] Persisted " + affectedActionIds.Count + " affected conflict action(s).");
        }

        private static void PersistActionBinding(string actionId, InputBinding binding)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            ModPrefs.SetString(BuildPrefKey(actionId, true), binding.Primary.ToString());
            ModPrefs.SetString(BuildPrefKey(actionId, false), binding.Secondary.ToString());
        }

        private void PersistRuntimeTuning()
        {
            PersistFloat(ZoomSpeedPrefKey, ZoomSpeed);
            PersistFloat(TouchpadMovementSpeedPrefKey, TouchpadMovementSpeed);
            PersistFloat(MouseScrollSpeedPrefKey, MouseScrollSpeed);
        }

        private static void NormalizeSelfOverlap(ref InputBinding binding, bool changedPrimary)
        {
            if (binding.Primary == KeyCode.None || binding.Secondary == KeyCode.None) return;
            if (binding.Primary != binding.Secondary) return;

            // Keep the newly changed slot and clear the other one.
            if (changedPrimary) binding.Secondary = KeyCode.None;
            else binding.Primary = KeyCode.None;
            MMLog.WriteInfo("[ShelteredKeybindsProvider] Normalized self-overlap by clearing "
                + (changedPrimary ? "secondary" : "primary") + " slot.");
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
            sb.Append("Override = clear conflicting bindings\nCancel = keep current binding");
            return sb.ToString();
        }

        private static string BuildPrefKey(string actionId, bool primary)
        {
            return PrefKeyPrefix + actionId + (primary ? ".Primary" : ".Secondary");
        }

        private void ApplyRuntimeTuning()
        {
            ShelteredInputTuning.ZoomSpeed = ZoomSpeed;
            ShelteredInputTuning.TouchpadMovementSpeed = TouchpadMovementSpeed;
            ShelteredInputTuning.MouseScrollSpeed = MouseScrollSpeed;
        }

        private static float LoadFloatPref(string key, float defaultValue)
        {
            string raw = ModPrefs.GetString(key, defaultValue.ToString("0.###", CultureInfo.InvariantCulture));
            float parsed;
            if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed))
                parsed = defaultValue;

            return ShelteredInputTuning.NormalizeSpeedScale(parsed, defaultValue);
        }

        private static void PersistFloat(string key, float value)
        {
            ModPrefs.SetString(key, value.ToString("0.###", CultureInfo.InvariantCulture));
        }

        private static KeyCode CoerceToKeyCode(object value, KeyCode fallback, string actionId, InputContext context)
        {
            return KeyValidationPolicy.ParseKeyCodeSafe(value, fallback, actionId, context);
        }

        private static float CoerceSpeedValue(object value, float fallback)
        {
            if (value == null)
                return fallback;

            try
            {
                return ShelteredInputTuning.NormalizeSpeedScale(
                    Convert.ToSingle(value, CultureInfo.InvariantCulture),
                    fallback);
            }
            catch
            {
                return fallback;
            }
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

        private static List<ModInputAction> GetDisplayedActions()
        {
            EnsureActionsRegistered();
            return InputActionRegistry.GetAllActions()
                .OrderBy(a => GetActionSortWeight(a))
                .ThenBy(a => GetCategorySortWeight(a))
                .ThenBy(a => GetDisplayCategory(a))
                .ThenBy(a => a.Label)
                .ToList();
        }

        private static void EnsureActionsRegistered()
        {
            ShelteredInputActions.EnsureRegistered();
            ShelteredVanillaInputActions.EnsureRegistered();
        }

        private static bool IsManagedActionId(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return false;
            if (ShelteredInputActions.IsShelteredAction(actionId)) return true;
            return ShelteredVanillaInputActions.GetContextForActionId(actionId) != InputContext.Unknown;
        }

        private static string GetDisplayCategory(ModInputAction action)
        {
            if (action == null) return string.Empty;
            return string.IsNullOrEmpty(action.Category) ? "General" : action.Category;
        }

        private static int GetCategorySortWeight(ModInputAction action)
        {
            if (action == null) return 99;
            if (!IsManagedActionId(action.Id)) return 0;

            string category = GetDisplayCategory(action);
            if (string.Equals(category, "Gameplay", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(category, "Camera", StringComparison.OrdinalIgnoreCase)) return 1;
            if (string.Equals(category, "Cinematics", StringComparison.OrdinalIgnoreCase)) return 2;
            if (string.Equals(category, "Menu", StringComparison.OrdinalIgnoreCase)) return 3;
            if (string.Equals(category, "System", StringComparison.OrdinalIgnoreCase)) return 4;
            if (string.Equals(category, TuningCategory, StringComparison.OrdinalIgnoreCase)) return 5;
            return 10;
        }

        private static int GetActionSortWeight(ModInputAction action)
        {
            if (action == null || string.IsNullOrEmpty(action.Id)) return 99;
            if (action.Id.StartsWith("sheltered.vanilla.", StringComparison.OrdinalIgnoreCase)) return 0;
            if (ShelteredInputActions.IsShelteredAction(action.Id)) return 1;
            return 2;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void AppendFloatJsonProperty(StringBuilder sb, ref bool first, string key, float value)
        {
            if (!first) sb.Append(",");
            sb.Append("\"").Append(Escape(key)).Append("\":");
            sb.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            first = false;
        }
    }
}
