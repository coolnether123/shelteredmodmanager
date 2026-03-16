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
                MMLog.WriteInfo("[ShelteredVanillaInputActions] Runtime keybinds loaded from provider.");
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
            string actionId = InputPrefix + "action";
            string cancelId = InputPrefix + "cancel";
            string contextId = InputPrefix + "context";
            string goHereId = InputPrefix + "go_here";

            AddInput(PlatformInput.InputButton.Action, "action", "Primary Action", "Gameplay", KeyCode.Mouse0, KeyCode.None, "Used for the main in-world action, such as selecting, confirming, or interacting.");
            AddInput(PlatformInput.InputButton.Interact, "interact", "Secondary Action", "Gameplay", KeyCode.Mouse1, KeyCode.None, "Used for secondary interactions in the world.");
            AddInput(PlatformInput.InputButton.CancelJob, "cancel_job", "Cancel Assigned Task", "Gameplay", KeyCode.C, KeyCode.None, "Cancels the selected survivor's current task.");
            AddInput(PlatformInput.InputButton.Context, "context", "Context Menu / Action", "Gameplay", KeyCode.Space, KeyCode.None, "Opens or confirms context-sensitive actions for the current selection.");
            AddInput(PlatformInput.InputButton.Clipboard, "clipboard", "Open Clipboard", "UI", KeyCode.G, KeyCode.None, "Opens the clipboard screen.");
            AddInput(PlatformInput.InputButton.Cancel, "cancel", "Back / Cancel", "UI", KeyCode.Escape, KeyCode.None, "Backs out of menus or cancels the current action.");
            AddInput(PlatformInput.InputButton.Pause, "pause", "Pause Game", "System", KeyCode.Escape, KeyCode.None, "Pauses or unpauses the game.", InputContext.System);
            AddInput(PlatformInput.InputButton.Info, "info", "Open Info Panel", "UI", KeyCode.I, KeyCode.None, "Opens the info panel for the current selection when available.");
            AddInput(PlatformInput.InputButton.Focus, "focus", "Focus Camera", "Gameplay", KeyCode.Space, KeyCode.None, "Focuses the camera on the current selection or point of interest.");
            AddInput(PlatformInput.InputButton.GoHere, "go_here", "Move Here", "Gameplay", KeyCode.Mouse0, KeyCode.None, "Sends the selected survivor to the chosen location.");
            AddInput(PlatformInput.InputButton.NextChar, "next_character", "Next Survivor", "Gameplay", KeyCode.E, KeyCode.None, "Selects the next survivor.");
            AddInput(PlatformInput.InputButton.PrevChar, "previous_character", "Previous Survivor", "Gameplay", KeyCode.Q, KeyCode.None, "Selects the previous survivor.");
            AddInput(PlatformInput.InputButton.Zoom, "zoom_modifier", "Hold to Zoom", "Camera", KeyCode.LeftControl, KeyCode.None, "Hold this key to make scroll input zoom where supported.");
            AddInput(PlatformInput.InputButton.CameraSpeed, "camera_speed", "Fast Camera Move", "Camera", KeyCode.LeftShift, KeyCode.None, "Hold this key to move the camera faster.");
            AddInput(PlatformInput.InputButton.ToggleAutomation, "toggle_automation", "Toggle Automation", "Gameplay", KeyCode.H, KeyCode.Home, "Turns automation on or off for the selected survivor.");
            AddInput(PlatformInput.InputButton.AcceptTransmission, "accept_transmission", "Accept Transmission", "UI", KeyCode.R, KeyCode.None, "Accepts an incoming radio transmission.");
            AddInput(PlatformInput.InputButton.Dismiss, "dismiss", "Dismiss Prompt", "UI", KeyCode.Mouse0, KeyCode.None, "Dismisses the current popup or prompt.");
            AddInput(PlatformInput.InputButton.OpenMap, "open_map", "Open Expedition Map", "UI", KeyCode.M, KeyCode.None, "Opens the expedition map.");
            AddInput(PlatformInput.InputButton.SlowDown, "slow_down", "Slow Down Time", "System", KeyCode.CapsLock, KeyCode.None, "Slows down the simulation speed while active.", InputContext.System);
            AddInput(PlatformInput.InputButton.SkipCutscene, "skip_cutscene", "Skip Cutscene", "Cinematics", KeyCode.Escape, KeyCode.None, "Skips the current cutscene when allowed.", InputContext.System);
            AddInput(PlatformInput.InputButton.SkipSpeech, "skip_speech", "Skip Dialogue", "Cinematics", KeyCode.Space, KeyCode.None, "Advances or skips dialogue and speech scenes.");

            AddMenu(PlatformInput.MenuInputButton.UIdragMap, "drag_map", "Drag Map", "Menu", KeyCode.Mouse1, KeyCode.None, "Drags the map view while held.");

            // Internal menu aliases (not user-facing in keybind UI) to avoid redundant entries.
            AddMenuAlias(PlatformInput.MenuInputButton.UIselect, actionId);
            AddMenuAlias(PlatformInput.MenuInputButton.UIcancel, cancelId);
            AddMenuAlias(PlatformInput.MenuInputButton.UIextra1, actionId);
            AddMenuAlias(PlatformInput.MenuInputButton.UIextra2, actionId);
            AddMenuAlias(PlatformInput.MenuInputButton.UIextra3, actionId);
            AddMenuAlias(PlatformInput.MenuInputButton.UIextra4, actionId);
            AddMenuAlias(PlatformInput.MenuInputButton.UITabRight, actionId);
            AddMenuAlias(PlatformInput.MenuInputButton.UITabLeft, actionId);
            AddMenuAlias(PlatformInput.MenuInputButton.UIstart, contextId);
            AddMenuAlias(PlatformInput.MenuInputButton.UIdragWaypoint, goHereId);
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
