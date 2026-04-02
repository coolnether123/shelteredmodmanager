using System;
using Cortex.Rendering;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.RuntimeUi;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiOverlayRendererFactory : IOverlayRendererFactory, IDisposable
    {
        private readonly ImguiModuleResources _moduleResources;
        private readonly bool _ownsModuleResources;
        private readonly IWorkbenchFrameContext _frameContext;

        public ImguiOverlayRendererFactory()
            : this(new ImguiModuleResources(), null, true)
        {
        }

        internal ImguiOverlayRendererFactory(ImguiModuleResources moduleResources, IWorkbenchFrameContext frameContext)
            : this(moduleResources, frameContext, false)
        {
        }

        private ImguiOverlayRendererFactory(ImguiModuleResources moduleResources, IWorkbenchFrameContext frameContext, bool ownsModuleResources)
        {
            _moduleResources = moduleResources ?? new ImguiModuleResources();
            _ownsModuleResources = ownsModuleResources;
            _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
        }

        public IHoverTooltipRenderer CreateHoverTooltipRenderer()
        {
            return new ImguiHoverTooltipRenderer(_moduleResources, _frameContext);
        }

        public IPopupMenuRenderer CreatePopupMenuRenderer()
        {
            return new ImguiPopupMenuRenderer(_moduleResources, _frameContext);
        }

        public void Dispose()
        {
            if (_ownsModuleResources)
            {
                _moduleResources.Dispose();
            }
        }
    }
}
