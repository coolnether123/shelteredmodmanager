using Cortex.Rendering.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.DearImgui
{
    public sealed class DearImguiRenderPipeline : IRenderPipeline
    {
        private readonly IWorkbenchRenderer _workbenchRenderer = new DearImguiWorkbenchRenderer();

        public IWorkbenchRenderer WorkbenchRenderer { get { return _workbenchRenderer; } }
        public IPanelRenderer PanelRenderer { get { return NullWorkbenchRuntimeUi.Instance.RenderPipeline.PanelRenderer; } }
        public IOverlayRendererFactory OverlayRendererFactory { get { return NullWorkbenchRuntimeUi.Instance.RenderPipeline.OverlayRendererFactory; } }
    }
}
