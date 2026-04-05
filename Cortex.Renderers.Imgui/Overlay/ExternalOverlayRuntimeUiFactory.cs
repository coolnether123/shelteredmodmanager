using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.Imgui
{
    public sealed class ExternalOverlayRuntimeUiFactory : IWorkbenchRuntimeUiFactory
    {
        private readonly IWorkbenchUiSurface _workbenchUiSurface;
        private readonly IWorkbenchFrameContext _frameContext;

        public ExternalOverlayRuntimeUiFactory()
            : this(null, null)
        {
        }

        public ExternalOverlayRuntimeUiFactory(IWorkbenchUiSurface workbenchUiSurface, IWorkbenchFrameContext frameContext)
        {
            _workbenchUiSurface = workbenchUiSurface ?? NullWorkbenchUiSurface.Instance;
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
        }

        public IWorkbenchRuntimeUi Create()
        {
            return new ExternalOverlayRuntimeUi(_workbenchUiSurface, _frameContext);
        }
    }
}
