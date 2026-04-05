using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Abstractions;

namespace Cortex.Rendering.RuntimeUi
{
    public enum WorkbenchRuntimeUiLayoutMode
    {
        IntegratedShellWindow = 0,
        OverlayWindows = 1,
        ExternalOverlayWindows = 2
    }

    public interface IWorkbenchRuntimeUi
    {
        IRenderPipeline RenderPipeline { get; }
        IWorkbenchUiSurface WorkbenchUiSurface { get; }
        IWorkbenchFrameContext FrameContext { get; }
        WorkbenchRuntimeUiLayoutMode LayoutMode { get; }
    }

    public interface IWorkbenchRuntimeUiFactory
    {
        IWorkbenchRuntimeUi Create();
    }

    public interface IWorkbenchRuntimeUiProvider
    {
        IWorkbenchRuntimeUi RuntimeUi { get; }
    }

    public interface IWorkbenchRuntimeUiSwitcher
    {
        bool SwitchRuntimeUi(IWorkbenchRuntimeUi runtimeUi);
    }
}
