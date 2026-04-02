using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.Tooltips
{
    public static class HoverTooltipInteractionController
    {
        public const double StickyHoverGraceMs = 700d;
        public const float MaxTooltipHeight = 360f;
        public static readonly HoverTooltipPlacementOptions DefaultPlacementOptions = HoverTooltipPlacement.CreateAnchoredOptions(-3f);

        public static void Reset(HoverTooltipRuntimeState state)
        {
            if (state == null)
            {
                return;
            }

            state.LastValidViewport = new RenderSize(0f, 0f);
            HoverTooltipPlacement.Reset(state.PlacementState);
            state.StickyModel = null;
            state.StickyHoverKey = string.Empty;
            state.StickyHoverDocumentPath = string.Empty;
            state.StickyAnchorRect = new RenderRect(0f, 0f, 0f, 0f);
            state.StickyTooltipRect = new RenderRect(0f, 0f, 0f, 0f);
            state.StickyKeepAliveUtc = DateTime.MinValue;
            state.PressedPartKey = string.Empty;
        }

        public static bool IsUsableViewport(RenderSize viewportSize)
        {
            return viewportSize.Width >= 64f && viewportSize.Height >= 64f;
        }

        public static RenderSize ResolveViewport(HoverTooltipRuntimeState state, RenderSize requestedViewport, RenderSize fallbackViewport)
        {
            if (IsUsableViewport(requestedViewport))
            {
                if (state != null)
                {
                    state.LastValidViewport = requestedViewport;
                }

                return requestedViewport;
            }

            if (IsUsableViewport(fallbackViewport))
            {
                if (state != null)
                {
                    state.LastValidViewport = fallbackViewport;
                }

                return fallbackViewport;
            }

            return state != null ? state.LastValidViewport : new RenderSize(0f, 0f);
        }

        public static float ResolveTooltipWidth(float requestedTooltipWidth, float defaultTooltipWidth, float[] partWidths)
        {
            var maxWidth = requestedTooltipWidth > 0f ? requestedTooltipWidth : defaultTooltipWidth;
            var width = 0f;
            for (var i = 0; partWidths != null && i < partWidths.Length; i++)
            {
                if (partWidths[i] > 0f)
                {
                    width += partWidths[i];
                }
            }

            return Math.Min(maxWidth, Math.Max(180f, width + 20f));
        }

        public static EditorHoverContentPart[] ResolveSignatureParts(HoverTooltipRenderModel model)
        {
            if (model != null && model.SignatureParts != null && model.SignatureParts.Length > 0)
            {
                return model.SignatureParts;
            }

            return new[]
            {
                new EditorHoverContentPart
                {
                    Text = model != null ? model.SymbolDisplay ?? string.Empty : string.Empty,
                    IsInteractive = false
                }
            };
        }

        public static HoverTooltipRenderModel ResolveVisibleModel(
            HoverTooltipRuntimeState state,
            HoverTooltipRenderModel currentModel,
            RenderPoint pointerPosition,
            bool hasPointer,
            DateTime utcNow)
        {
            if (currentModel != null)
            {
                var currentKey = currentModel.Key ?? string.Empty;
                if (state != null &&
                    state.StickyModel != null &&
                    !string.IsNullOrEmpty(state.StickyHoverKey) &&
                    !string.Equals(currentKey, state.StickyHoverKey, StringComparison.Ordinal) &&
                    (IsPointerWithinRichHoverSurface(state, pointerPosition) || utcNow <= state.StickyKeepAliveUtc))
                {
                    return state.StickyModel;
                }

                if (state != null)
                {
                    state.StickyModel = currentModel;
                    state.StickyHoverKey = currentKey;
                    state.StickyHoverDocumentPath = currentModel.DocumentPath ?? string.Empty;
                    state.StickyAnchorRect = currentModel.AnchorRect;
                    RefreshKeepAlive(state, utcNow);
                }

                return currentModel;
            }

            if (state == null || state.StickyModel == null)
            {
                return null;
            }

            if ((hasPointer && IsPointerWithinRichHoverSurface(state, pointerPosition)) || utcNow <= state.StickyKeepAliveUtc)
            {
                return state.StickyModel;
            }

            Reset(state);
            return null;
        }

        public static float LayoutParts(RenderRect bounds, EditorHoverContentPart[] parts, float[] partWidths, float lineHeight, IList<HoverTooltipPartLayout> layouts)
        {
            var x = bounds.X;
            var y = bounds.Y;
            var maxX = bounds.X + Math.Max(8f, bounds.Width);
            var effectiveLineHeight = Math.Max(18f, lineHeight);

            for (var i = 0; parts != null && i < parts.Length; i++)
            {
                var part = parts[i];
                var text = part != null ? part.Text ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var width = partWidths != null && i < partWidths.Length
                    ? Math.Max(2f, partWidths[i])
                    : 2f;
                if (x > bounds.X && x + width > maxX)
                {
                    x = bounds.X;
                    y += effectiveLineHeight;
                }

                if (layouts != null)
                {
                    layouts.Add(new HoverTooltipPartLayout
                    {
                        Part = part,
                        Bounds = new RenderRect(x, y, width, effectiveLineHeight)
                    });
                }

                x += width;
            }

            return Math.Max(effectiveLineHeight, (y - bounds.Y) + effectiveLineHeight);
        }

        public static RenderRect BuildTooltipRect(
            HoverTooltipRuntimeState state,
            string hoverKey,
            RenderRect anchorRect,
            RenderPoint mousePosition,
            RenderSize viewportSize,
            float tooltipWidth,
            float height)
        {
            var spawnX = HoverTooltipPlacement.ResolveSpawnX(
                state != null ? state.PlacementState : null,
                hoverKey ?? string.Empty,
                mousePosition.X);
            return HoverTooltipPlacement.BuildRect(
                anchorRect,
                mousePosition,
                spawnX,
                viewportSize,
                tooltipWidth,
                height,
                DefaultPlacementOptions);
        }

        public static RenderRect ResolveTooltipRect(
            HoverTooltipRuntimeState state,
            string hoverKey,
            RenderRect anchorRect,
            RenderPoint mousePosition,
            RenderSize viewportSize,
            float tooltipWidth,
            float height,
            bool allowVisualRefresh)
        {
            if (state == null)
            {
                return BuildTooltipRect(null, hoverKey, anchorRect, mousePosition, viewportSize, tooltipWidth, height);
            }

            if (allowVisualRefresh || !HasArea(state.StickyTooltipRect))
            {
                state.StickyTooltipRect = BuildTooltipRect(state, hoverKey, anchorRect, mousePosition, viewportSize, tooltipWidth, height);
                return state.StickyTooltipRect;
            }

            var stickyRect = state.StickyTooltipRect;
            stickyRect.Width = tooltipWidth;
            stickyRect.Height = height;
            state.StickyTooltipRect = HoverTooltipPlacement.ClampRect(stickyRect, viewportSize, DefaultPlacementOptions);
            return state.StickyTooltipRect;
        }

        public static float BuildTooltipHeight(float pathHeight, float metaHeight, float signatureHeight, float detailHeight)
        {
            return Math.Min(
                MaxTooltipHeight,
                14f + pathHeight + metaHeight + signatureHeight + (detailHeight > 0f ? detailHeight + 8f : 0f));
        }

        public static RenderRect BuildPathRect(RenderRect tooltipRect, float pathHeight)
        {
            return new RenderRect(
                tooltipRect.X + 8f,
                tooltipRect.Y + 7f,
                tooltipRect.Width - 16f,
                Math.Max(0f, pathHeight));
        }

        public static RenderRect BuildMetaRect(RenderRect tooltipRect, float pathHeight, float metaHeight)
        {
            return new RenderRect(
                tooltipRect.X + 8f,
                tooltipRect.Y + 7f + pathHeight,
                tooltipRect.Width - 16f,
                Math.Max(0f, metaHeight));
        }

        public static RenderRect BuildSignatureRect(RenderRect tooltipRect, float pathHeight, float metaHeight)
        {
            return new RenderRect(
                tooltipRect.X + 8f,
                tooltipRect.Y + 7f + pathHeight + metaHeight + (metaHeight > 0f ? 4f : 0f),
                Math.Max(0f, tooltipRect.Width - 16f),
                Math.Max(0f, tooltipRect.Height - pathHeight - metaHeight - 14f));
        }

        public static RenderRect BuildDetailRect(RenderRect tooltipRect, float pathHeight, float metaHeight, float signatureHeight)
        {
            return new RenderRect(
                tooltipRect.X + 8f,
                tooltipRect.Y + 7f + pathHeight + metaHeight + (metaHeight > 0f ? 4f : 0f) + signatureHeight + 6f,
                tooltipRect.Width - 16f,
                Math.Max(0f, tooltipRect.Height - pathHeight - metaHeight - signatureHeight - 20f));
        }

        public static EditorHoverContentPart FindHoveredPart(IList<HoverTooltipPartLayout> layouts, RenderPoint pointerPosition)
        {
            if (layouts == null)
            {
                return null;
            }

            for (var i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                var part = layout != null ? layout.Part : null;
                if (part != null && part.IsInteractive && RuntimeUiHitTest.Contains(layout.Bounds, pointerPosition))
                {
                    return part;
                }
            }

            return null;
        }

        public static RenderRect FindPartBounds(IList<HoverTooltipPartLayout> layouts, EditorHoverContentPart hoveredPart)
        {
            if (layouts == null || hoveredPart == null)
            {
                return new RenderRect(0f, 0f, 0f, 0f);
            }

            for (var i = 0; i < layouts.Count; i++)
            {
                var layout = layouts[i];
                if (layout != null && object.ReferenceEquals(layout.Part, hoveredPart))
                {
                    return layout.Bounds;
                }
            }

            return new RenderRect(0f, 0f, 0f, 0f);
        }

        public static EditorHoverContentPart HandlePartPointerInput(HoverTooltipRuntimeState state, RuntimeUiPointerFrameInput input, EditorHoverContentPart hoveredPart)
        {
            if (state == null || input.PointerButton != 0)
            {
                return null;
            }

            var partKey = hoveredPart != null && hoveredPart.IsInteractive ? BuildPartKey(hoveredPart) : string.Empty;
            if (input.EventKind == RuntimeUiPointerEventKind.Down)
            {
                state.PressedPartKey = partKey;
                return null;
            }

            if (input.EventKind != RuntimeUiPointerEventKind.Up)
            {
                return null;
            }

            var shouldActivate = !string.IsNullOrEmpty(partKey) &&
                string.Equals(state.PressedPartKey, partKey, StringComparison.Ordinal);
            state.PressedPartKey = string.Empty;
            return shouldActivate ? hoveredPart : null;
        }

        public static string BuildMetaText(HoverTooltipRenderModel model, EditorHoverContentPart hoveredPart)
        {
            if (hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.SummaryText))
            {
                return hoveredPart.SummaryText ?? string.Empty;
            }

            return model != null ? model.SummaryText ?? string.Empty : string.Empty;
        }

        public static string BuildDetailText(HoverTooltipRenderModel model, EditorHoverContentPart hoveredPart)
        {
            var detail = hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.DocumentationText)
                ? hoveredPart.DocumentationText ?? string.Empty
                : (model != null ? model.DocumentationText ?? string.Empty : string.Empty);
            var supplemental = FormatSupplementalSections(
                hoveredPart != null && hoveredPart.SupplementalSections != null && hoveredPart.SupplementalSections.Length > 0
                    ? hoveredPart.SupplementalSections
                    : (model != null ? model.SupplementalSections : null));

            if (string.IsNullOrEmpty(detail))
            {
                return supplemental;
            }

            if (string.IsNullOrEmpty(supplemental))
            {
                return detail;
            }

            return supplemental + Environment.NewLine + detail;
        }

        public static bool IsPointerWithinRichHoverSurface(HoverTooltipRuntimeState state, RenderPoint pointerPosition)
        {
            if (state == null)
            {
                return false;
            }

            if (IsPointerWithinTooltip(state, pointerPosition))
            {
                return true;
            }

            if (HasArea(state.StickyAnchorRect) && RuntimeUiHitTest.Contains(state.StickyAnchorRect, pointerPosition))
            {
                return true;
            }

            var bridgeRect = BuildHoverBridgeRect(state);
            return HasArea(bridgeRect) && RuntimeUiHitTest.Contains(bridgeRect, pointerPosition);
        }

        public static void RefreshKeepAlive(HoverTooltipRuntimeState state, DateTime utcNow)
        {
            if (state == null)
            {
                return;
            }

            state.StickyKeepAliveUtc = utcNow.AddMilliseconds(StickyHoverGraceMs);
        }

        public static string BuildPartKey(EditorHoverContentPart part)
        {
            var navigationTarget = part != null ? part.NavigationTarget : null;
            return (part != null ? part.Text ?? string.Empty : string.Empty) + "|" +
                (navigationTarget != null ? navigationTarget.MetadataName ?? string.Empty : string.Empty) + "|" +
                (navigationTarget != null ? navigationTarget.DefinitionDocumentPath ?? string.Empty : string.Empty) + "|" +
                (navigationTarget != null && navigationTarget.DefinitionRange != null ? navigationTarget.DefinitionRange.Start.ToString() : string.Empty);
        }

        private static string FormatSupplementalSections(EditorHoverSection[] sections)
        {
            if (sections == null || sections.Length == 0)
            {
                return string.Empty;
            }

            var lines = new List<string>();
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var text = !string.IsNullOrEmpty(section.Text)
                    ? section.Text ?? string.Empty
                    : FlattenDisplayParts(section.DisplayParts);
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(section.Title))
                {
                    lines.Add(HumanizeSectionTitle(section.Title) + ": " + text);
                }
                else
                {
                    lines.Add(text);
                }
            }

            return lines.Count > 0
                ? string.Join(Environment.NewLine, lines.ToArray())
                : string.Empty;
        }

        private static string FlattenDisplayParts(EditorHoverContentPart[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            var text = string.Empty;
            for (var i = 0; i < parts.Length; i++)
            {
                text += parts[i] != null ? parts[i].Text ?? string.Empty : string.Empty;
            }

            return text.Trim();
        }

        private static string HumanizeSectionTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
            {
                return string.Empty;
            }

            var result = string.Empty;
            for (var i = 0; i < title.Length; i++)
            {
                var current = title[i];
                if (i > 0 && char.IsUpper(current) && !char.IsWhiteSpace(title[i - 1]))
                {
                    result += " ";
                }

                result += current;
            }

            return result;
        }

        private static bool IsPointerWithinTooltip(HoverTooltipRuntimeState state, RenderPoint pointerPosition)
        {
            return state != null && HasArea(state.StickyTooltipRect) && RuntimeUiHitTest.Contains(state.StickyTooltipRect, pointerPosition);
        }

        private static RenderRect BuildHoverBridgeRect(HoverTooltipRuntimeState state)
        {
            if (state == null || !HasArea(state.StickyAnchorRect) || !HasArea(state.StickyTooltipRect))
            {
                return new RenderRect(0f, 0f, 0f, 0f);
            }

            var overlapLeft = Max(state.StickyAnchorRect.X, state.StickyTooltipRect.X);
            var overlapRight = Min(state.StickyAnchorRect.X + state.StickyAnchorRect.Width, state.StickyTooltipRect.X + state.StickyTooltipRect.Width);
            if (overlapRight > overlapLeft)
            {
                return BuildRect(
                    overlapLeft - 6f,
                    Min(state.StickyAnchorRect.Y, state.StickyTooltipRect.Y) - 4f,
                    overlapRight + 6f,
                    Max(state.StickyAnchorRect.Y + state.StickyAnchorRect.Height, state.StickyTooltipRect.Y + state.StickyTooltipRect.Height) + 4f);
            }

            var overlapTop = Max(state.StickyAnchorRect.Y, state.StickyTooltipRect.Y);
            var overlapBottom = Min(state.StickyAnchorRect.Y + state.StickyAnchorRect.Height, state.StickyTooltipRect.Y + state.StickyTooltipRect.Height);
            if (overlapBottom > overlapTop)
            {
                return BuildRect(
                    Min(state.StickyAnchorRect.X, state.StickyTooltipRect.X) - 4f,
                    overlapTop - 6f,
                    Max(state.StickyAnchorRect.X + state.StickyAnchorRect.Width, state.StickyTooltipRect.X + state.StickyTooltipRect.Width) + 4f,
                    overlapBottom + 6f);
            }

            var anchorCenterX = state.StickyAnchorRect.X + (state.StickyAnchorRect.Width * 0.5f);
            var anchorCenterY = state.StickyAnchorRect.Y + (state.StickyAnchorRect.Height * 0.5f);
            var tooltipCenterX = state.StickyTooltipRect.X + (state.StickyTooltipRect.Width * 0.5f);
            var tooltipCenterY = state.StickyTooltipRect.Y + (state.StickyTooltipRect.Height * 0.5f);
            return BuildRect(
                Min(anchorCenterX, tooltipCenterX) - 8f,
                Min(anchorCenterY, tooltipCenterY) - 8f,
                Max(anchorCenterX, tooltipCenterX) + 8f,
                Max(anchorCenterY, tooltipCenterY) + 8f);
        }

        private static RenderRect BuildRect(float minX, float minY, float maxX, float maxY)
        {
            return new RenderRect(minX, minY, Max(0f, maxX - minX), Max(0f, maxY - minY));
        }

        private static bool HasArea(RenderRect rect)
        {
            return rect.Width > 0f && rect.Height > 0f;
        }

        private static float Min(float left, float right)
        {
            return left < right ? left : right;
        }

        private static float Max(float left, float right)
        {
            return left > right ? left : right;
        }
    }
}
