using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;

namespace Cortex.Renderers.Overlay
{
    public sealed class UnityOverlayWorkbenchRenderer : IWorkbenchRenderer
    {
        private readonly RendererCapabilitySet _capabilities;

        public UnityOverlayWorkbenchRenderer()
        {
            _capabilities = new RendererCapabilitySet();
            _capabilities.SupportsRetainedLayout = false;
            _capabilities.SupportsClipping = true;
            _capabilities.SupportsLayeredOverlays = true;
            _capabilities.SupportsTextMeasurementCache = false;
            _capabilities.SupportsIme = false;
            _capabilities.SupportsVirtualization = false;
            _capabilities.SupportsDragPreview = true;
            _capabilities.SupportsCursorControl = false;
            _capabilities.SupportsRichText = false;
            _capabilities.SupportsPointerCapture = true;
            _capabilities.SupportsKeyboardRouting = true;
            _capabilities.SupportsScrollableSurfaces = true;
            _capabilities.SupportsCaretRendering = true;
            _capabilities.SupportsDenseCollections = true;
        }

        public string RendererId
        {
            get { return "cortex.renderer.unity-overlay"; }
        }

        public string DisplayName
        {
            get { return "Unity Overlay Renderer"; }
        }

        public RendererCapabilitySet Capabilities
        {
            get { return _capabilities; }
        }
    }
}
