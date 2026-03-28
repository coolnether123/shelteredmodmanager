using System;
using Cortex.Rendering.Abstractions;

namespace Cortex.Renderers.Imgui
{
    public sealed class ImguiOverlayRendererFactory : IOverlayRendererFactory, IDisposable
    {
        private readonly ImguiModuleResources _moduleResources;
        private readonly bool _ownsModuleResources;

        public ImguiOverlayRendererFactory()
            : this(new ImguiModuleResources(), true)
        {
        }

        internal ImguiOverlayRendererFactory(ImguiModuleResources moduleResources)
            : this(moduleResources, false)
        {
        }

        private ImguiOverlayRendererFactory(ImguiModuleResources moduleResources, bool ownsModuleResources)
        {
            _moduleResources = moduleResources ?? new ImguiModuleResources();
            _ownsModuleResources = ownsModuleResources;
        }

        public IHoverTooltipRenderer CreateHoverTooltipRenderer()
        {
            return new ImguiHoverTooltipRenderer(_moduleResources);
        }

        public IPopupMenuRenderer CreatePopupMenuRenderer()
        {
            return new ImguiPopupMenuRenderer(_moduleResources);
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
