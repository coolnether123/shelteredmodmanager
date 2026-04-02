using Cortex.Plugins.Abstractions;
using Cortex.Rendering.Abstractions;

namespace Cortex.Rendering.RuntimeUi
{
    public interface IWorkbenchRuntimeUi
    {
        IRenderPipeline RenderPipeline { get; }
        IWorkbenchUiSurface WorkbenchUiSurface { get; }
        IWorkbenchFrameContext FrameContext { get; }
    }

    public interface IWorkbenchRuntimeUiFactory
    {
        IWorkbenchRuntimeUi Create();
    }

    public interface IWorkbenchRuntimeUiProvider
    {
        IWorkbenchRuntimeUi RuntimeUi { get; }
    }
}
