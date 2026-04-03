using System;
using Cortex;
using Cortex.Plugins.Abstractions;
using UnityEngine;

namespace Cortex.Shell.Unity.Imgui.Ui
{
    public sealed class ImguiWorkbenchUiSurface : IWorkbenchUiSurface
    {
        private const float DefaultPropertyLabelWidth = 280f;

        public string DrawSearchToolbar(string label, string draftQuery, float height, bool expandWidth)
        {
            var nextQuery = draftQuery ?? string.Empty;
            ImguiWorkbenchLayout.DrawGroup(null, delegate
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(label ?? "Search", GUILayout.Width(130f), GUILayout.Height(22f));
                nextQuery = GUILayout.TextField(nextQuery, GUILayout.Height(24f), GUILayout.ExpandWidth(expandWidth));
                if (GUILayout.Button("Clear", GUILayout.Width(64f), GUILayout.Height(24f)))
                {
                    nextQuery = string.Empty;
                }

                GUILayout.EndHorizontal();
            }, GUILayout.Height(height), GUILayout.ExpandWidth(expandWidth));
            return nextQuery;
        }

        public bool DrawNavigationGroupHeader(string title, bool isActive, bool isExpanded)
        {
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUI.backgroundColor = isActive
                ? ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.3f)
                : ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.72f);
            GUI.contentColor = isActive ? Color.white : ImguiWorkbenchLayout.GetTextColor();
            var clicked = GUILayout.Button((isExpanded ? "v " : "> ") + (title ?? "General"), GUILayout.Height(26f), GUILayout.ExpandWidth(true));
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            return clicked;
        }

        public bool DrawNavigationItem(string title, bool isSelected, float indent)
        {
            var previousBackground = GUI.backgroundColor;
            var previousContent = GUI.contentColor;
            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            GUI.backgroundColor = isSelected
                ? ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.16f)
                : ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.82f);
            GUI.contentColor = isSelected ? Color.white : ImguiWorkbenchLayout.GetTextColor();
            var clicked = GUILayout.Button(title ?? string.Empty, GUILayout.Height(24f), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            return clicked;
        }

        public void DrawCollapsedNavigationItem(string title, float indent)
        {
            GUILayout.Space(2f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            GUILayout.Label(title ?? string.Empty, CreateCollapsedNavigationStyle(), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            DrawHorizontalRule(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetBorderColor(), ImguiWorkbenchLayout.GetTextColor(), 0.3f), 1f);
            GUILayout.Space(4f);
        }

        public void DrawSectionHeader(string title, string description)
        {
            GUILayout.Label(title ?? string.Empty, CreateSectionHeaderStyle(), GUILayout.Height(40f));
            DrawHorizontalRule(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetBorderColor(), ImguiWorkbenchLayout.GetTextColor(), 0.34f), 1f);
            if (!string.IsNullOrEmpty(description))
            {
                GUILayout.Space(4f);
                GUILayout.Label(description);
            }

            GUILayout.Space(10f);
        }

        public void DrawSectionPanel(string title, Action drawBody)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(title ?? string.Empty, GUILayout.Height(24f));
            GUILayout.BeginHorizontal();
            DrawAccentBar(2f, 56f, ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetBorderColor(), 0.4f));
            GUILayout.Space(14f);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            if (drawBody != null)
            {
                drawBody();
            }

            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
            GUILayout.Space(10f);
        }

        public void DrawPopupMenuPanel(float width, Action drawBody)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.Width(width > 0f ? width : 220f));
            DrawHorizontalRule(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetBorderColor(), 0.32f), 2f);
            GUILayout.Space(4f);
            if (drawBody != null)
            {
                drawBody();
            }

            GUILayout.EndVertical();
        }

        public void BeginPropertyRow()
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            DrawAccentBar(2f, 44f, ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetBorderColor(), 0.45f));
            GUILayout.Space(14f);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        }

        public void EndPropertyRow()
        {
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }

        public void DrawPropertyLabelColumn(string title, string description)
        {
            GUILayout.BeginVertical(GUILayout.Width(DefaultPropertyLabelWidth));
            GUILayout.Label(title ?? string.Empty, GUILayout.Height(20f));
            if (!string.IsNullOrEmpty(description))
            {
                GUILayout.Label(description);
            }

            GUILayout.EndVertical();
        }

        private static GUIStyle CreateCollapsedNavigationStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = Mathf.Max(style.fontSize, 15);
            style.normal.textColor = Color.white;
            return style;
        }

        private static GUIStyle CreateSectionHeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = Mathf.Max(style.fontSize, 23);
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static void DrawAccentBar(float width, float minHeight, Color color)
        {
            var rect = GUILayoutUtility.GetRect(width, width, minHeight, minHeight, GUILayout.Width(width), GUILayout.MinHeight(minHeight), GUILayout.ExpandHeight(true));
            DrawSolidRect(rect, color);
        }

        private static void DrawHorizontalRule(Color color, float height)
        {
            var rect = GUILayoutUtility.GetRect(0f, 0f, height, height, GUILayout.ExpandWidth(true), GUILayout.Height(height));
            DrawSolidRect(rect, color);
        }

        private static void DrawSolidRect(Rect rect, Color color)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint)
            {
                return;
            }

            var previousColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture, ScaleMode.StretchToFill);
            GUI.color = previousColor;
        }
    }
}
