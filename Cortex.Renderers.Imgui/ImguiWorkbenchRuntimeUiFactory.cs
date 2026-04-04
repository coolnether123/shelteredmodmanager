using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiWorkbenchRuntimeUiFactory : IWorkbenchRuntimeUiFactory
    {
        private readonly IWorkbenchUiSurface _workbenchUiSurface;
        private readonly IWorkbenchFrameContext _frameContext;
        private readonly WorkbenchRuntimeUiLayoutMode _layoutMode;

        public ImguiWorkbenchRuntimeUiFactory()
            : this(null, null, WorkbenchRuntimeUiLayoutMode.IntegratedShellWindow)
        {
        }

        public ImguiWorkbenchRuntimeUiFactory(IWorkbenchUiSurface workbenchUiSurface, IWorkbenchFrameContext frameContext)
            : this(workbenchUiSurface, frameContext, WorkbenchRuntimeUiLayoutMode.IntegratedShellWindow)
        {
        }

        public ImguiWorkbenchRuntimeUiFactory(IWorkbenchUiSurface workbenchUiSurface, IWorkbenchFrameContext frameContext, WorkbenchRuntimeUiLayoutMode layoutMode)
        {
            _workbenchUiSurface = workbenchUiSurface ?? NullWorkbenchUiSurface.Instance;
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
            _layoutMode = layoutMode;
        }

        public IWorkbenchRuntimeUi Create()
        {
            return new ImguiWorkbenchRuntimeUi(_workbenchUiSurface, _frameContext, _layoutMode);
        }
    }
}
