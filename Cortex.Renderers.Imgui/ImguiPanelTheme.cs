using UnityEngine;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiPanelTheme
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

    public sealed class PanelRenderResult
    {
        public Vector2 Scroll = Vector2.zero;
        public string ActivatedId = string.Empty;
    }
}
