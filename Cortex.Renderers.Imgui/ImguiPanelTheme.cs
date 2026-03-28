using Cortex.Rendering.Models;
using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    internal sealed class ImguiPanelTheme
    {
        public Color BackgroundColor;
        public Color HeaderColor;
        public Color BorderColor;
        public Color DividerColor;
        public Color ActionFillColor;
        public Color ActionActiveFillColor;
        public Color CardFillColor;
        public Color TextColor;
        public Color MutedTextColor;
        public Color AccentColor;
        public Color WarningColor;
    }

    internal static class ImguiPanelThemeFactory
    {
        public static ImguiPanelTheme Create(PanelThemePalette theme)
        {
            var resolvedTheme = theme ?? new PanelThemePalette();
            return new ImguiPanelTheme
            {
                BackgroundColor = ImguiThemeUtility.ToColor(resolvedTheme.BackgroundColor),
                HeaderColor = ImguiThemeUtility.ToColor(resolvedTheme.HeaderColor),
                BorderColor = ImguiThemeUtility.ToColor(resolvedTheme.BorderColor),
                DividerColor = ImguiThemeUtility.ToColor(resolvedTheme.DividerColor),
                ActionFillColor = ImguiThemeUtility.ToColor(resolvedTheme.ActionFillColor),
                ActionActiveFillColor = ImguiThemeUtility.ToColor(resolvedTheme.ActionActiveFillColor),
                CardFillColor = ImguiThemeUtility.ToColor(resolvedTheme.CardFillColor),
                TextColor = ImguiThemeUtility.ToColor(resolvedTheme.TextColor),
                MutedTextColor = ImguiThemeUtility.ToColor(resolvedTheme.MutedTextColor),
                AccentColor = ImguiThemeUtility.ToColor(resolvedTheme.AccentColor),
                WarningColor = ImguiThemeUtility.ToColor(resolvedTheme.WarningColor)
            };
        }
    }
}
