using Cortex.Rendering.Models;
using System.Collections.Generic;

namespace Cortex.Rendering.Abstractions
{
    public interface IWorkbenchRenderer
    {
        string RendererId { get; }
        string DisplayName { get; }
        RendererCapabilitySet Capabilities { get; }
    }

    public interface IPanelRenderer
    {
        PanelRenderResult Draw(RenderRect rect, PanelDocument document, RenderPoint scroll, PanelThemePalette theme);
    }

    public interface IHoverTooltipRenderer
    {
        void ResetTextTooltip();
        void RegisterTextTooltip(RenderRect anchorRect, string text);
        void DrawTextTooltip(HoverTooltipThemePalette theme);
        void ClearRichState();
        bool DrawRichTooltip(
            HoverTooltipRenderModel currentModel,
            RenderPoint mousePosition,
            RenderSize viewportSize,
            bool hasMouse,
            HoverTooltipThemePalette theme,
            float tooltipWidth,
            out HoverTooltipRenderResult result);
    }

    public interface IPopupMenuRenderer
    {
        void Reset();
        void QueueScrollDelta(float delta);
        bool TryCapturePointerInput(RenderRect menuRect, RenderPoint localMouse);
        PopupMenuRenderResult Draw(
            RenderPoint position,
            RenderSize viewportSize,
            string headerText,
            IList<PopupMenuItemModel> items,
            RenderPoint localMouse,
            PopupMenuThemePalette theme);
        RenderRect PredictMenuRect(RenderPoint position, RenderSize viewportSize, IList<PopupMenuItemModel> items);
    }

    public interface IOverlayRendererFactory
    {
        IHoverTooltipRenderer CreateHoverTooltipRenderer();
        IPopupMenuRenderer CreatePopupMenuRenderer();
    }

    public interface IRenderPipeline
    {
        IWorkbenchRenderer WorkbenchRenderer { get; }
        IPanelRenderer PanelRenderer { get; }
        IOverlayRendererFactory OverlayRendererFactory { get; }
    }

    public interface ITextMeasurementService
    {
        float MeasureTextWidth(string text);
        float MeasureLineHeight();
    }
}
