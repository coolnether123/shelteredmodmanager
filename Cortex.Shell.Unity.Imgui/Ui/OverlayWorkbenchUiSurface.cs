using System;
using Cortex;
using Cortex.Plugins.Abstractions;
using Cortex.Shell.Unity.Imgui;
using UnityEngine;

namespace Cortex.Shell.Unity.Overlay.Ui
{
    public sealed class OverlayWorkbenchUiSurface : IWorkbenchUiSurface
    {
        private const float PropertyLabelWidth = 240f;

        public string DrawSearchToolbar(string label, string draftQuery, float height, bool expandWidth)
        {
            var nextQuery = draftQuery ?? string.Empty;
            ImguiWorkbenchLayout.DrawGroup(null, delegate
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label((label ?? "Search").ToUpperInvariant(), CreateHeaderLabelStyle(), GUILayout.Width(108f), GUILayout.Height(22f));
                nextQuery = GUILayout.TextField(nextQuery, CreateSearchFieldStyle(), GUILayout.Height(24f), GUILayout.ExpandWidth(expandWidth));
                if (GUILayout.Button("Reset", CreateSearchButtonStyle(), GUILayout.Width(64f), GUILayout.Height(24f)))
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
                ? ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), Color.black, 0.22f)
                : ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), Color.black, 0.15f);
            GUI.contentColor = isActive ? Color.white : ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetTextColor(), Color.white, 0.15f);
            var clicked = GUILayout.Button((isExpanded ? "[-] " : "[+] ") + (title ?? "Group"), CreateNavigationHeaderStyle(), GUILayout.Height(28f), GUILayout.ExpandWidth(true));
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
                ? ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), Color.black, 0.08f)
                : ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetHeaderColor(), Color.black, 0.2f);
            GUI.contentColor = isSelected ? Color.white : ImguiWorkbenchLayout.GetTextColor();
            var clicked = GUILayout.Button(title ?? string.Empty, CreateNavigationItemStyle(isSelected), GUILayout.Height(25f), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            GUI.backgroundColor = previousBackground;
            GUI.contentColor = previousContent;
            return clicked;
        }

        public void DrawCollapsedNavigationItem(string title, float indent)
        {
            GUILayout.Space(1f);
            GUILayout.BeginHorizontal();
            GUILayout.Space(indent);
            GUILayout.Label(title ?? string.Empty, CreateCollapsedLabelStyle(), GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();
            DrawHorizontalRule(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetBorderColor(), 0.5f), 1f);
            GUILayout.Space(3f);
        }

        public void DrawSectionHeader(string title, string description)
        {
            GUILayout.BeginVertical(CreateHeaderBoxStyle(), GUILayout.ExpandWidth(true));
            GUILayout.Label(title ?? string.Empty, CreateSectionTitleStyle(), GUILayout.Height(28f));
            if (!string.IsNullOrEmpty(description))
            {
                GUILayout.Label(description, CreateSectionDescriptionStyle());
            }

            GUILayout.EndVertical();
            GUILayout.Space(8f);
        }

        public void DrawSectionPanel(string title, Action drawBody)
        {
            GUILayout.BeginVertical(CreatePanelStyle(), GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal();
            DrawAccentBar(4f, 56f, ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), Color.black, 0.12f));
            GUILayout.Space(10f);
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(title ?? string.Empty, CreatePanelTitleStyle(), GUILayout.Height(22f));
            GUILayout.Space(4f);
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
            GUILayout.BeginVertical(CreatePanelStyle(), GUILayout.Width(width > 0f ? width : 220f));
            if (drawBody != null)
            {
                drawBody();
            }

            GUILayout.EndVertical();
        }

        public void BeginPropertyRow()
        {
            GUILayout.BeginVertical(CreatePropertyRowStyle(), GUILayout.ExpandWidth(true));
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            DrawAccentBar(3f, 42f, ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), Color.black, 0.15f));
            GUILayout.Space(10f);
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
            GUILayout.BeginVertical(GUILayout.Width(PropertyLabelWidth));
            GUILayout.Label(title ?? string.Empty, CreatePropertyTitleStyle(), GUILayout.Height(18f));
            if (!string.IsNullOrEmpty(description))
            {
                GUILayout.Label(description, CreatePropertyDescriptionStyle());
            }

            GUILayout.EndVertical();
        }

        private static GUIStyle CreateHeaderLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.alignment = TextAnchor.MiddleLeft;
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateSearchFieldStyle()
        {
            var style = new GUIStyle(GUI.skin.textField);
            style.margin = new RectOffset(0, 0, 1, 1);
            return style;
        }

        private static GUIStyle CreateSearchButtonStyle()
        {
            var style = new GUIStyle(GUI.skin.button);
            style.fontStyle = FontStyle.Bold;
            return style;
        }

        private static GUIStyle CreateNavigationHeaderStyle()
        {
            var style = new GUIStyle(GUI.skin.button);
            style.alignment = TextAnchor.MiddleLeft;
            style.fontStyle = FontStyle.Bold;
            style.padding = new RectOffset(10, 10, 4, 4);
            return style;
        }

        private static GUIStyle CreateNavigationItemStyle(bool isSelected)
        {
            var style = new GUIStyle(GUI.skin.button);
            style.alignment = TextAnchor.MiddleLeft;
            style.padding = new RectOffset(12, 10, 4, 4);
            if (isSelected)
            {
                style.fontStyle = FontStyle.Bold;
            }

            return style;
        }

        private static GUIStyle CreateCollapsedLabelStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetTextColor(), Color.white, 0.18f);
            return style;
        }

        private static GUIStyle CreateHeaderBoxStyle()
        {
            var style = new GUIStyle(GUI.skin.box);
            style.padding = new RectOffset(10, 10, 10, 10);
            return style;
        }

        private static GUIStyle CreateSectionTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = Mathf.Max(18, style.fontSize + 4);
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreateSectionDescriptionStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.normal.textColor = ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetTextColor(), Color.white, 0.3f);
            return style;
        }

        private static GUIStyle CreatePanelStyle()
        {
            var style = new GUIStyle(GUI.skin.box);
            style.padding = new RectOffset(8, 8, 8, 8);
            style.margin = new RectOffset(0, 0, 0, 0);
            return style;
        }

        private static GUIStyle CreatePanelTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreatePropertyRowStyle()
        {
            var style = new GUIStyle(GUI.skin.box);
            style.padding = new RectOffset(8, 8, 8, 8);
            style.margin = new RectOffset(0, 0, 0, 6);
            return style;
        }

        private static GUIStyle CreatePropertyTitleStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.normal.textColor = ImguiWorkbenchLayout.GetTextColor();
            return style;
        }

        private static GUIStyle CreatePropertyDescriptionStyle()
        {
            var style = new GUIStyle(GUI.skin.label);
            style.wordWrap = true;
            style.normal.textColor = ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetTextColor(), Color.white, 0.35f);
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
