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
                BackgroundColor = ToColor(resolvedTheme.BackgroundColor),
                HeaderColor = ToColor(resolvedTheme.HeaderColor),
                BorderColor = ToColor(resolvedTheme.BorderColor),
                DividerColor = ToColor(resolvedTheme.DividerColor),
                ActionFillColor = ToColor(resolvedTheme.ActionFillColor),
                ActionActiveFillColor = ToColor(resolvedTheme.ActionActiveFillColor),
                CardFillColor = ToColor(resolvedTheme.CardFillColor),
                TextColor = ToColor(resolvedTheme.TextColor),
                MutedTextColor = ToColor(resolvedTheme.MutedTextColor),
                AccentColor = ToColor(resolvedTheme.AccentColor),
                WarningColor = ToColor(resolvedTheme.WarningColor)
            };
        }

        private static Color ToColor(RenderColor color)
        {
            return new Color(color.R, color.G, color.B, color.A);
        }
    }
}
