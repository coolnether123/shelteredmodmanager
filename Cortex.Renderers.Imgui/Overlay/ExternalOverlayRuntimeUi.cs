using System;
using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.Imgui
{
    public sealed class ExternalOverlayRuntimeUi : IWorkbenchRuntimeUi, IDisposable
    {
        private readonly IWorkbenchUiSurface _workbenchUiSurface;
        private readonly IWorkbenchFrameContext _frameContext;
        private readonly IRenderPipeline _renderPipeline;

        public ExternalOverlayRuntimeUi(IWorkbenchUiSurface workbenchUiSurface, IWorkbenchFrameContext frameContext)
        {
            _workbenchUiSurface = workbenchUiSurface ?? NullWorkbenchUiSurface.Instance;
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
            _renderPipeline = new ExternalOverlayRenderPipeline(_frameContext);
        }

        public IRenderPipeline RenderPipeline
        {
            get { return _renderPipeline; }
        }

        public IWorkbenchUiSurface WorkbenchUiSurface
        {
            get { return _workbenchUiSurface; }
        }

        public IWorkbenchFrameContext FrameContext
        {
            get { return _frameContext; }
        }

        public WorkbenchRuntimeUiLayoutMode LayoutMode
        {
            get { return WorkbenchRuntimeUiLayoutMode.ExternalOverlayWindows; }
        }

        public void Dispose()
        {
            var disposable = _renderPipeline as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

        private sealed class ExternalOverlayRenderPipeline : IRenderPipeline, IDisposable
        {
            private readonly ImguiRenderPipeline _innerPipeline;
            private readonly IWorkbenchRenderer _renderer = new ExternalOverlayWorkbenchRenderer();

            public ExternalOverlayRenderPipeline(IWorkbenchFrameContext frameContext)
            {
                _innerPipeline = new ImguiRenderPipeline(frameContext ?? NullWorkbenchFrameContext.Instance);
            }

            public IWorkbenchRenderer WorkbenchRenderer
            {
                get { return _renderer; }
            }

            public IPanelRenderer PanelRenderer
            {
                get { return _innerPipeline.PanelRenderer; }
            }

            public IOverlayRendererFactory OverlayRendererFactory
            {
                get { return _innerPipeline.OverlayRendererFactory; }
            }

            public void Dispose()
            {
                _innerPipeline.Dispose();
            }
        }

        private sealed class ExternalOverlayWorkbenchRenderer : IWorkbenchRenderer
        {
            private static readonly RendererCapabilitySet CapabilitiesInstance = new RendererCapabilitySet();

            public string RendererId
            {
                get { return "cortex.renderer.external-overlay"; }
            }

            public string DisplayName
            {
                get { return "External Overlay Presenter"; }
            }

            public RendererCapabilitySet Capabilities
            {
                get { return CapabilitiesInstance; }
            }
        }
    }
}
