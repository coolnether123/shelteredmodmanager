using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;

namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    // Renders the Keyboard Shortcuts view inside the workshop help modal. The
    // model is generated from ScenarioAuthoringShortcutCatalog, so this renderer
    // only lays out parchment rows of kbd-style key chips and descriptions,
    // grouped by context, and highlights the currently-active context group.
    internal sealed partial class ScenarioAuthoringShellImguiRenderModule
    {
        private Vector2 _shortcutsScroll;

        private void DrawHelpShortcutsBody(ScenarioAuthoringShortcutOverlayViewModel shortcuts)
        {
            GUILayout.Label("KEYBOARD SHORTCUTS", _sectionTitleStyle);
            GUILayout.Space(4f);
            GUILayout.Label("Press F1 any time to open this reference. The highlighted group matches your current editing context.", _mutedTextStyle);
            GUILayout.Space(8f);

            _shortcutsScroll = GUILayout.BeginScrollView(_shortcutsScroll);
            ScenarioAuthoringShortcutGroupViewModel[] groups = shortcuts != null ? shortcuts.Groups : null;
            for (int g = 0; groups != null && g < groups.Length; g++)
            {
                ScenarioAuthoringShortcutGroupViewModel group = groups[g];
                if (group == null)
                    continue;

                DrawShortcutGroupHeader(group);
                ScenarioAuthoringShortcutRowViewModel[] rows = group.Rows;
                for (int r = 0; rows != null && r < rows.Length; r++)
                {
                    ScenarioAuthoringShortcutRowViewModel row = rows[r];
                    if (row == null)
                        continue;

                    GUILayout.BeginHorizontal();
                    GUILayout.Label(row.KeyChord ?? string.Empty, _uiContext.Styles.Pill, GUILayout.Width(174f));
                    GUILayout.Label(row.Description ?? string.Empty, _textStyle);
                    GUILayout.EndHorizontal();
                    GUILayout.Space(4f);
                }
                GUILayout.Space(10f);
            }
            GUILayout.EndScrollView();
        }

        private void DrawShortcutGroupHeader(ScenarioAuthoringShortcutGroupViewModel group)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label((group.Title ?? string.Empty).ToUpperInvariant(), _sectionTitleStyle);
            if (group.IsActiveContext)
            {
                GUILayout.Space(6f);
                GUILayout.Label("ACTIVE", _uiContext.Styles.PillSuccess, GUILayout.Width(74f));
            }
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);
        }
    }
}
