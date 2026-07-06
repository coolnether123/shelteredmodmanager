using System;

using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;

namespace ShelteredAPI.Scenarios.Application.Authoring{
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
        public string WindowId { get; set; }
    }

    internal sealed class ScenarioAuthoringSurfaceResolver
    {
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
                return Surface(ScenarioAuthoringSurfaceKind.PixelEditor, "Pixel editor", ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, ScenarioAuthoringWindowIds.PixelEditor);

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
                return Surface(ScenarioAuthoringSurfaceKind.Selection, "Selection", ScenarioAuthoringActionIds.ActionSelectionClear, null);

            return Surface(ScenarioAuthoringSurfaceKind.AuthoringWorld, "Authoring world");
        }

        private static ScenarioAuthoringSurfaceState ResolveModalSurface(ScenarioAuthoringState state, ScenarioAuthoringInputCaptureService inputCapture)
        {
            if (state == null)
                return null;

            if (!string.IsNullOrEmpty(state.FocusedEditorKind))
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Focused editor", ResolveFocusedEditorCancelAction(state.FocusedEditorKind), null);
            if (state.SpriteSwapPicker != null && state.SpriteSwapPicker.IsOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Asset picker", ScenarioAuthoringActionIds.ActionSpriteSwapPickerCancel, null);
            if (state.HelpWindowOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Workshop help", ScenarioAuthoringActionIds.ActionShellCloseHelp, null);
            if (state.SettingsWindowOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Editor settings", ScenarioAuthoringActionIds.ActionShellCloseSettings, ScenarioAuthoringWindowIds.Settings);
            if (state.WindowMenuOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Windows menu", ScenarioAuthoringActionIds.ActionShellToggleWindowMenu, null);
            if (inputCapture != null && inputCapture.PopupOpen)
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Popup", null, null);

            return null;
        }

        private static string ResolveFocusedEditorCancelAction(string focusedEditorKind)
        {
            if (string.Equals(focusedEditorKind, ScenarioStoryFocusedEditorActions.FocusedEditorKind, StringComparison.OrdinalIgnoreCase))
                return ScenarioStoryFocusedEditorActions.ActionCancel;
            if (string.Equals(focusedEditorKind, ScenarioBaseModeAuthoringActions.FocusedEditorKind, StringComparison.OrdinalIgnoreCase))
                return ScenarioBaseModeAuthoringActions.ActionSwitchCancel;

            return ScenarioAuthoringActionIds.ActionFocusedEditorCancel;
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
                ? Surface(ScenarioAuthoringSurfaceKind.AuthoringWindow, "Authoring window", ScenarioAuthoringActionIds.ActionWindowTogglePrefix + frontmost.Id, frontmost.Id)
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

        private static ScenarioAuthoringSurfaceState ResolvePlacementSurface()
        {
            if (ScenarioBuildPlacementAuthoringService.Instance.HasActivePlacement)
                return Surface(ScenarioAuthoringSurfaceKind.Placement, "Build placement", ScenarioAuthoringActionIds.ActionBuildPlacementCancel, null);

            try
            {
                ScenarioSceneSpritePlacementAuthoringService sceneSpritePlacement = ScenarioCompositionRoot.Resolve<ScenarioSceneSpritePlacementAuthoringService>();
                if (sceneSpritePlacement != null && sceneSpritePlacement.HasActivePlacement)
                    return Surface(ScenarioAuthoringSurfaceKind.Placement, "Scene sprite placement", ScenarioAuthoringActionIds.ActionSceneSpritePlacementCancel, null);
            }
            catch
            {
            }

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
    }
}
