using System;
using Cortex.Core.Models;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.Tooltips
{
    public sealed class HoverTooltipRuntimeState
    {
        public readonly HoverTooltipPlacementState PlacementState = new HoverTooltipPlacementState();
        public RenderSize LastValidViewport;
        public HoverTooltipRenderModel StickyModel;
        public string StickyHoverKey = string.Empty;
        public string StickyHoverDocumentPath = string.Empty;
        public RenderRect StickyAnchorRect = new RenderRect(0f, 0f, 0f, 0f);
        public RenderRect StickyTooltipRect = new RenderRect(0f, 0f, 0f, 0f);
        public DateTime StickyKeepAliveUtc = DateTime.MinValue;
        public string PressedPartKey = string.Empty;
    }

    public sealed class HoverTooltipPartLayout
    {
        public EditorHoverContentPart Part;
        public RenderRect Bounds;
    }
}
