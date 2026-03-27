using Cortex.Rendering.Abstractions;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiOverlayRendererFactory : IOverlayRendererFactory
    {
        public IHoverTooltipRenderer CreateHoverTooltipRenderer()
        {
            return new ImguiHoverTooltipRenderer();
        }

        public IPopupMenuRenderer CreatePopupMenuRenderer()
        {
            return new ImguiPopupMenuRenderer();
        }
    }
}
