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
        private void DrawHelpShortcutsBody(ScenarioAuthoringShortcutOverlayViewModel shortcuts, float availableWidth)
        {
            GUILayout.Label("KEYBOARD SHORTCUTS", _sectionTitleStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Press F1 any time to open this reference. The highlighted group matches your current editing context.", _mutedTextStyle);
            GUILayout.Space(8f);

            ScenarioAuthoringShortcutGroupViewModel[] groups = shortcuts != null ? shortcuts.Groups : null;
            int groupCount = groups != null ? groups.Length : 0;
            int columnCount = availableWidth >= 500f ? 2 : 1;
            int groupsPerColumn = (groupCount + columnCount - 1) / columnCount;
            float columnGap = 10f;
            float scrollBarAllowance = 18f;
            float layoutWidth = Math.Max(120f, availableWidth - scrollBarAllowance);
            float columnWidth = columnCount == 1
                ? layoutWidth
                : Math.Max(120f, (layoutWidth - columnGap) * 0.5f);
            bool stackRows = columnCount == 1 && columnWidth < 300f;
            bool roomy = _chromeViewportRect.height >= 820f;
            GUILayout.BeginHorizontal();
            for (int column = 0; column < columnCount; column++)
            {
                if (column > 0) GUILayout.Space(columnGap);
                GUILayout.BeginVertical(GUILayout.Width(columnWidth));
                int start = column * groupsPerColumn;
                int end = Math.Min(groupCount, start + groupsPerColumn);
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
                            if (stackRows)
                            {
                                GUILayout.Label(row.KeyChord ?? string.Empty, _uiContext.Styles.Pill, GUILayout.Width(Math.Min(118f, columnWidth)));
                                GUILayout.Label(row.Description ?? string.Empty, _textStyle);
                            }
                            else
                            {
                                GUILayout.BeginHorizontal();
                                GUILayout.Label(row.KeyChord ?? string.Empty, _uiContext.Styles.Pill, GUILayout.Width(118f));
                                GUILayout.Label(row.Description ?? string.Empty, _textStyle);
                                GUILayout.EndHorizontal();
                            }
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
