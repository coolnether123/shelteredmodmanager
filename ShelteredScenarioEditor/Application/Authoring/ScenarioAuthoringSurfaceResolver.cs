using System;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Composition;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;

namespace ShelteredScenarioEditor.Application.Authoring{
    internal enum ScenarioAuthoringSurfaceKind
    {
        Inactive = 0,
        TextField = 1,
        PixelEditor = 2,
        Modal = 3,
        AuthoringWindow = 4,
        Placement = 5,
        Selection = 6,
        AuthoringWorld = 7
    }

    internal sealed class ScenarioAuthoringSurfaceState
    {
        public ScenarioAuthoringSurfaceKind Kind { get; set; }
        public string Description { get; set; }
        public string ActionId { get; set; }
        public ScenarioAuthoringCommand Command { get; set; }
        public string WindowId { get; set; }
    }

    internal sealed class ScenarioAuthoringSurfaceResolver
    {
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;
        private readonly ScenarioSceneSpritePlacementAuthoringService _sceneSpritePlacement;

        public ScenarioAuthoringSurfaceResolver(
            ScenarioBuildPlacementAuthoringService buildPlacement = null,
            ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacement = null)
        {
            _buildPlacement = buildPlacement;
            _sceneSpritePlacement = sceneSpritePlacement;
        }

        public ScenarioAuthoringSurfaceState Resolve(
            ScenarioAuthoringState state,
            ScenarioAuthoringInputCaptureService inputCapture,
            bool pixelEditorOpen)
        {
            if (state == null || !state.IsActive)
                return Surface(ScenarioAuthoringSurfaceKind.Inactive, "Scenario authoring is not active.");

            if (inputCapture != null && inputCapture.TextFieldFocused)
                return Surface(ScenarioAuthoringSurfaceKind.TextField, "Focused text field");

            if (pixelEditorOpen)
                return Surface(ScenarioAuthoringSurfaceKind.PixelEditor, "Pixel editor", SpriteSwapCommand.CancelPicker(), ScenarioAuthoringWindowIds.PixelEditor);

            ScenarioAuthoringSurfaceState modal = ResolveModalSurface(state, inputCapture);
            if (modal != null)
                return modal;

            ScenarioAuthoringSurfaceState window = ResolveFrontmostWindow(state);
            if (window != null)
                return window;

            ScenarioAuthoringSurfaceState placement = ResolvePlacementSurface();
            if (placement != null)
                return placement;

            if (state.SelectedTarget != null)
                return Surface(ScenarioAuthoringSurfaceKind.Selection, "Selection", SelectionCommand.Clear(), null);

            return Surface(ScenarioAuthoringSurfaceKind.AuthoringWorld, "Authoring world");
        }

        private static ScenarioAuthoringSurfaceState ResolveModalSurface(ScenarioAuthoringState state, ScenarioAuthoringInputCaptureService inputCapture)
        {
            if (state == null)
                return null;

            if (state.GlobalSearchOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Search", ShellUxCommand.Simple(ShellUxCommandKind.CloseGlobalSearch, ScenarioAuthoringActionIds.ActionShellCloseGlobalSearch), null);
            if (!string.IsNullOrEmpty(state.FocusedEditorKind))
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Focused editor", ResolveFocusedEditorCancelAction(state.FocusedEditorKind), null);
            if (state.SpriteSwapPicker != null && state.SpriteSwapPicker.IsOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Asset picker", SpriteSwapCommand.CancelPicker(), null);
            if (state.HelpWindowOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Workshop help", ShellUxCommand.Simple(ShellUxCommandKind.CloseHelp, ScenarioAuthoringActionIds.ActionShellCloseHelp), null);
            if (state.SettingsWindowOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Editor settings", ShellUxCommand.Simple(ShellUxCommandKind.CloseSettings, ScenarioAuthoringActionIds.ActionShellCloseSettings), ScenarioAuthoringWindowIds.Settings);
            if (state.WindowMenuOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Windows menu", ShellUxCommand.Simple(ShellUxCommandKind.ToggleWindowMenu, ScenarioAuthoringActionIds.ActionShellToggleWindowMenu), null);
            if (inputCapture != null && inputCapture.PopupOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Popup", (string)null, null);

            return null;
        }

        private static EditorLifecycleCommand ResolveFocusedEditorCancelAction(string focusedEditorKind)
        {
            if (string.Equals(focusedEditorKind, ScenarioBaseModeAuthoringActions.FocusedEditorKind, StringComparison.OrdinalIgnoreCase))
                return EditorLifecycleCommand.CancelBaseModeSwitch;

            return EditorLifecycleCommand.CancelFocusedEditor;
        }

        private static ScenarioAuthoringSurfaceState ResolveFrontmostWindow(ScenarioAuthoringState state)
        {
            if (state == null || state.WindowStates == null)
                return null;

            ScenarioAuthoringWindowState frontmost = null;
            for (int i = 0; i < state.WindowStates.Count; i++)
            {
                ScenarioAuthoringWindowState window = state.WindowStates[i];
                if (!IsDismissibleWindow(window))
                    continue;

                if (frontmost == null || window.ZIndex > frontmost.ZIndex)
                    frontmost = window;
            }

            return frontmost != null
                ? Surface(ScenarioAuthoringSurfaceKind.AuthoringWindow, "Authoring window", ShellUxCommand.ToggleWindow(frontmost.Id), frontmost.Id)
                : null;
        }

        private static bool IsDismissibleWindow(ScenarioAuthoringWindowState window)
        {
            if (window == null || !window.Visible || window.Collapsed || string.IsNullOrEmpty(window.Id))
                return false;

            if (string.Equals(window.Id, ScenarioAuthoringWindowIds.Scenario, StringComparison.OrdinalIgnoreCase)
                || string.Equals(window.Id, ScenarioAuthoringWindowIds.PixelEditor, StringComparison.OrdinalIgnoreCase)
                || string.Equals(window.Id, ScenarioAuthoringWindowIds.Settings, StringComparison.OrdinalIgnoreCase)
                || string.Equals(window.Id, ScenarioAuthoringWindowIds.Inspector, StringComparison.OrdinalIgnoreCase)
                || string.Equals(window.Id, ScenarioAuthoringWindowIds.BuildTools, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }

        private ScenarioAuthoringSurfaceState ResolvePlacementSurface()
        {
            if (_buildPlacement != null && _buildPlacement.HasActivePlacement)
                return Surface(ScenarioAuthoringSurfaceKind.Placement, "Build placement", BuildPlacementCommand.Cancel(), null);

            if (_sceneSpritePlacement != null && _sceneSpritePlacement.HasActivePlacement)
                return Surface(ScenarioAuthoringSurfaceKind.Placement, "Scene sprite placement", SceneSpritePlacementCommand.Cancel(), null);

            return null;
        }

        private static ScenarioAuthoringSurfaceState Surface(ScenarioAuthoringSurfaceKind kind, string description, string actionId = null, string windowId = null)
        {
            return new ScenarioAuthoringSurfaceState
            {
                Kind = kind,
                Description = description ?? kind.ToString(),
                ActionId = actionId,
                WindowId = windowId
            };
        }

        private static ScenarioAuthoringSurfaceState Surface(ScenarioAuthoringSurfaceKind kind, string description, ScenarioAuthoringCommand command, string windowId)
        {
            ScenarioAuthoringSurfaceState surface = Surface(kind, description, command != null ? command.AutomationId : null, windowId);
            surface.Command = command;
            return surface;
        }
    }
}
