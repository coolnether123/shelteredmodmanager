using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;

namespace Cortex.Renderers.DearImgui
{
    public sealed class DearImguiWorkbenchRenderer : IWorkbenchRenderer
    {
        public const string RendererIdValue = "cortex.renderer.dearimgui";
        private readonly RendererCapabilitySet _capabilities;

        public DearImguiWorkbenchRenderer()
        {
            _capabilities = new RendererCapabilitySet();
            _capabilities.SupportsRetainedLayout = false;
            _capabilities.SupportsClipping = true;
            _capabilities.SupportsLayeredOverlays = true;
            _capabilities.SupportsTextMeasurementCache = false;
            _capabilities.SupportsIme = false;
            _capabilities.SupportsVirtualization = false;
            _capabilities.SupportsDragPreview = false;
            _capabilities.SupportsCursorControl = false;
            _capabilities.SupportsRichText = false;
            _capabilities.SupportsPointerCapture = true;
            _capabilities.SupportsKeyboardRouting = true;
            _capabilities.SupportsScrollableSurfaces = true;
            _capabilities.SupportsCaretRendering = true;
            _capabilities.SupportsDenseCollections = true;
        }

        public string RendererId { get { return RendererIdValue; } }
        public string DisplayName { get { return "Dear ImGui Renderer"; } }
        public RendererCapabilitySet Capabilities { get { return _capabilities; } }
    }
}
