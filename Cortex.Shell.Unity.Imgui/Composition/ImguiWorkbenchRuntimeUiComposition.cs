using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;
using Cortex.Renderers.Imgui;
using Cortex.Shell.Unity.Imgui.Ui;

namespace Cortex.Shell.Unity.Imgui
{
    public static class ImguiWorkbenchRuntimeUiComposition
    {
        public static IWorkbenchUiSurface CreateWorkbenchUiSurface()
        {
            return new ImguiWorkbenchUiSurface();
        }

        public static IWorkbenchRuntimeUiFactory CreateRuntimeUiFactory(IWorkbenchFrameContext frameContext)
        {
            return new ImguiWorkbenchRuntimeUiFactory(CreateWorkbenchUiSurface(), frameContext);
        }
    }
}
