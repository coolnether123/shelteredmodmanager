using UnityEngine;

namespace ShelteredAPI.Networking
{
    internal static class MultiplayerDiagnosticsWidgets
    {
        private const float LabelWidth = 96f;
        private static GUISkin _cachedSkin;
        private static int _cachedLabelFontSize;
        private static GUIStyle _headerStyle;
        private static GUIStyle _subHeaderStyle;
        private static GUIStyle _mutedStyle;
        private static GUIStyle _warningStyle;
        private static GUIStyle _wrappedStyle;

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
            try
            {
                GUILayout.Label(label ?? string.Empty, BuildMutedStyle());
                GUILayout.Label(value ?? string.Empty, BuildWrappedStyle());
            }
            finally
            {
                GUILayout.EndVertical();
            }
        }

        public static void DrawValue(string label, string value)
        {
            GUILayout.BeginHorizontal();
            try
            {
                GUILayout.Label((label ?? string.Empty) + ":", GUILayout.Width(LabelWidth));
                GUILayout.Label(value ?? string.Empty, BuildWrappedStyle());
            }
            finally
            {
                GUILayout.EndHorizontal();
            }
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
            EnsureStyles();
            return _headerStyle;
        }

        private static GUIStyle BuildSubHeaderStyle()
        {
            EnsureStyles();
            return _subHeaderStyle;
        }

        private static GUIStyle BuildMutedStyle()
        {
            EnsureStyles();
            return _mutedStyle;
        }

        private static GUIStyle BuildWarningStyle()
        {
            EnsureStyles();
            return _warningStyle;
        }

        private static GUIStyle BuildWrappedStyle()
        {
            EnsureStyles();
            return _wrappedStyle;
        }

        private static void EnsureStyles()
        {
            GUISkin skin = GUI.skin;
            int labelFontSize = skin != null && skin.label != null ? skin.label.fontSize : 0;
            if (_cachedSkin == skin
                && _cachedLabelFontSize == labelFontSize
                && _headerStyle != null)
            {
                return;
            }

            GUIStyle label = skin != null && skin.label != null ? skin.label : GUIStyle.none;
            _cachedSkin = skin;
            _cachedLabelFontSize = labelFontSize;

            _wrappedStyle = new GUIStyle(label);
            _wrappedStyle.wordWrap = true;

            _mutedStyle = new GUIStyle(label);
            _mutedStyle.wordWrap = true;

            _subHeaderStyle = new GUIStyle(label);
            _subHeaderStyle.fontStyle = FontStyle.Bold;
            _subHeaderStyle.wordWrap = true;

            _headerStyle = new GUIStyle(label);
            _headerStyle.fontStyle = FontStyle.Bold;
            if (labelFontSize > 0)
                _headerStyle.fontSize = labelFontSize + 2;
            _headerStyle.wordWrap = true;

            _warningStyle = new GUIStyle(_wrappedStyle);
            _warningStyle.fontStyle = FontStyle.Bold;
        }
    }
}
