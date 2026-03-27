using Cortex.Rendering.Abstractions;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiRenderPipeline : IRenderPipeline
    {
        private readonly IWorkbenchRenderer _workbenchRenderer;
        private readonly IPanelRenderer _panelRenderer;
        private readonly IOverlayRendererFactory _overlayRendererFactory;

        public ImguiRenderPipeline()
        {
            _workbenchRenderer = new ImguiWorkbenchRenderer();
            _panelRenderer = new ImguiPanelRenderer();
            _overlayRendererFactory = new ImguiOverlayRendererFactory();
        }

        public IWorkbenchRenderer WorkbenchRenderer
        {
            get { return _workbenchRenderer; }
        }

        public IPanelRenderer PanelRenderer
        {
            get { return _panelRenderer; }
        }

        public IOverlayRendererFactory OverlayRendererFactory
        {
            get { return _overlayRendererFactory; }
        }
    }
}
