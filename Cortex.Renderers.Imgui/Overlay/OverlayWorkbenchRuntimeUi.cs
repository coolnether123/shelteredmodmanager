using System;
using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.Overlay
{
    public sealed class OverlayWorkbenchRuntimeUi : IWorkbenchRuntimeUi, IDisposable
    {
        private readonly IRenderPipeline _renderPipeline;
        private readonly IWorkbenchUiSurface _workbenchUiSurface;
        private readonly IWorkbenchFrameContext _frameContext;

        public OverlayWorkbenchRuntimeUi(IWorkbenchUiSurface workbenchUiSurface, IWorkbenchFrameContext frameContext)
        {
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
            _renderPipeline = new UnityOverlayRenderPipeline(_frameContext);
            _workbenchUiSurface = workbenchUiSurface ?? NullWorkbenchUiSurface.Instance;
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
            get { return WorkbenchRuntimeUiLayoutMode.OverlayWindows; }
        }

        public void Dispose()
        {
            var disposable = _renderPipeline as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }
        }
    }
}
