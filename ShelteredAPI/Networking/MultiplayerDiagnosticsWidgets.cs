using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal static class MultiplayerDiagnosticsWidgets
    {
        private const float LabelWidth = 96f;

        public static void BeginSection()
        {
            GUILayout.BeginVertical(GUI.skin.box);
        }

        public static void EndSection()
        {
            GUILayout.EndVertical();
        }

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
            GUILayout.Label(text ?? string.Empty, BuildHeaderStyle());
        }

        public static void DrawSubHeader(string text)
        {
            GUILayout.Space(6f);
            GUILayout.Label(text ?? string.Empty, BuildSubHeaderStyle());
        }

        public static void DrawMiniMetric(string label, string value)
        {
            GUILayout.BeginVertical(GUILayout.MinWidth(90f));
            GUILayout.Label(label ?? string.Empty, BuildMutedStyle());
            GUILayout.Label(value ?? string.Empty, BuildWrappedStyle());
            GUILayout.EndVertical();
        }

        public static void DrawValue(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label((label ?? string.Empty) + ":", GUILayout.Width(LabelWidth));
            GUILayout.Label(value ?? string.Empty, BuildWrappedStyle());
            GUILayout.EndHorizontal();
        }

        public static void DrawOptionalError(string label, string value)
        {
            if (!string.IsNullOrEmpty(value))
                DrawWarning(label + ": " + value);
        }

        public static void DrawWarning(string text)
        {
            GUILayout.Label(text ?? string.Empty, BuildWarningStyle());
        }

        public static void DrawHint(string text)
        {
            GUILayout.Label(text ?? string.Empty, BuildWrappedStyle());
        }

        private static GUIStyle BuildHeaderStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            if (GUI.skin.label.fontSize > 0)
                style.fontSize = GUI.skin.label.fontSize + 2;
            style.wordWrap = true;
            return style;
        }

        private static GUIStyle BuildSubHeaderStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.wordWrap = true;
            return style;
        }

        private static GUIStyle BuildMutedStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            return style;
        }

        private static GUIStyle BuildWarningStyle()
        {
            GUIStyle style = BuildWrappedStyle();
            style.fontStyle = FontStyle.Bold;
            return style;
        }

        private static GUIStyle BuildWrappedStyle()
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            return style;
        }
    }
}
