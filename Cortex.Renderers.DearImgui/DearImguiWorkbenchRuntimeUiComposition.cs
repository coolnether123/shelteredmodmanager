using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.DearImgui
{
    public static class DearImguiWorkbenchRuntimeUiComposition
    {
        public static IWorkbenchRuntimeUiFactory CreateRuntimeUiFactory(IWorkbenchFrameContext frameContext)
        {
            return new DearImguiWorkbenchRuntimeUiFactory(frameContext);
        }
    }
}
