using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.Tooltips
{
    public interface IHoverTooltipLayoutMeasurer
    {
        float MeasurePartWidth(EditorHoverContentPart part);
        float MeasurePathHeight(string text, float width);
        float MeasureMetaHeight(string text, float width);
        float MeasureDetailHeight(string text, float width);
        float MeasureLineHeight();
    }

    public sealed class HoverTooltipLayoutPlan
    {
        public bool Visible;
        public string HiddenReason = string.Empty;
        public HoverTooltipRenderModel Model;
        public RenderRect TooltipRect;
        public RenderRect PathRect;
        public RenderRect MetaRect;
        public RenderRect SignatureRect;
        public RenderRect DetailRect;
        public float EffectiveWidth;
        public float PathHeight;
        public float MetaHeight;
        public float SignatureHeight;
        public float DetailHeight;
        public string MetaText = string.Empty;
        public string DetailText = string.Empty;
        public EditorHoverContentPart HoveredPart;
        public readonly List<HoverTooltipPartLayout> PartLayouts = new List<HoverTooltipPartLayout>();
    }

    public static class HoverTooltipLayoutPlanner
    {
        public static HoverTooltipLayoutPlan BuildLayout(
            HoverTooltipRuntimeState state,
            HoverTooltipRenderModel currentModel,
            RenderPoint mousePosition,
            RenderSize requestedViewport,
            RenderSize fallbackViewport,
            bool hasPointer,
            DateTime utcNow,
            float requestedTooltipWidth,
            float defaultTooltipWidth,
            bool allowVisualRefresh,
            IHoverTooltipLayoutMeasurer measurer)
        {
            var plan = new HoverTooltipLayoutPlan();
            var viewport = HoverTooltipInteractionController.ResolveViewport(state, requestedViewport, fallbackViewport);
            if (!HoverTooltipInteractionController.IsUsableViewport(viewport))
            {
                plan.HiddenReason = "viewport-invalid";
                return plan;
            }

            if (measurer == null)
            {
                plan.HiddenReason = "measurer-null";
                return plan;
            }

            var activeModel = HoverTooltipInteractionController.ResolveVisibleModel(state, currentModel, mousePosition, hasPointer, utcNow);
            if (activeModel == null)
            {
                plan.HiddenReason = currentModel == null ? "model-null" : "model-hidden";
                return plan;
            }

            var signatureParts = HoverTooltipInteractionController.ResolveSignatureParts(activeModel);
            var partWidths = MeasurePartWidths(signatureParts, measurer);
            var lineHeight = Math.Max(18f, measurer.MeasureLineHeight());
            var effectiveWidth = HoverTooltipInteractionController.ResolveTooltipWidth(requestedTooltipWidth, defaultTooltipWidth, partWidths);
            var initialMetaText = HoverTooltipInteractionController.BuildMetaText(activeModel, null);
            var initialDetailText = HoverTooltipInteractionController.BuildDetailText(activeModel, null);
            var pathHeight = measurer.MeasurePathHeight(activeModel.QualifiedPath ?? string.Empty, effectiveWidth - 16f);
            var metaHeight = measurer.MeasureMetaHeight(initialMetaText, effectiveWidth - 16f);
            var signatureHeight = HoverTooltipInteractionController.LayoutParts(
                new RenderRect(0f, 0f, effectiveWidth - 16f, 0f),
                signatureParts,
                partWidths,
                lineHeight,
                null);
            if (signatureHeight <= 0f)
            {
                plan.HiddenReason = "signature-height-zero";
                HoverTooltipInteractionController.Reset(state);
                return plan;
            }

            var detailHeight = measurer.MeasureDetailHeight(initialDetailText, effectiveWidth - 16f);
            var tooltipRect = HoverTooltipInteractionController.ResolveTooltipRect(
                state,
                activeModel.Key ?? string.Empty,
                activeModel.AnchorRect,
                mousePosition,
                viewport,
                effectiveWidth,
                HoverTooltipInteractionController.BuildTooltipHeight(pathHeight, metaHeight, signatureHeight, detailHeight),
                allowVisualRefresh);
            var signatureRect = HoverTooltipInteractionController.BuildSignatureRect(tooltipRect, pathHeight, metaHeight);
            HoverTooltipInteractionController.LayoutParts(signatureRect, signatureParts, partWidths, lineHeight, plan.PartLayouts);
            var hoveredPart = HoverTooltipInteractionController.FindHoveredPart(plan.PartLayouts, mousePosition);

            var metaText = HoverTooltipInteractionController.BuildMetaText(activeModel, hoveredPart);
            metaHeight = measurer.MeasureMetaHeight(metaText, effectiveWidth - 16f);
            var detailText = HoverTooltipInteractionController.BuildDetailText(activeModel, hoveredPart);
            detailHeight = measurer.MeasureDetailHeight(detailText, effectiveWidth - 16f);
            tooltipRect = HoverTooltipInteractionController.ResolveTooltipRect(
                state,
                activeModel.Key ?? string.Empty,
                activeModel.AnchorRect,
                mousePosition,
                viewport,
                effectiveWidth,
                HoverTooltipInteractionController.BuildTooltipHeight(pathHeight, metaHeight, signatureHeight, detailHeight),
                allowVisualRefresh);

            plan.PartLayouts.Clear();
            signatureRect = HoverTooltipInteractionController.BuildSignatureRect(tooltipRect, pathHeight, metaHeight);
            HoverTooltipInteractionController.LayoutParts(signatureRect, signatureParts, partWidths, lineHeight, plan.PartLayouts);
            hoveredPart = HoverTooltipInteractionController.FindHoveredPart(plan.PartLayouts, mousePosition);

            plan.Visible = true;
            plan.Model = activeModel;
            plan.TooltipRect = tooltipRect;
            plan.PathRect = HoverTooltipInteractionController.BuildPathRect(tooltipRect, pathHeight);
            plan.MetaRect = HoverTooltipInteractionController.BuildMetaRect(tooltipRect, pathHeight, metaHeight);
            plan.SignatureRect = signatureRect;
            plan.DetailRect = HoverTooltipInteractionController.BuildDetailRect(tooltipRect, pathHeight, metaHeight, signatureHeight);
            plan.EffectiveWidth = effectiveWidth;
            plan.PathHeight = pathHeight;
            plan.MetaHeight = metaHeight;
            plan.SignatureHeight = signatureHeight;
            plan.DetailHeight = detailHeight;
            plan.MetaText = metaText ?? string.Empty;
            plan.DetailText = detailText ?? string.Empty;
            plan.HoveredPart = hoveredPart;
            return plan;
        }

        private static float[] MeasurePartWidths(EditorHoverContentPart[] parts, IHoverTooltipLayoutMeasurer measurer)
        {
            var widths = parts != null ? new float[parts.Length] : new float[0];
            for (var i = 0; parts != null && i < parts.Length; i++)
            {
                widths[i] = Math.Max(2f, measurer.MeasurePartWidth(parts[i]));
            }

            return widths;
        }
    }
}
