using System;

namespace Cortex.Rendering.Models
{
    public sealed class HoverTooltipPlacementState
    {
        public string HoverKey = string.Empty;
        public float SpawnX;
    }

    public struct HoverTooltipPlacementOptions
    {
        public float AnchorVerticalOffset;
        public float FallbackCursorOffsetX;
        public float FallbackCursorOffsetY;
        public float ClampMinX;
        public float ClampMinY;
        public float ClampRightMargin;
        public float ClampBottomMargin;
    }

    public static class HoverTooltipPlacement
    {
        public static float ResolveSpawnX(HoverTooltipPlacementState state, string hoverKey, float mouseX)
        {
            if (state == null)
            {
                return mouseX;
            }

            var effectiveHoverKey = hoverKey ?? string.Empty;
            if (!string.Equals(state.HoverKey, effectiveHoverKey, StringComparison.Ordinal))
            {
                state.HoverKey = effectiveHoverKey;
                state.SpawnX = mouseX;
            }

            return state.SpawnX;
        }

        public static void Reset(HoverTooltipPlacementState state)
        {
            if (state == null)
            {
                return;
            }

            state.HoverKey = string.Empty;
            state.SpawnX = 0f;
        }

        public static RenderRect BuildRect(
            RenderRect anchorRect,
            RenderPoint mousePosition,
            float anchoredX,
            RenderSize viewportSize,
            float tooltipWidth,
            float height,
            HoverTooltipPlacementOptions options)
        {
            var rect = HasArea(anchorRect)
                ? BuildRectFromAnchor(anchorRect, anchoredX, viewportSize, tooltipWidth, height, options)
                : new RenderRect(
                    mousePosition.X + options.FallbackCursorOffsetX,
                    mousePosition.Y + options.FallbackCursorOffsetY,
                    tooltipWidth,
                    height);
            return ClampRect(rect, viewportSize, options);
        }

        private static RenderRect BuildRectFromAnchor(
            RenderRect anchorRect,
            float anchoredX,
            RenderSize viewportSize,
            float tooltipWidth,
            float height,
            HoverTooltipPlacementOptions options)
        {
            var y = anchorRect.Y + anchorRect.Height + options.AnchorVerticalOffset;
            if (y + height > viewportSize.Height - options.ClampBottomMargin)
            {
                y = anchorRect.Y - height - options.AnchorVerticalOffset;
            }

            return new RenderRect(anchoredX, y, tooltipWidth, height);
        }

        private static RenderRect ClampRect(RenderRect rect, RenderSize viewportSize, HoverTooltipPlacementOptions options)
        {
            var maxX = Math.Max(options.ClampMinX, viewportSize.Width - rect.Width - options.ClampRightMargin);
            var maxY = Math.Max(options.ClampMinY, viewportSize.Height - rect.Height - options.ClampBottomMargin);
            rect.X = Math.Min(rect.X, maxX);
            rect.Y = Math.Min(rect.Y, maxY);
            rect.X = Math.Max(options.ClampMinX, rect.X);
            rect.Y = Math.Max(options.ClampMinY, rect.Y);
            return rect;
        }

        private static bool HasArea(RenderRect rect)
        {
            return rect.Width > 0f && rect.Height > 0f;
        }
    }
}
