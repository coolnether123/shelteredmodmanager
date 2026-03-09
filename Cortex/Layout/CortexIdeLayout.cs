using System;
using Cortex.Core.Models;
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

        public static string GetHostDisplayName(WorkbenchHostLocation hostLocation)
        {
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost:
                    return "Side";
                case WorkbenchHostLocation.DocumentHost:
                    return "Editors";
                case WorkbenchHostLocation.PanelHost:
                    return "Panels";
                case WorkbenchHostLocation.SecondarySideHost:
                    return "Aux";
                case WorkbenchHostLocation.ToolRail:
                    return "Rail";
                case WorkbenchHostLocation.StatusStrip:
                    return "Status";
                case WorkbenchHostLocation.CommandSurface:
                    return "Commands";
                default:
                    return "Host";
            }
        }

        public static Color GetHostAccentColor(WorkbenchHostLocation hostLocation)
        {
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost:
                    return new Color(0.35f, 0.74f, 1f, 1f);
                case WorkbenchHostLocation.DocumentHost:
                    return new Color(0.44f, 0.91f, 0.66f, 1f);
                case WorkbenchHostLocation.PanelHost:
                    return new Color(1f, 0.77f, 0.34f, 1f);
                case WorkbenchHostLocation.SecondarySideHost:
                    return new Color(0.83f, 0.63f, 1f, 1f);
                case WorkbenchHostLocation.CommandSurface:
                    return new Color(1f, 0.56f, 0.56f, 1f);
                default:
                    return new Color(0.78f, 0.82f, 0.9f, 1f);
            }
        }

        private static void EnsureStyles()
        {
            if (_groupStyle == null)
            {
                _groupBackground = MakeTex(new Color(0.08f, 0.08f, 0.1f, 0.94f));
                _groupStyle = new GUIStyle(GUI.skin.box);
                GuiStyleUtil.ApplyBackgroundToAllStates(_groupStyle, _groupBackground);
                _groupStyle.padding = new RectOffset(10, 10, 10, 10);
                _groupStyle.margin = new RectOffset(4, 4, 4, 4);
            }

            if (_headerStyle == null)
            {
                _headerBackground = MakeTex(new Color(0.16f, 0.17f, 0.2f, 0.98f));
                _headerStyle = new GUIStyle(GUI.skin.label);
                GuiStyleUtil.ApplyBackgroundToAllStates(_headerStyle, _headerBackground);
                GuiStyleUtil.ApplyTextColorToAllStates(_headerStyle, new Color(0.95f, 0.95f, 0.95f, 1f));
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
