using System;
using Cortex.Rendering;
using Cortex.Rendering.Abstractions;
using Cortex.Renderers.Imgui;

namespace Cortex.Renderers.Overlay
{
    public sealed class UnityOverlayRenderPipeline : IRenderPipeline, IDisposable
    {
        private readonly ImguiModuleResources _moduleResources;
        private readonly IWorkbenchRenderer _workbenchRenderer;
        private readonly IPanelRenderer _panelRenderer;
        private readonly IOverlayRendererFactory _overlayRendererFactory;

        public UnityOverlayRenderPipeline()
            : this(null)
        {
        }

        public UnityOverlayRenderPipeline(IWorkbenchFrameContext frameContext)
        {
            _moduleResources = new ImguiModuleResources();
            _workbenchRenderer = new UnityOverlayWorkbenchRenderer();
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
