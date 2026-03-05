using System;
using System.Collections.Generic;
using ModAPI.InputActions;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Registers and resolves vanilla Sheltered PC input actions through ModAPI bindings.
    /// </summary>
    public static class ShelteredVanillaInputActions
    {
        private const string InputPrefix = "sheltered.vanilla.input.";
        private const string MenuPrefix = "sheltered.vanilla.menu.";

        private static readonly object Sync = new object();
        private static readonly object RuntimeSync = new object();
        private static readonly Dictionary<PlatformInput.InputButton, ActionDef> InputDefs =
            new Dictionary<PlatformInput.InputButton, ActionDef>();
        private static readonly Dictionary<PlatformInput.MenuInputButton, ActionDef> MenuDefs =
            new Dictionary<PlatformInput.MenuInputButton, ActionDef>();
        private static readonly Dictionary<string, InputContext> ActionContexts =
            new Dictionary<string, InputContext>(StringComparer.OrdinalIgnoreCase);

        private static bool _registered;
        private static bool _runtimeLoaded;

        static ShelteredVanillaInputActions()
        {
            BuildCatalog();
        }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            lock (Sync)
            {
                if (_registered) return;

                RegisterRange(InputDefs.Values);
                RegisterRange(MenuDefs.Values);
                _registered = true;
                MMLog.WriteInfo("[ShelteredVanillaInputActions] Registered "
                    + InputDefs.Count + " gameplay actions and " + MenuDefs.Count + " menu actions.");
            }
        }

        public static void EnsureRuntimeLoaded()
        {
            EnsureRegistered();
            if (_runtimeLoaded) return;

            lock (RuntimeSync)
            {
                if (_runtimeLoaded) return;
                ShelteredKeybindsProvider.Instance.EnsureLoaded();
                _runtimeLoaded = true;
                MMLog.WriteInfo("[ShelteredVanillaInputActions] Runtime keybinds loaded from provider.");
            }
        }

        public static bool TryGetBinding(PlatformInput.InputButton button, out InputBinding binding)
        {
            binding = new InputBinding(KeyCode.None, KeyCode.None);
            EnsureRuntimeLoaded();

            ActionDef def;
            if (!InputDefs.TryGetValue(button, out def)) return false;
            return InputActionRegistry.TryGetBinding(def.Id, out binding);
        }

        public static bool TryGetBinding(PlatformInput.MenuInputButton button, out InputBinding binding)
        {
            binding = new InputBinding(KeyCode.None, KeyCode.None);
            EnsureRuntimeLoaded();

            ActionDef def;
            if (!MenuDefs.TryGetValue(button, out def)) return false;
            return InputActionRegistry.TryGetBinding(def.Id, out binding);
        }

        public static bool IsAnyMappedKeyDown()
        {
            EnsureRuntimeLoaded();

            foreach (ActionDef def in InputDefs.Values)
            {
                InputBinding binding;
                if (InputActionRegistry.TryGetBinding(def.Id, out binding) && binding.IsDown())
                    return true;
            }

            foreach (ActionDef def in MenuDefs.Values)
            {
                InputBinding binding;
                if (InputActionRegistry.TryGetBinding(def.Id, out binding) && binding.IsDown())
                    return true;
            }

            return false;
        }

        public static InputContext GetContextForActionId(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return InputContext.Unknown;

            InputContext context;
            if (ActionContexts.TryGetValue(actionId, out context))
                return context;

            if (ShelteredInputActions.IsShelteredAction(actionId))
                return InputContext.Gameplay;

            return InputContext.Unknown;
        }

        private static void RegisterRange(IEnumerable<ActionDef> defs)
        {
            foreach (ActionDef def in defs)
            {
                InputActionRegistry.Register(new ModInputAction(
                    def.Id,
                    def.Label,
                    def.Category,
                    def.DefaultBinding,
                    def.Description));
            }
        }

        private static void BuildCatalog()
        {
            AddInput(PlatformInput.InputButton.Action, "action", "Primary Action", "Gameplay", KeyCode.Mouse0, KeyCode.None, "Primary in-world action.");
            AddInput(PlatformInput.InputButton.Interact, "interact", "Interact", "Gameplay", KeyCode.Mouse1, KeyCode.None, "Secondary interaction.");
            AddInput(PlatformInput.InputButton.CancelJob, "cancel_job", "Cancel Job", "Gameplay", KeyCode.C, KeyCode.None, "Cancel current character job.");
            AddInput(PlatformInput.InputButton.Context, "context", "Context Action", "Gameplay", KeyCode.Space, KeyCode.None, "Open contextual action.");
            AddInput(PlatformInput.InputButton.Clipboard, "clipboard", "Clipboard", "UI", KeyCode.G, KeyCode.None, "Open clipboard view.");
            AddInput(PlatformInput.InputButton.Cancel, "cancel", "Cancel / Back", "UI", KeyCode.Escape, KeyCode.None, "Back/cancel action.");
            AddInput(PlatformInput.InputButton.Pause, "pause", "Pause", "System", KeyCode.Escape, KeyCode.None, "Pause game.", InputContext.System);
            AddInput(PlatformInput.InputButton.Info, "info", "Info", "UI", KeyCode.I, KeyCode.None, "Open info panel.");
            AddInput(PlatformInput.InputButton.Focus, "focus", "Focus", "Gameplay", KeyCode.Space, KeyCode.None, "Focus camera/object.");
            AddInput(PlatformInput.InputButton.GoHere, "go_here", "Go Here", "Gameplay", KeyCode.Mouse0, KeyCode.None, "Move selected character.");
            AddInput(PlatformInput.InputButton.NextChar, "next_character", "Next Character", "Gameplay", KeyCode.E, KeyCode.None, "Cycle to next character.");
            AddInput(PlatformInput.InputButton.PrevChar, "previous_character", "Previous Character", "Gameplay", KeyCode.Q, KeyCode.None, "Cycle to previous character.");
            AddInput(PlatformInput.InputButton.Zoom, "zoom_modifier", "Zoom Modifier", "Camera", KeyCode.LeftControl, KeyCode.None, "Zoom modifier key.");
            AddInput(PlatformInput.InputButton.CameraSpeed, "camera_speed", "Camera Speed Modifier", "Camera", KeyCode.LeftShift, KeyCode.None, "Camera speed modifier.");
            AddInput(PlatformInput.InputButton.ToggleAutomation, "toggle_automation", "Toggle Automation", "Gameplay", KeyCode.H, KeyCode.Home, "Toggle selected character automation.");
            AddInput(PlatformInput.InputButton.AcceptTransmission, "accept_transmission", "Accept Transmission", "UI", KeyCode.R, KeyCode.None, "Accept incoming transmission.");
            AddInput(PlatformInput.InputButton.Dismiss, "dismiss", "Dismiss", "UI", KeyCode.Mouse0, KeyCode.None, "Dismiss current prompt.");
            AddInput(PlatformInput.InputButton.OpenMap, "open_map", "Open Map", "UI", KeyCode.M, KeyCode.None, "Open expedition map.");
            AddInput(PlatformInput.InputButton.NudgePlacementLeft, "nudge_left", "Nudge Placement Left", "Placement", KeyCode.Mouse0, KeyCode.None, "Nudge placement ghost left.");
            AddInput(PlatformInput.InputButton.NudgePlacementRight, "nudge_right", "Nudge Placement Right", "Placement", KeyCode.Mouse0, KeyCode.None, "Nudge placement ghost right.");
            AddInput(PlatformInput.InputButton.SlowDown, "slow_down", "Slow Down", "System", KeyCode.CapsLock, KeyCode.None, "Slow simulation speed.", InputContext.System);
            AddInput(PlatformInput.InputButton.SkipCutscene, "skip_cutscene", "Skip Cutscene", "Cinematics", KeyCode.Escape, KeyCode.None, "Skip active cutscene.", InputContext.System);
            AddInput(PlatformInput.InputButton.SkipSpeech, "skip_speech", "Skip Speech", "Cinematics", KeyCode.Space, KeyCode.None, "Skip current speech.");

            AddMenu(PlatformInput.MenuInputButton.UIselect, "select", "UI Select", "Menu", KeyCode.Mouse0, KeyCode.None, "Confirm/select in menu.");
            AddMenu(PlatformInput.MenuInputButton.UIcancel, "cancel", "UI Cancel", "Menu", KeyCode.Escape, KeyCode.None, "Cancel/back in menu.");
            AddMenu(PlatformInput.MenuInputButton.UIextra1, "extra_1", "UI Extra 1", "Menu", KeyCode.Mouse0, KeyCode.None, "Additional menu shortcut.");
            AddMenu(PlatformInput.MenuInputButton.UIextra2, "extra_2", "UI Extra 2", "Menu", KeyCode.Mouse0, KeyCode.None, "Additional menu shortcut.");
            AddMenu(PlatformInput.MenuInputButton.UIextra3, "extra_3", "UI Extra 3", "Menu", KeyCode.Mouse0, KeyCode.None, "Additional menu shortcut.");
            AddMenu(PlatformInput.MenuInputButton.UIextra4, "extra_4", "UI Extra 4", "Menu", KeyCode.Mouse0, KeyCode.None, "Additional menu shortcut.");
            AddMenu(PlatformInput.MenuInputButton.UITabRight, "tab_right", "Tab Right", "Menu", KeyCode.Mouse0, KeyCode.None, "Move to next menu tab.");
            AddMenu(PlatformInput.MenuInputButton.UITabLeft, "tab_left", "Tab Left", "Menu", KeyCode.Mouse0, KeyCode.None, "Move to previous menu tab.");
            AddMenu(PlatformInput.MenuInputButton.UIstart, "start", "UI Start", "Menu", KeyCode.Space, KeyCode.None, "Start/confirm from menu.");
            AddMenu(PlatformInput.MenuInputButton.UIdragMap, "drag_map", "Drag Map", "Menu", KeyCode.Mouse1, KeyCode.None, "Drag map view.");
            AddMenu(PlatformInput.MenuInputButton.UIdragWaypoint, "drag_waypoint", "Drag Waypoint", "Menu", KeyCode.Mouse0, KeyCode.None, "Drag waypoint marker.");
        }

        private static void AddInput(
            PlatformInput.InputButton button,
            string idSuffix,
            string label,
            string category,
            KeyCode primary,
            KeyCode secondary,
            string description,
            InputContext context = InputContext.Gameplay)
        {
            InputDefs[button] = new ActionDef(
                InputPrefix + idSuffix,
                label,
                category,
                new InputBinding(primary, secondary),
                description);
            ActionContexts[InputPrefix + idSuffix] = context;
        }

        private static void AddMenu(
            PlatformInput.MenuInputButton button,
            string idSuffix,
            string label,
            string category,
            KeyCode primary,
            KeyCode secondary,
            string description)
        {
            MenuDefs[button] = new ActionDef(
                MenuPrefix + idSuffix,
                label,
                category,
                new InputBinding(primary, secondary),
                description);
            ActionContexts[MenuPrefix + idSuffix] = InputContext.Menu;
        }

        private sealed class ActionDef
        {
            public readonly string Id;
            public readonly string Label;
            public readonly string Category;
            public readonly InputBinding DefaultBinding;
            public readonly string Description;

            public ActionDef(string id, string label, string category, InputBinding defaultBinding, string description)
            {
                Id = id;
                Label = label;
                Category = category;
                DefaultBinding = defaultBinding;
                Description = description ?? string.Empty;
            }
        }
    }
}
