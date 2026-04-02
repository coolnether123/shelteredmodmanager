using System.Collections.Generic;
using Cortex.Plugins.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi
{
    public sealed class NullWorkbenchRuntimeUi : IWorkbenchRuntimeUi
    {
        public static readonly IWorkbenchRuntimeUi Instance = new NullWorkbenchRuntimeUi();

        private static readonly IRenderPipeline RenderPipelineInstance = new NullRenderPipeline();

        private NullWorkbenchRuntimeUi()
        {
        }

        public IRenderPipeline RenderPipeline
        {
            get { return RenderPipelineInstance; }
        }

        public IWorkbenchUiSurface WorkbenchUiSurface
        {
            get { return NullWorkbenchUiSurface.Instance; }
        }

        public IWorkbenchFrameContext FrameContext
        {
            get { return NullWorkbenchFrameContext.Instance; }
        }

        private sealed class NullRenderPipeline : IRenderPipeline
        {
            private static readonly IWorkbenchRenderer WorkbenchRendererInstance = new NullWorkbenchRenderer();
            private static readonly IPanelRenderer PanelRendererInstance = new NullPanelRenderer();
            private static readonly IOverlayRendererFactory OverlayRendererFactoryInstance = new NullOverlayRendererFactory();

            public IWorkbenchRenderer WorkbenchRenderer
            {
                get { return WorkbenchRendererInstance; }
            }

            public IPanelRenderer PanelRenderer
            {
                get { return PanelRendererInstance; }
            }

            public IOverlayRendererFactory OverlayRendererFactory
            {
                get { return OverlayRendererFactoryInstance; }
            }
        }

        private sealed class NullWorkbenchRenderer : IWorkbenchRenderer
        {
            private static readonly RendererCapabilitySet CapabilitiesInstance = new RendererCapabilitySet();

            public string RendererId
            {
                get { return "runtime-ui.null"; }
            }

            public string DisplayName
            {
                get { return "Unavailable UI"; }
            }

            public RendererCapabilitySet Capabilities
            {
                get { return CapabilitiesInstance; }
            }
        }

        private sealed class NullPanelRenderer : IPanelRenderer
        {
            public PanelRenderResult Draw(RenderRect rect, PanelDocument document, RenderPoint scroll, PanelThemePalette theme)
            {
                var result = new PanelRenderResult();
                result.Scroll = scroll;
                return result;
            }
        }

        private sealed class NullOverlayRendererFactory : IOverlayRendererFactory
        {
            public IHoverTooltipRenderer CreateHoverTooltipRenderer()
            {
                return new NullHoverTooltipRenderer();
            }

            public IPopupMenuRenderer CreatePopupMenuRenderer()
            {
                return new NullPopupMenuRenderer();
            }
        }

        private sealed class NullHoverTooltipRenderer : IHoverTooltipRenderer
        {
            public void ResetTextTooltip()
            {
            }

            public void RegisterTextTooltip(RenderRect anchorRect, string text)
            {
            }

            public void DrawTextTooltip(HoverTooltipThemePalette theme)
            {
            }

            public void ClearRichState()
            {
            }

            public bool DrawRichTooltip(
                HoverTooltipRenderModel currentModel,
                RenderPoint mousePosition,
                RenderSize viewportSize,
                bool hasMouse,
                HoverTooltipThemePalette theme,
                float tooltipWidth,
                out HoverTooltipRenderResult result)
            {
                result = new HoverTooltipRenderResult();
                return false;
            }
        }

        private sealed class NullPopupMenuRenderer : IPopupMenuRenderer
        {
            public void Reset()
            {
            }

            public void QueueScrollDelta(float delta)
            {
            }

            public bool TryCapturePointerInput(RenderRect menuRect, RenderPoint localMouse)
            {
                return false;
            }

            public PopupMenuRenderResult Draw(
                RenderPoint position,
                RenderSize viewportSize,
                string headerText,
                IList<PopupMenuItemModel> items,
                RenderPoint localMouse,
                PopupMenuThemePalette theme)
            {
                return new PopupMenuRenderResult();
            }

            public RenderRect PredictMenuRect(RenderPoint position, RenderSize viewportSize, IList<PopupMenuItemModel> items)
            {
                return new RenderRect(position.X, position.Y, 0f, 0f);
            }
        }
    }

    public sealed class NullWorkbenchRuntimeUiFactory : IWorkbenchRuntimeUiFactory
    {
        public static readonly IWorkbenchRuntimeUiFactory Instance = new NullWorkbenchRuntimeUiFactory();

        private NullWorkbenchRuntimeUiFactory()
        {
        }

        public IWorkbenchRuntimeUi Create()
        {
            return NullWorkbenchRuntimeUi.Instance;
        }
    }

    public sealed class NullWorkbenchUiSurface : IWorkbenchUiSurface
    {
        public static readonly IWorkbenchUiSurface Instance = new NullWorkbenchUiSurface();

        private NullWorkbenchUiSurface()
        {
        }

        public string DrawSearchToolbar(string label, string draftQuery, float height, bool expandWidth)
        {
            return draftQuery ?? string.Empty;
        }

        public bool DrawNavigationGroupHeader(string title, bool isActive, bool isExpanded)
        {
            return false;
        }

        public bool DrawNavigationItem(string title, bool isSelected, float indent)
        {
            return false;
        }

        public void DrawCollapsedNavigationItem(string title, float indent)
        {
        }

        public void DrawSectionHeader(string title, string description)
        {
        }

        public void DrawSectionPanel(string title, System.Action drawBody)
        {
            if (drawBody != null)
            {
                drawBody();
            }
        }

        public void DrawPopupMenuPanel(float width, System.Action drawBody)
        {
            if (drawBody != null)
            {
                drawBody();
            }
        }

        public void BeginPropertyRow()
        {
        }

        public void EndPropertyRow()
        {
        }

        public void DrawPropertyLabelColumn(string title, string description)
        {
        }
    }
}
