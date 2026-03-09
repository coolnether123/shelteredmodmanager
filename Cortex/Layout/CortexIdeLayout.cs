using System;
using Cortex.Core.Models;
using Cortex.Presentation.Models;
using UnityEngine;

namespace Cortex
{
    internal static class CortexIdeLayout
    {
        private static GUIStyle _groupStyle;
        private static GUIStyle _headerStyle;
        private static Texture2D _groupBackground;
        private static Texture2D _headerBackground;
        private static string _appliedThemeId = string.Empty;
        private static readonly ThemeTokenSet _themeTokens = new ThemeTokenSet();

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
            var accent = GetAccentColor();
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PrimarySideHost:
                    return accent;
                case WorkbenchHostLocation.DocumentHost:
                    return Blend(accent, new Color(0.38f, 0.96f, 0.7f, 1f), 0.5f);
                case WorkbenchHostLocation.PanelHost:
                    return GetWarningColor();
                case WorkbenchHostLocation.SecondarySideHost:
                    return Blend(accent, GetMutedTextColor(), 0.35f);
                case WorkbenchHostLocation.CommandSurface:
                    return GetErrorColor();
                default:
                    return GetMutedTextColor();
            }
        }

        public static Color GetBackgroundColor()
        {
            return ParseColor(_themeTokens.BackgroundColor, new Color(0.05f, 0.05f, 0.07f, 0.97f));
        }

        public static Color GetSurfaceColor()
        {
            return ParseColor(_themeTokens.SurfaceColor, new Color(0.08f, 0.08f, 0.1f, 0.94f));
        }

        public static Color GetHeaderColor()
        {
            return ParseColor(_themeTokens.HeaderColor, new Color(0.16f, 0.17f, 0.2f, 0.98f));
        }

        public static Color GetBorderColor()
        {
            return ParseColor(_themeTokens.BorderColor, new Color(0.19f, 0.21f, 0.27f, 1f));
        }

        public static Color GetAccentColor()
        {
            return ParseColor(_themeTokens.AccentColor, new Color(0.35f, 0.74f, 1f, 1f));
        }

        public static Color GetTextColor()
        {
            return ParseColor(_themeTokens.TextColor, new Color(0.95f, 0.95f, 0.95f, 1f));
        }

        public static Color GetMutedTextColor()
        {
            return ParseColor(_themeTokens.MutedTextColor, new Color(0.78f, 0.82f, 0.9f, 1f));
        }

        public static Color GetWarningColor()
        {
            return ParseColor(_themeTokens.WarningColor, new Color(1f, 0.77f, 0.34f, 1f));
        }

        public static Color GetErrorColor()
        {
            return ParseColor(_themeTokens.ErrorColor, new Color(1f, 0.56f, 0.56f, 1f));
        }

        public static Color GetActivityButtonColor(bool isSelected)
        {
            return isSelected
                ? Blend(GetHeaderColor(), GetAccentColor(), 0.55f)
                : Blend(GetSurfaceColor(), GetHeaderColor(), 0.6f);
        }

        public static Color GetInteractiveFillColor(bool isSelected, WorkbenchHostLocation hostLocation)
        {
            return isSelected
                ? Blend(GetHeaderColor(), GetHostAccentColor(hostLocation), 0.5f)
                : Blend(GetSurfaceColor(), GetHeaderColor(), 0.72f);
        }

        public static Color GetInteractiveTextColor(bool isSelected)
        {
            return isSelected ? Color.white : GetMutedTextColor();
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        public static Color Blend(Color from, Color to, float amount)
        {
            return Color.Lerp(from, to, Mathf.Clamp01(amount));
        }

        public static void ApplyTheme(ThemeTokenSet tokens, string themeId)
        {
            var effectiveThemeId = string.IsNullOrEmpty(themeId) ? "cortex.default" : themeId;
            if (tokens == null || string.Equals(_appliedThemeId, effectiveThemeId, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _themeTokens.BackgroundColor = tokens.BackgroundColor;
            _themeTokens.SurfaceColor = tokens.SurfaceColor;
            _themeTokens.HeaderColor = tokens.HeaderColor;
            _themeTokens.BorderColor = tokens.BorderColor;
            _themeTokens.AccentColor = tokens.AccentColor;
            _themeTokens.TextColor = tokens.TextColor;
            _themeTokens.MutedTextColor = tokens.MutedTextColor;
            _themeTokens.WarningColor = tokens.WarningColor;
            _themeTokens.ErrorColor = tokens.ErrorColor;
            _themeTokens.FontRole = tokens.FontRole;
            _groupStyle = null;
            _headerStyle = null;
            _groupBackground = null;
            _headerBackground = null;
            _appliedThemeId = effectiveThemeId;
        }

        public static Color ParseColor(string hex, Color fallback)
        {
            if (string.IsNullOrEmpty(hex))
            {
                return fallback;
            }

            Color parsed;
            return ColorUtility.TryParseHtmlString(hex, out parsed) ? parsed : fallback;
        }

        private static void EnsureStyles()
        {
            if (_groupStyle == null)
            {
                _groupBackground = MakeTex(GetSurfaceColor());
                _groupStyle = new GUIStyle(GUI.skin.box);
                GuiStyleUtil.ApplyBackgroundToAllStates(_groupStyle, _groupBackground);
                _groupStyle.padding = new RectOffset(10, 10, 10, 10);
                _groupStyle.margin = new RectOffset(4, 4, 4, 4);
            }

            if (_headerStyle == null)
            {
                _headerBackground = MakeTex(GetHeaderColor());
                _headerStyle = new GUIStyle(GUI.skin.label);
                GuiStyleUtil.ApplyBackgroundToAllStates(_headerStyle, _headerBackground);
                GuiStyleUtil.ApplyTextColorToAllStates(_headerStyle, GetTextColor());
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
