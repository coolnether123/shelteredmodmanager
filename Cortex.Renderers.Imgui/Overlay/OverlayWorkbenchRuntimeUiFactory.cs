using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.Overlay
{
    public sealed class OverlayWorkbenchRuntimeUiFactory : IWorkbenchRuntimeUiFactory
    {
        private readonly IWorkbenchUiSurface _workbenchUiSurface;
        private readonly IWorkbenchFrameContext _frameContext;

        public OverlayWorkbenchRuntimeUiFactory()
            : this(null, null)
        {
        }

        public OverlayWorkbenchRuntimeUiFactory(IWorkbenchUiSurface workbenchUiSurface, IWorkbenchFrameContext frameContext)
        {
            _workbenchUiSurface = workbenchUiSurface ?? NullWorkbenchUiSurface.Instance;
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
        }

        public IWorkbenchRuntimeUi Create()
        {
            return new OverlayWorkbenchRuntimeUi(_workbenchUiSurface, _frameContext);
        }
    }
}
