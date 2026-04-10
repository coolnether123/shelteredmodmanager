using Cortex.Rendering;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.DearImgui
{
    public sealed class DearImguiWorkbenchRuntimeUiFactory : IWorkbenchRuntimeUiFactory
    {
        private readonly IWorkbenchFrameContext _frameContext;

        public DearImguiWorkbenchRuntimeUiFactory(IWorkbenchFrameContext frameContext)
        {
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
        }

        public IWorkbenchRuntimeUi Create()
        {
            return new DearImguiWorkbenchRuntimeUi(_frameContext);
        }
    }
}
