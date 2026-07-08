using System;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private Rect BuildPlacementHudRect(float scaledWidth, float scaledHeight)
        {
            float width = Mathf.Clamp(scaledWidth - 48f, 520f, 760f);
            float height = 94f;
            return new Rect(
                (scaledWidth - width) * 0.5f,
                scaledHeight - height - 16f,
                width,
                height);
        }

        private void DrawPlacementHudCore(Rect rect, ScenarioAuthoringShellWindowViewModel[] windows)
        {
            ScenarioAuthoringShellWindowViewModel buildTools = FindWindow(windows, ScenarioAuthoringWindowIds.BuildTools);
            DrawChromePanel(rect, _rootPanelStyle);

            Rect thumbRect = new Rect(rect.x + 12f, rect.y + 12f, 70f, 70f);
            Sprite preview = ResolvePlacementPreviewSprite(buildTools);
            if (preview != null)
                DrawSpritePreview(thumbRect, preview, true);
            else
                GUI.Box(thumbRect, "PL", _activeButtonStyle);

            float textX = thumbRect.xMax + 12f;
            float actionWidth = rect.width >= 660f ? 118f : 100f;
            float actionGap = 8f;
            float rightWidth = (actionWidth * 4f) + (actionGap * 3f);
            Rect textRect = new Rect(textX, rect.y + 12f, Math.Max(120f, rect.xMax - textX - rightWidth - 18f), 70f);
            string label = ResolveActivePlacementLabel(buildTools);
            GUI.Label(new Rect(textRect.x, textRect.y, textRect.width, 24f), ShortenToFit(label, textRect.width, _smallTitleStyle), _smallTitleStyle);
            GUI.Label(new Rect(textRect.x, textRect.y + 28f, textRect.width, 20f), ResolvePlacementValidityLabel(buildTools), _mutedTextStyle);
            GUI.Label(new Rect(textRect.x, textRect.y + 50f, textRect.width, 20f), "Shift free-place / snap override", _mutedTextStyle);

            bool snapOn = _snapshot == null
                || _snapshot.State == null
                || _snapshot.State.Settings == null
                || _snapshot.State.Settings.GetBool("visuals.snap_to_grid", true);
            float x = rect.xMax - rightWidth - 12f;
            DrawButton(new Rect(x, rect.y + 18f, actionWidth, 28f), new ScenarioAuthoringInspectorAction
            {
                Id = ScenarioAuthoringActionIds.ActionSettingTogglePrefix + "visuals.snap_to_grid",
                Label = snapOn ? "Snap On" : "Snap Off",
                Hint = "Toggle default scene-sprite snapping. Hold Shift for the temporary opposite.",
                Enabled = true,
                Emphasized = snapOn,
                IconText = "SN"
            }, false);
            x += actionWidth + actionGap;

            DrawDisabledPlacementHudButton(new Rect(x, rect.y + 18f, actionWidth, 28f), "Rotate/Flip", "No orientation action is exposed for this asset.");
            x += actionWidth + actionGap;

            if (DrawPlacementHudButton(new Rect(x, rect.y + 18f, actionWidth, 28f), "Back", "Return to the full asset browser."))
                CancelActivePlacement();
            x += actionWidth + actionGap;

            if (DrawPlacementHudButton(new Rect(x, rect.y + 18f, actionWidth, 28f), "Done", "Stop placement and close the Tool Workspace."))
            {
                CancelActivePlacement();
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionWindowTogglePrefix + ScenarioAuthoringWindowIds.BuildTools);
            }
        }

        private bool DrawPlacementHudButton(Rect rect, string label, string tooltip)
        {
            return DrawPlainButton(rect, new GUIContent(label, tooltip), _buttonStyle, true);
        }

        private void DrawDisabledPlacementHudButton(Rect rect, string label, string tooltip)
        {
            GUIStyle style = _uiContext != null && _uiContext.Styles != null ? _uiContext.Styles.ButtonDisabled : _buttonStyle;
            DrawPlainButton(rect, new GUIContent(label, tooltip), style, false);
        }

        private void CancelActivePlacement()
        {
            if (IsBuildPlacementActive())
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionBuildPlacementCancel);
            if (IsSceneSpritePlacementActive())
                ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringActionIds.ActionSceneSpritePlacementCancel);
        }

        private static Sprite ResolvePlacementPreviewSprite(ScenarioAuthoringShellWindowViewModel window)
        {
            ScenarioAuthoringInspectorSection selected = FindSection(window, "asset_browser_selected");
            ScenarioAuthoringInspectorItem preview = FindPreviewItem(selected);
            if (preview != null)
                return preview.PreviewSprite;

            for (int i = 0; window != null && window.Sections != null && i < window.Sections.Length; i++)
            {
                ScenarioAuthoringInspectorSection section = window.Sections[i];
                for (int j = 0; section != null && section.Items != null && j < section.Items.Length; j++)
                {
                    ScenarioAuthoringInspectorItem item = section.Items[j];
                    if (item != null && item.Action != null && item.Action.Emphasized && item.Action.PreviewSprite != null)
                        return item.Action.PreviewSprite;
                }
            }

            return null;
        }

        private static ScenarioAuthoringInspectorItem FindPreviewItem(ScenarioAuthoringInspectorSection section)
        {
            for (int i = 0; section != null && section.Items != null && i < section.Items.Length; i++)
            {
                ScenarioAuthoringInspectorItem item = section.Items[i];
                if (item != null && item.PreviewSprite != null)
                    return item;
            }

            return null;
        }
    }
}
