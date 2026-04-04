using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;
using Cortex.Renderers.Overlay;
using Cortex.Shell.Unity.Overlay.Ui;

namespace Cortex.Shell.Unity.Overlay
{
    public static class OverlayWorkbenchRuntimeUiComposition
    {
        public static IWorkbenchUiSurface CreateWorkbenchUiSurface()
        {
            return new OverlayWorkbenchUiSurface();
        }

        public static IWorkbenchRuntimeUiFactory CreateRuntimeUiFactory(IWorkbenchFrameContext frameContext)
        {
            return new OverlayWorkbenchRuntimeUiFactory(CreateWorkbenchUiSurface(), frameContext);
        }
    }
}
