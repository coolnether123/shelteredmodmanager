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
    internal static class ShelteredVanillaInputActions
    {
        private const string InputPrefix = "sheltered.vanilla.input.";
        private const string MenuPrefix = "sheltered.vanilla.menu.";

        private static readonly object Sync = new object();
        private static readonly object RuntimeSync = new object();
        private static readonly Dictionary<PlatformInput.InputButton, ActionDef> InputDefs =
            new Dictionary<PlatformInput.InputButton, ActionDef>();
        private static readonly Dictionary<PlatformInput.MenuInputButton, ActionDef> MenuDefs =
            new Dictionary<PlatformInput.MenuInputButton, ActionDef>();
        private static readonly Dictionary<PlatformInput.MenuInputButton, string> MenuAliasActionIds =
            new Dictionary<PlatformInput.MenuInputButton, string>();
        private static readonly Dictionary<string, InputContext> ActionContexts =
            new Dictionary<string, InputContext>(StringComparer.OrdinalIgnoreCase);

        private static bool _registered;
        private static bool _runtimeLoaded;

        static ShelteredVanillaInputActions()
        {
            BuildCatalog();
        }

        /// <summary>
        /// Registers the vanilla Sheltered action catalog with <see cref="InputActionRegistry"/> exactly once.
        /// </summary>
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

        /// <summary>
        /// Ensures the action catalog is registered and the persisted Sheltered keybind state has been loaded.
        /// </summary>
        public static void EnsureRuntimeLoaded()
        {
            EnsureRegistered();
            if (_runtimeLoaded) return;

            lock (RuntimeSync)
            {
                if (_runtimeLoaded) return;
                ShelteredKeybindsProvider.Instance.EnsureLoaded();
                _runtimeLoaded = true;
                MMLog.WriteDebug("[ShelteredVanillaInputActions] Runtime keybinds loaded from provider.");
            }
        }

        /// <summary>
        /// Tries to resolve the active binding for a vanilla gameplay input button.
        /// </summary>
        /// <param name="button">The vanilla gameplay button identifier.</param>
        /// <param name="binding">Receives the current active binding when the lookup succeeds.</param>
        /// <returns><see langword="true"/> when the button is tracked by the catalog; otherwise <see langword="false"/>.</returns>
        public static bool TryGetBinding(PlatformInput.InputButton button, out InputBinding binding)
        {
            binding = new InputBinding(KeyCode.None, KeyCode.None);
            EnsureRuntimeLoaded();

            ActionDef def;
            if (!InputDefs.TryGetValue(button, out def)) return false;
            return InputActionRegistry.TryGetBinding(def.Id, out binding);
        }

        /// <summary>
        /// Tries to resolve the active binding for a vanilla menu input button or alias.
        /// </summary>
        /// <param name="button">The vanilla menu button identifier.</param>
        /// <param name="binding">Receives the current active binding when the lookup succeeds.</param>
        /// <returns><see langword="true"/> when the button or alias is tracked by the catalog; otherwise <see langword="false"/>.</returns>
        public static bool TryGetBinding(PlatformInput.MenuInputButton button, out InputBinding binding)
        {
            binding = new InputBinding(KeyCode.None, KeyCode.None);
            EnsureRuntimeLoaded();

            ActionDef def;
            if (MenuDefs.TryGetValue(button, out def))
                return InputActionRegistry.TryGetBinding(def.Id, out binding);

            string aliasActionId;
            if (MenuAliasActionIds.TryGetValue(button, out aliasActionId))
                return InputActionRegistry.TryGetBinding(aliasActionId, out binding);

            return false;
        }

        /// <summary>
        /// Returns a value indicating whether any registered vanilla Sheltered binding was pressed this frame.
        /// </summary>
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

        /// <summary>
        /// Resolves the validation/conflict context for a registered Sheltered action identifier.
        /// </summary>
        /// <param name="actionId">The action identifier produced by the Sheltered input catalogs.</param>
        /// <returns>The resolved input context, or <see cref="InputContext.Unknown"/> when the action is not known.</returns>
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
            AddInput(PlatformInput.InputButton.Action, "action", "World Select / Place", "Gameplay", KeyCode.Mouse0, KeyCode.None, "Selects objects and survivors in the shelter. During placement, this confirms the item placement.");
            AddInput(PlatformInput.InputButton.Interact, "interact", "Open Interaction Menu", "Gameplay", KeyCode.Mouse1, KeyCode.None, "Opens the interaction menu for the object under the cursor.");
            AddInput(PlatformInput.InputButton.CancelJob, "cancel_job", "Cancel Survivor Task", "Gameplay", KeyCode.C, KeyCode.None, "Cancels the selected survivor's latest queued task.");
            AddInput(PlatformInput.InputButton.Context, "context", "Context / Finish Move", "Gameplay", KeyCode.Space, KeyCode.None, "Runs context-sensitive actions, such as opening the activity log or finishing object movement.");
            AddInput(PlatformInput.InputButton.Clipboard, "clipboard", "Toggle Status Dropdowns", "UI", KeyCode.G, KeyCode.None, "Shows or hides the avatar and time dropdown panels.");
            AddInput(PlatformInput.InputButton.Cancel, "cancel", "Back / Cancel Action", "UI", KeyCode.Escape, KeyCode.None, "Backs out of menus or cancels placement. Escape is reserved while rebinding; use Reset to restore it.");
            AddInput(PlatformInput.InputButton.Pause, "pause", "Pause / Resume", "System", KeyCode.Escape, KeyCode.None, "Pauses or resumes the game while the shelter is accepting input. Escape is reserved while rebinding; use Reset to restore it.", InputContext.System);
            AddInput(PlatformInput.InputButton.Info, "info", "Close Info Dropdown", "UI", KeyCode.I, KeyCode.None, "Closes the open avatar info dropdown when available.");
            AddInput(PlatformInput.InputButton.Focus, "focus", "Focus Selected Survivor", "Gameplay", KeyCode.Space, KeyCode.None, "Centers the camera on the selected survivor.");
            AddInput(PlatformInput.InputButton.GoHere, "go_here", "Move Selected Survivor", "Gameplay", KeyCode.Mouse0, KeyCode.None, "Sends the selected survivor to the cursor position, or selects the survivor under the cursor.");
            AddInput(PlatformInput.InputButton.NextChar, "next_character", "Select Next Survivor", "Gameplay", KeyCode.E, KeyCode.None, "Selects the next available survivor.");
            AddInput(PlatformInput.InputButton.PrevChar, "previous_character", "Select Previous Survivor", "Gameplay", KeyCode.Q, KeyCode.None, "Selects the previous available survivor.");
            AddInput(PlatformInput.InputButton.Zoom, "zoom_modifier", "Zoom Toggle / Scroll Modifier", "Camera", KeyCode.LeftControl, KeyCode.None, "Toggles shelter zoom when pressed. When held, scroll gestures can use it as the zoom modifier.");
            AddInput(PlatformInput.InputButton.CameraSpeed, "camera_speed", "Fast Forward / Fast Camera", "Camera", KeyCode.LeftShift, KeyCode.None, "Speeds up camera panning and also holds fast-forward time while normal gameplay is active.");
            AddInput(PlatformInput.InputButton.ToggleAutomation, "toggle_automation", "Toggle Survivor Automation", "Gameplay", KeyCode.H, KeyCode.Home, "Turns automation on or off for the selected survivor.");
            AddInput(PlatformInput.InputButton.AcceptTransmission, "accept_transmission", "Accept Radio Transmission", "UI", KeyCode.R, KeyCode.None, "Accepts any incoming radio transmission.");
            AddInput(PlatformInput.InputButton.OpenMap, "open_map", "Open / Close Expedition Map", "UI", KeyCode.M, KeyCode.None, "Opens the expedition map, or closes it when the map is already the top panel.");
            AddInput(PlatformInput.InputButton.SlowDown, "slow_down", "Toggle Slow Motion", "System", KeyCode.CapsLock, KeyCode.None, "Toggles slow-motion simulation while normal gameplay is active.", InputContext.System);
            AddInput(PlatformInput.InputButton.SkipCutscene, "skip_cutscene", "Skip Cutscene", "Cinematics", KeyCode.Escape, KeyCode.None, "Skips the current skippable cutscene. Escape is reserved while rebinding; use Reset to restore it.", InputContext.System);
            AddInput(PlatformInput.InputButton.SkipSpeech, "skip_speech", "Advance Dialogue", "Cinematics", KeyCode.Space, KeyCode.None, "Advances dialogue and speech bubbles during cutscenes.");

            AddMenu(PlatformInput.MenuInputButton.UIselect, "select", "Menu Select / Confirm", "Menu", KeyCode.Mouse0, KeyCode.None, "Selects the highlighted menu item when a Sheltered menu polls keyboard/mouse menu input.");
            AddMenu(PlatformInput.MenuInputButton.UIcancel, "cancel", "Menu Back / Cancel", "Menu", KeyCode.Escape, KeyCode.None, "Cancels or backs out of active menus. Escape is reserved while rebinding; use Reset to restore it.");
            AddMenu(PlatformInput.MenuInputButton.UIstart, "start", "Menu Start / Alternate Confirm", "Menu", KeyCode.Space, KeyCode.None, "Confirms start-menu prompts that accept the alternate menu confirm input.");
            AddMenu(PlatformInput.MenuInputButton.UIdragMap, "drag_map", "Drag Expedition Map", "Menu", KeyCode.Mouse1, KeyCode.None, "Drags the expedition map while held.");
            AddMenu(PlatformInput.MenuInputButton.UIdragWaypoint, "drag_waypoint", "Drag Expedition Waypoint", "Menu", KeyCode.Mouse0, KeyCode.None, "Starts and releases waypoint dragging on the expedition map.");
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

        private static void AddMenuAlias(PlatformInput.MenuInputButton button, string sourceActionId)
        {
            if (string.IsNullOrEmpty(sourceActionId)) return;
            MenuAliasActionIds[button] = sourceActionId;
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
