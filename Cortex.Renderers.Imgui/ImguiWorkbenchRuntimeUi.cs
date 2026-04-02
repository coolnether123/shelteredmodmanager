using System;
using Cortex.Plugins.Abstractions;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiWorkbenchRuntimeUi : IWorkbenchRuntimeUi, IDisposable
    {
        private readonly IRenderPipeline _renderPipeline;
        private readonly IWorkbenchUiSurface _workbenchUiSurface;
        private readonly IWorkbenchFrameContext _frameContext;

        public ImguiWorkbenchRuntimeUi(IWorkbenchUiSurface workbenchUiSurface, IWorkbenchFrameContext frameContext)
        {
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
            _renderPipeline = new ImguiRenderPipeline(_frameContext);
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
