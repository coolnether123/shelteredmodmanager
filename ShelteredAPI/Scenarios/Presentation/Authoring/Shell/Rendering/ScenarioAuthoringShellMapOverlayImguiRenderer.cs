using System.Globalization;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Presentation.UiKit.Widgets;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private void DrawMapAuthoringOverlayCore(
            float scaledWidth,
            float scaledHeight,
            ScenarioAuthoringInputCaptureService inputCapture)
        {
            ScenarioAuthoringState state = _snapshot != null ? _snapshot.State : null;
            ScenarioMapRegionSelection selection = state != null ? state.MapSelection : null;
            Rect rect = BuildMapAuthoringOverlayRect(scaledWidth, scaledHeight);
            RegisterVisualSurface("map.authoring.card", rect);
            using (EnterVisualSurface("map.authoring.card"))
                DrawMapAuthoringCard(rect, selection);
            if (inputCapture != null)
                inputCapture.RegisterInteractiveRect(rect);
        }

        private Rect BuildMapAuthoringOverlayRect(float scaledWidth, float scaledHeight)
        {
            float width = Mathf.Min(360f, Mathf.Max(300f, scaledWidth - (Margin * 2f)));
            float height = 190f;
            return new Rect(Margin, Mathf.Max(Margin, scaledHeight - height - Margin), width, height);
        }

        private void DrawMapAuthoringCard(Rect rect, ScenarioMapRegionSelection selection)
        {
            GUI.Box(rect, GUIContent.none, _rootPanelStyle);
            Rect inner = new Rect(rect.x + 12f, rect.y + 10f, rect.width - 24f, rect.height - 20f);
            GUI.Label(new Rect(inner.x, inner.y, inner.width, 22f), "Map Authoring", _smallTitleStyle);

            float y = inner.y + 28f;
            if (selection == null)
            {
                GUI.Label(new Rect(inner.x, y, inner.width, 44f), "Select a vanilla region on the map.", _textStyle);
                y += 52f;
                GUI.Label(new Rect(inner.x, y, inner.width, 36f), "Escape or Close returns to the Map workshop page.", _mutedTextStyle);
            }
            else
            {
                GUI.Label(new Rect(inner.x, y, inner.width, 22f), selection.DisplayName ?? "<unnamed>", _textStyle);
                y += 23f;
                GUI.Label(new Rect(inner.x, y, inner.width, 20f), "Grid " + selection.GridX.ToString(CultureInfo.InvariantCulture) + "," + selection.GridY.ToString(CultureInfo.InvariantCulture) + "  " + SafeOverlay(selection.Topography), _mutedTextStyle);
                y += 21f;
                GUI.Label(new Rect(inner.x, y, inner.width, 20f), FormatOverlayFlags(selection), _mutedTextStyle);
                y += 21f;
                GUI.Label(new Rect(inner.x, y, inner.width, 20f), selection.Captured ? "Captured as " + SafeOverlay(selection.CapturedLocationId) : "Not captured", _mutedTextStyle);
            }

            Rect closeRect = new Rect(inner.x, inner.yMax - 32f, 94f, 28f);
            if (GUI.Button(closeRect, "Close", _buttonStyle))
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionMapAuthoringClose);

            Rect captureRect = new Rect(closeRect.xMax + 8f, closeRect.y, 138f, 28f);
            bool canCapture = selection != null;
            GUIStyle captureStyle = canCapture ? _activeButtonStyle : _buttonStyle;
            if (GUI.Button(captureRect, selection != null && selection.Captured ? "Update Draft" : "Capture", captureStyle) && canCapture)
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionMapAuthoringCaptureSelection);

            if (!canCapture)
                ScenarioUiWidgets.DrawPill(new Rect(captureRect.xMax + 8f, captureRect.y + 5f, 74f, 18f), "Select", _uiContext.Styles, ScenarioUiPillEmphasis.Default);
        }

        private static string FormatOverlayFlags(ScenarioMapRegionSelection selection)
        {
            if (selection == null)
                return string.Empty;

            string visible = selection.VisibleOnMap ? "visible" : "hidden";
            string discovered = selection.Discovered ? "discovered" : "undiscovered";
            string searchable = selection.Searchable ? "searchable" : "not searchable";
            return visible + ", " + discovered + ", " + searchable;
        }

        private static string SafeOverlay(string value)
        {
            return string.IsNullOrEmpty(value) ? "<none>" : value;
        }
    }
}
