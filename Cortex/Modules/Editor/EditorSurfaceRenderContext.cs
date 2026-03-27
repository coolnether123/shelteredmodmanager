using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;

namespace Cortex.Modules.Editor
{
    internal sealed class EditorSurfaceRenderContext
    {
        public string ThemeKey { get; set; }
        public UnityEngine.GUIStyle CodeStyle { get; set; }
        public UnityEngine.GUIStyle GutterStyle { get; set; }
        public IPanelRenderer PanelRenderer { get; set; }
        public RenderRect BlockedRect { get; set; }
        public float GutterWidth { get; set; }
        public PopupMenuThemePalette PopupMenuTheme { get; set; }
        public HoverTooltipThemePalette HoverTooltipTheme { get; set; }
    }
}
