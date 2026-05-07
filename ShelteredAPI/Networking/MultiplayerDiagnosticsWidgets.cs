using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal static class MultiplayerDiagnosticsWidgets
    {
        private const float LabelWidth = 96f;

        public static bool DrawStateButton(string text, bool active, string tooltip, params GUILayoutOption[] options)
        {
            GUIStyle activeStyle = GUI.skin.FindStyle("button_on") ?? GUI.skin.button;
            GUIStyle inactiveStyle = GUI.skin.button;
            GUIStyle style = active ? activeStyle : inactiveStyle;
            return GUILayout.Button(new GUIContent(text ?? string.Empty, tooltip ?? string.Empty), style, options);
        }

        public static void DrawSectionHeader(string text)
        {
            GUILayout.Space(10f);
            GUILayout.Label(text ?? string.Empty);
        }

        public static void DrawMiniMetric(string label, string value)
        {
            GUILayout.BeginVertical(GUILayout.MinWidth(90f));
            GUILayout.Label(label ?? string.Empty);
            GUILayout.Label(value ?? string.Empty);
            GUILayout.EndVertical();
        }

        public static void DrawValue(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label((label ?? string.Empty) + ":", GUILayout.Width(LabelWidth));
            GUILayout.Label(value ?? string.Empty);
            GUILayout.EndHorizontal();
        }

        public static void DrawOptionalError(string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
                DrawWarning(label + ": " + value);
        }

        public static void DrawWarning(string text)
        {
            GUILayout.Label(text ?? string.Empty);
        }

        public static void DrawHint(string text)
        {
            GUILayout.Label(text ?? string.Empty);
        }
    }
}
