using System;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    // Renders the Keyboard Shortcuts view inside the workshop help modal. The
    // model is generated from ScenarioAuthoringShortcutCatalog, so this renderer
    // only lays out parchment rows of kbd-style key chips and descriptions,
    // grouped by context, and highlights the currently-active context group.
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private void DrawHelpShortcutsBody(ScenarioAuthoringShortcutOverlayViewModel shortcuts)
        {
            GUILayout.Label("KEYBOARD SHORTCUTS", _sectionTitleStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Press F1 any time to open this reference. The highlighted group matches your current editing context.", _mutedTextStyle);
            GUILayout.Space(8f);

            ScenarioAuthoringShortcutGroupViewModel[] groups = shortcuts != null ? shortcuts.Groups : null;
            int groupCount = groups != null ? groups.Length : 0;
            int leftCount = (groupCount + 1) / 2;
            float width = Math.Max(220f, (GetSectionContentWidth() - 10f) * 0.5f);
            bool roomy = _chromeViewportRect.height >= 820f;
            GUILayout.BeginHorizontal();
            for (int column = 0; column < 2; column++)
            {
                if (column > 0) GUILayout.Space(10f);
                GUILayout.BeginVertical(GUILayout.Width(width));
                int start = column == 0 ? 0 : leftCount;
                int end = column == 0 ? leftCount : groupCount;
                for (int g = start; g < end; g++)
                {
                    ScenarioAuthoringShortcutGroupViewModel group = groups[g];
                    if (group == null) continue;
                    string key = "shortcuts.group." + (group.Title ?? g.ToString()).ToLowerInvariant();
                    bool expanded = ScenarioAuthoringRendererInteractionState.Instance.GetDisclosureExpanded(key, roomy && group.IsActiveContext);
                    Rect header = GUILayoutUtility.GetRect(120f, 28f, GUILayout.ExpandWidth(true), GUILayout.Height(28f));
                    string label = (expanded ? "v " : "> ") + (group.Title ?? "Shortcuts") + (group.IsActiveContext ? "  -  ACTIVE" : string.Empty);
                    if (DrawPlainButton(header, new GUIContent(label), group.IsActiveContext ? _activeButtonStyle : _buttonStyle, true))
                    {
                        ScenarioAuthoringBackendService.Instance.ExecuteAction(ScenarioAuthoringRendererActionManifest.BuildTokenAction(ScenarioAuthoringActionIds.ActionRendererWorkshopGroupTogglePrefix, key));
                        if (Event.current != null) Event.current.Use();
                        expanded = ScenarioAuthoringRendererInteractionState.Instance.GetDisclosureExpanded(key, roomy && group.IsActiveContext);
                    }
                    if (expanded)
                    {
                        ScenarioAuthoringShortcutRowViewModel[] rows = group.Rows;
                        for (int r = 0; rows != null && r < rows.Length; r++)
                        {
                            ScenarioAuthoringShortcutRowViewModel row = rows[r];
                            if (row == null) continue;
                            GUILayout.BeginHorizontal();
                            GUILayout.Label(row.KeyChord ?? string.Empty, _uiContext.Styles.Pill, GUILayout.Width(118f));
                            GUILayout.Label(row.Description ?? string.Empty, _textStyle);
                            GUILayout.EndHorizontal();
                            GUILayout.Space(3f);
                        }
                    }
                    GUILayout.Space(8f);
                }
                GUILayout.EndVertical();
            }
            GUILayout.EndHorizontal();
        }

    }
}
