using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi;
using Cortex.Rendering.RuntimeUi.PopupMenus;
using Cortex.Rendering.RuntimeUi.Tooltips;
using Xunit;

namespace Cortex.Tests.Rendering
{
    public sealed class RuntimeUiInteractionTests
    {
        [Fact]
        public void PopupMenuInteractionController_ActivatesPressedItem_WithoutBackendEvents()
        {
            var state = new PopupMenuRuntimeState();
            var menuRect = new RenderRect(10f, 10f, 160f, 120f);
            var pointerPosition = new RenderPoint(34f, 52f);

            var downInput = new RuntimeUiPointerFrameInput
            {
                HasPointer = true,
                PointerPosition = pointerPosition,
                PointerButton = 0,
                EventKind = RuntimeUiPointerEventKind.Down
            };
            var downCapture = PopupMenuInteractionController.TryCapturePointerInput(state, menuRect, downInput);
            var downPrepared = PopupMenuInteractionController.PrepareFrame(state, downInput, 0f, true);
            var downInteraction = PopupMenuInteractionController.EvaluateItemInteraction(
                state,
                downInput,
                "open",
                true,
                true,
                downPrepared.HasQueuedPointerDown,
                false);

            var upInput = new RuntimeUiPointerFrameInput
            {
                HasPointer = true,
                PointerPosition = pointerPosition,
                PointerButton = 0,
                EventKind = RuntimeUiPointerEventKind.Up
            };
            var upCapture = PopupMenuInteractionController.TryCapturePointerInput(state, menuRect, upInput);
            var upPrepared = PopupMenuInteractionController.PrepareFrame(state, upInput, 0f, true);
            var upInteraction = PopupMenuInteractionController.EvaluateItemInteraction(
                state,
                upInput,
                "open",
                true,
                true,
                false,
                upPrepared.HasQueuedPointerUp);
            var frameResult = PopupMenuInteractionController.CompleteFrame(state, upInput, true);

            Assert.True(downCapture.Captured);
            Assert.True(downCapture.ShouldConsumeInput);
            Assert.True(downInteraction.IsPressedVisual);
            Assert.True(upCapture.Captured);
            Assert.True(upCapture.ShouldConsumeInput);
            Assert.True(upInteraction.ShouldClose);
            Assert.Equal("open", upInteraction.ActivatedCommandId);
            Assert.False(frameResult.ShouldClose);
        }

        [Fact]
        public void PopupMenuInteractionController_ClosesOnOutsideClick_WithoutBackendEvents()
        {
            var state = new PopupMenuRuntimeState();
            var menuRect = new RenderRect(10f, 10f, 160f, 120f);
            var input = new RuntimeUiPointerFrameInput
            {
                HasPointer = true,
                PointerPosition = new RenderPoint(220f, 220f),
                PointerButton = 0,
                EventKind = RuntimeUiPointerEventKind.Down
            };

            var capture = PopupMenuInteractionController.TryCapturePointerInput(state, menuRect, input);
            var frameResult = PopupMenuInteractionController.CompleteFrame(state, input, false);

            Assert.True(capture.Captured);
            Assert.True(capture.ShouldConsumeInput);
            Assert.True(frameResult.ShouldClose);
        }

        [Fact]
        public void PopupMenuInteractionController_DeduplicatesAnalogScroll_PerFrame()
        {
            var state = new PopupMenuRuntimeState();
            var menuRect = new RenderRect(10f, 10f, 160f, 120f);
            var input = new RuntimeUiPointerFrameInput
            {
                HasPointer = true,
                PointerPosition = new RenderPoint(30f, 30f),
                PointerButton = -1,
                FrameId = 42,
                AnalogScrollDelta = -1f
            };

            var capture = PopupMenuInteractionController.TryCapturePointerInput(state, menuRect, input);
            var firstFrame = PopupMenuInteractionController.PrepareFrame(state, input, 240f, true);
            var secondFrame = PopupMenuInteractionController.PrepareFrame(state, input, 240f, true);

            Assert.True(capture.Captured);
            Assert.False(capture.ShouldConsumeInput);
            Assert.Equal(180f, firstFrame.ScrollOffset);
            Assert.Equal(180f, secondFrame.ScrollOffset);
        }

        [Fact]
        public void HoverTooltipInteractionController_KeepsStickyModelVisible_WithoutBackendEvents()
        {
            var now = new DateTime(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
            var state = new HoverTooltipRuntimeState();
            var model = CreateHoverModel("alpha", new RenderRect(0f, 0f, 20f, 20f));

            var visible = HoverTooltipInteractionController.ResolveVisibleModel(
                state,
                model,
                new RenderPoint(5f, 5f),
                true,
                now);
            state.StickyTooltipRect = new RenderRect(40f, 0f, 60f, 30f);

            var stickyVisible = HoverTooltipInteractionController.ResolveVisibleModel(
                state,
                null,
                new RenderPoint(44f, 12f),
                true,
                now.AddMilliseconds(100d));

            Assert.Same(model, visible);
            Assert.Same(model, stickyVisible);
        }

        [Fact]
        public void HoverTooltipInteractionController_ComposesMetaAndDetail_FromPortableModel()
        {
            var part = new EditorHoverContentPart
            {
                Text = "Thing",
                SummaryText = "hovered summary",
                DocumentationText = "hovered docs",
                SupplementalSections = new[]
                {
                    new EditorHoverSection
                    {
                        Title = "baseTypes",
                        Text = "System.Object"
                    }
                }
            };
            var model = new HoverTooltipRenderModel
            {
                SummaryText = "model summary",
                DocumentationText = "model docs",
                SupplementalSections = new[]
                {
                    new EditorHoverSection
                    {
                        Title = "remarks",
                        DisplayParts = new[]
                        {
                            new EditorHoverContentPart { Text = "fallback section" }
                        }
                    }
                }
            };

            var metaText = HoverTooltipInteractionController.BuildMetaText(model, part);
            var detailText = HoverTooltipInteractionController.BuildDetailText(model, part);

            Assert.Equal("hovered summary", metaText);
            Assert.Contains("Base Types: System.Object", detailText);
            Assert.Contains("hovered docs", detailText);
        }

        [Fact]
        public void HoverTooltipInteractionController_PreservesStickyPlacement_WhenVisualRefreshIsDeferred()
        {
            var state = new HoverTooltipRuntimeState();
            var firstRect = HoverTooltipInteractionController.ResolveTooltipRect(
                state,
                "alpha",
                new RenderRect(0f, 0f, 20f, 12f),
                new RenderPoint(24f, 16f),
                new RenderSize(400f, 300f),
                220f,
                80f,
                true);
            var secondRect = HoverTooltipInteractionController.ResolveTooltipRect(
                state,
                "alpha",
                new RenderRect(0f, 0f, 20f, 12f),
                new RenderPoint(200f, 180f),
                new RenderSize(400f, 300f),
                220f,
                80f,
                false);

            Assert.Equal(firstRect.X, secondRect.X);
            Assert.Equal(firstRect.Y, secondRect.Y);
        }

        private static HoverTooltipRenderModel CreateHoverModel(string key, RenderRect anchorRect)
        {
            return new HoverTooltipRenderModel
            {
                Key = key,
                AnchorRect = anchorRect,
                SymbolDisplay = "Alpha",
                SignatureParts = new[]
                {
                    new EditorHoverContentPart
                    {
                        Text = "Alpha",
                        IsInteractive = true,
                        NavigationTarget = new EditorHoverNavigationTarget
                        {
                            MetadataName = "Alpha",
                            DefinitionDocumentPath = "Alpha.cs",
                            DefinitionRange = new LanguageServiceRange { Start = 12 }
                        }
                    }
                }
            };
        }
    }
}
