using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiWorkbenchRuntimeUiFactory : IWorkbenchRuntimeUiFactory
    {
        private readonly IWorkbenchUiSurface _workbenchUiSurface;
        private readonly IWorkbenchFrameContext _frameContext;

        public ImguiWorkbenchRuntimeUiFactory()
            : this(null, null)
        {
        }

        public ImguiWorkbenchRuntimeUiFactory(IWorkbenchUiSurface workbenchUiSurface, IWorkbenchFrameContext frameContext)
        {
            _workbenchUiSurface = workbenchUiSurface ?? NullWorkbenchUiSurface.Instance;
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
        }

        public IWorkbenchRuntimeUi Create()
        {
            return new ImguiWorkbenchRuntimeUi(_workbenchUiSurface, _frameContext);
        }
    }
}
