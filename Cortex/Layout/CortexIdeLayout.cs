using System;
using UnityEngine;

namespace Cortex
{
    internal static class CortexIdeLayout
    {
        private static GUIStyle _groupStyle;
        private static GUIStyle _headerStyle;
        private static Texture2D _groupBackground;
        private static Texture2D _headerBackground;

        public static void DrawTwoPane(float primaryWidth, float minimumPrimaryWidth, Action drawPrimary, Action drawSecondary)
        {
            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical(GUILayout.Width(Mathf.Max(minimumPrimaryWidth, primaryWidth)));
            if (drawPrimary != null)
            {
                drawPrimary();
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (drawSecondary != null)
            {
                drawSecondary();
            }
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();
        }

        public static void DrawGroup(string title, Action drawBody, params GUILayoutOption[] options)
        {
            EnsureStyles();
            GUILayout.BeginVertical(_groupStyle, options);
            if (!string.IsNullOrEmpty(title))
            {
                GUILayout.Label(title, _headerStyle);
            }

            if (drawBody != null)
            {
                drawBody();
            }

            GUILayout.EndVertical();
        }

        private static void EnsureStyles()
        {
            if (_groupStyle == null)
            {
                _groupBackground = MakeTex(new Color(0.08f, 0.08f, 0.1f, 0.94f));
                _groupStyle = new GUIStyle(GUI.skin.box);
                _groupStyle.normal.background = _groupBackground;
                _groupStyle.padding = new RectOffset(10, 10, 10, 10);
                _groupStyle.margin = new RectOffset(4, 4, 4, 4);
            }

            if (_headerStyle == null)
            {
                _headerBackground = MakeTex(new Color(0.16f, 0.17f, 0.2f, 0.98f));
                _headerStyle = new GUIStyle(GUI.skin.label);
                _headerStyle.normal.background = _headerBackground;
                _headerStyle.normal.textColor = new Color(0.95f, 0.95f, 0.95f, 1f);
                _headerStyle.fontStyle = FontStyle.Bold;
                _headerStyle.padding = new RectOffset(8, 8, 4, 4);
                _headerStyle.margin = new RectOffset(0, 0, 0, 8);
            }
        }

        private static Texture2D MakeTex(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
