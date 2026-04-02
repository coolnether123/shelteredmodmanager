using System;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiRenderPipeline : IRenderPipeline, IDisposable
    {
        private readonly ImguiModuleResources _moduleResources;
        private readonly IWorkbenchRenderer _workbenchRenderer;
        private readonly IPanelRenderer _panelRenderer;
        private readonly IOverlayRendererFactory _overlayRendererFactory;

        public ImguiRenderPipeline()
            : this(null)
        {
        }

        public ImguiRenderPipeline(IWorkbenchFrameContext frameContext)
        {
            _moduleResources = new ImguiModuleResources();
            _workbenchRenderer = new ImguiWorkbenchRenderer();
            _panelRenderer = new ImguiPanelRenderer(_moduleResources);
            _overlayRendererFactory = new ImguiOverlayRendererFactory(_moduleResources, frameContext);
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

        public void Dispose()
        {
            _moduleResources.Dispose();
        }
    }
}
