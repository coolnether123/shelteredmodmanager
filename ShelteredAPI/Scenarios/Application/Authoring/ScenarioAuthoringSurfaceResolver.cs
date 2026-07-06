using System;

using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;

namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal enum ScenarioAuthoringSurfaceKind
    {
        Inactive = 0,
        TextField = 1,
        PixelEditor = 2,
        Modal = 3,
        AuthoringWorld = 4
    }

    internal sealed class ScenarioAuthoringSurfaceState
    {
        public ScenarioAuthoringSurfaceKind Kind { get; set; }
        public string Description { get; set; }
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
                return Surface(ScenarioAuthoringSurfaceKind.PixelEditor, "Pixel editor");

            if (IsModalSurfaceOpen(state, inputCapture))
                return Surface(ScenarioAuthoringSurfaceKind.Modal, "Modal or picker");

            return Surface(ScenarioAuthoringSurfaceKind.AuthoringWorld, "Authoring world");
        }

        private static bool IsModalSurfaceOpen(ScenarioAuthoringState state, ScenarioAuthoringInputCaptureService inputCapture)
        {
            if (state == null)
                return false;

            return state.HelpWindowOpen
                || state.SettingsWindowOpen
                || state.WindowMenuOpen
                || !string.IsNullOrEmpty(state.FocusedEditorKind)
                || (state.SpriteSwapPicker != null && state.SpriteSwapPicker.IsOpen)
                || (inputCapture != null && inputCapture.PopupOpen);
        }

        private static ScenarioAuthoringSurfaceState Surface(ScenarioAuthoringSurfaceKind kind, string description)
        {
            return new ScenarioAuthoringSurfaceState
            {
                Kind = kind,
                Description = description ?? kind.ToString()
            };
        }
    }
}
