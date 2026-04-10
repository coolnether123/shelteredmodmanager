using System;
using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.DearImgui
{
    public sealed class DearImguiWorkbenchRuntimeUi : IWorkbenchRuntimeUi, IDisposable
    {
        private readonly IWorkbenchFrameContext _frameContext;
        private readonly IRenderPipeline _renderPipeline = new DearImguiRenderPipeline();
        private readonly IWorkbenchUiSurface _workbenchUiSurface = new DearImguiWorkbenchUiSurface();

        public DearImguiWorkbenchRuntimeUi(IWorkbenchFrameContext frameContext)
        {
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
        }

        public IRenderPipeline RenderPipeline { get { return _renderPipeline; } }
        public IWorkbenchUiSurface WorkbenchUiSurface { get { return _workbenchUiSurface; } }
        public IWorkbenchFrameContext FrameContext { get { return _frameContext; } }
        public WorkbenchRuntimeUiLayoutMode LayoutMode { get { return WorkbenchRuntimeUiLayoutMode.IntegratedShellWindow; } }
        public void Dispose() { }
    }
}
