using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiWorkbenchRenderer : IWorkbenchRenderer
    {
        private readonly RendererCapabilitySet _capabilities;

        public ImguiWorkbenchRenderer()
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
            _capabilities.SupportsPointerCapture = false;
            _capabilities.SupportsKeyboardRouting = true;
            _capabilities.SupportsScrollableSurfaces = true;
            _capabilities.SupportsCaretRendering = true;
            _capabilities.SupportsDenseCollections = true;
        }

        public string RendererId
        {
            get { return "cortex.renderer.imgui"; }
        }

        public string DisplayName
        {
            get { return "IMGUI Compatibility Renderer"; }
        }

        public RendererCapabilitySet Capabilities
        {
            get { return _capabilities; }
        }
    }
}
