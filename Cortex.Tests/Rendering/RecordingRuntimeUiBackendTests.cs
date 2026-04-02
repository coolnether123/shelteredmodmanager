using System;
using System.Collections.Generic;
using System.Linq;
using Cortex.Core.Models;
using Cortex.Rendering;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi;
using Cortex.Rendering.RuntimeUi.Panels;
using Cortex.Rendering.RuntimeUi.PopupMenus;
using Cortex.Rendering.RuntimeUi.Tooltips;
using Xunit;

namespace Cortex.Tests.Rendering
{
    public sealed class RecordingRuntimeUiBackendTests
    {
        [Fact]
        public void RecordingBackend_RecordsPortablePanelPopupAndTooltipCommands()
        {
            var frameContext = new TestWorkbenchFrameContext();
            frameContext.SetSnapshot(new WorkbenchFrameInputSnapshot
            {
                ViewportSize = new RenderSize(1024f, 768f),
                AllowsVisualRefresh = true,
                CurrentMousePosition = new RenderPoint(180f, 140f),
                PointerPosition = new RenderPoint(180f, 140f)
            });

            var pipeline = new RecordingRenderPipeline(frameContext);
            var panelResult = pipeline.PanelRenderer.Draw(
                new RenderRect(20f, 20f, 420f, 320f),
                BuildPanelDocument(),
                RenderPoint.Zero,
                new PanelThemePalette());

            Assert.Equal(RenderPoint.Zero.X, panelResult.Scroll.X);
            Assert.Contains(pipeline.Commands, command => command.Kind == "panel.title");
            Assert.Contains(pipeline.Commands, command => command.Kind == "panel.card.row");

            pipeline.Commands.Clear();
            var popupRenderer = pipeline.OverlayRendererFactory.CreatePopupMenuRenderer();
            var popupResult = popupRenderer.Draw(
                new RenderPoint(80f, 80f),
                frameContext.Snapshot.ViewportSize,
                "Actions",
                BuildPopupItems(18),
                new RenderPoint(120f, 150f),
                new PopupMenuThemePalette());

            Assert.True(popupResult.MenuRect.Width > 0f);
            Assert.Contains(pipeline.Commands, command => command.Kind == "popup.header");
            Assert.Contains(pipeline.Commands, command => command.Kind == "popup.item");
            Assert.Contains(pipeline.Commands, command => command.Kind == "popup.scroll");

            pipeline.Commands.Clear();
            var hoverRenderer = pipeline.OverlayRendererFactory.CreateHoverTooltipRenderer();
            HoverTooltipRenderResult hoverResult;
            var visible = hoverRenderer.DrawRichTooltip(
                BuildHoverModel(),
                new RenderPoint(180f, 140f),
                frameContext.Snapshot.ViewportSize,
                true,
                new HoverTooltipThemePalette(),
                420f,
                out hoverResult);

            Assert.True(visible);
            Assert.True(hoverResult.Visible);
            Assert.Contains(pipeline.Commands, command => command.Kind == "tooltip.path");
            Assert.Contains(pipeline.Commands, command => command.Kind == "tooltip.part");
            Assert.Contains(pipeline.Commands, command => command.Kind == "tooltip.detail");
        }

        [Fact]
        public void RecordingPopupBackend_UsesFrameSnapshotForCaptureAndActivation()
        {
            var frameContext = new TestWorkbenchFrameContext();
            var commands = new List<RecordingRenderCommand>();
            var popupRenderer = new RecordingPopupMenuRenderer(frameContext, commands);
            var items = BuildPopupItems(3);
            var menuRect = popupRenderer.PredictMenuRect(new RenderPoint(80f, 80f), new RenderSize(640f, 480f), items);
            var pointerInsideFirstItem = new RenderPoint(menuRect.X + 18f, menuRect.Y + PopupMenuLayoutPlanner.HeaderHeight + 8f);

            frameContext.SetSnapshot(new WorkbenchFrameInputSnapshot
            {
                ViewportSize = new RenderSize(640f, 480f),
                HasCurrentEvent = true,
                AllowsVisualRefresh = true,
                CurrentEventKind = WorkbenchInputEventKind.MouseDown,
                CurrentMouseButton = 0,
                PointerPosition = pointerInsideFirstItem
            });

            Assert.True(popupRenderer.TryCapturePointerInput(menuRect, pointerInsideFirstItem));
            Assert.Equal(1, frameContext.ConsumedCount);

            frameContext.SetSnapshot(new WorkbenchFrameInputSnapshot
            {
                ViewportSize = new RenderSize(640f, 480f),
                HasCurrentEvent = true,
                AllowsVisualRefresh = true,
                CurrentEventKind = WorkbenchInputEventKind.MouseUp,
                CurrentMouseButton = 0,
                PointerPosition = pointerInsideFirstItem
            });

            Assert.True(popupRenderer.TryCapturePointerInput(menuRect, pointerInsideFirstItem));

            var result = popupRenderer.Draw(
                new RenderPoint(80f, 80f),
                new RenderSize(640f, 480f),
                "Actions",
                items,
                pointerInsideFirstItem,
                new PopupMenuThemePalette());

            Assert.True(result.ShouldClose);
            Assert.Equal("cmd-0", result.ActivatedCommandId);
            Assert.Equal(2, frameContext.ConsumedCount);
            Assert.Contains(commands, command => command.Kind == "popup.item");
        }

        private static PanelDocument BuildPanelDocument()
        {
            return new PanelDocument
            {
                Title = "Inspector",
                Subtitle = "Portable proof backend",
                ShowCloseButton = true,
                HeaderActions = new[]
                {
                    new PanelAction { Id = "refresh", Label = "Refresh", Enabled = true }
                },
                Sections = new[]
                {
                    new PanelSection
                    {
                        Id = "summary",
                        Title = "Summary",
                        Expanded = true,
                        Elements = new PanelElement[]
                        {
                            new PanelMetadataElement { Label = "Symbol", Value = "Player.Update", DrawDivider = true },
                            new PanelTextElement { Label = "Body", Value = "Portable runtime UI should own layout and interaction behavior." },
                            new PanelActionElement { Action = new PanelAction { Id = "open", Label = "Open", Enabled = true }, Hint = "Navigate to source." },
                            new PanelCardElement
                            {
                                Title = "Relationships",
                                Body = "One incoming call",
                                Rows = new[]
                                {
                                    new PanelMetadataElement { Label = "Callers", Value = "1", DrawDivider = false }
                                },
                                Actions = new[]
                                {
                                    new PanelAction { Id = "inspect", Label = "Inspect", Enabled = true }
                                }
                            }
                        }
                    }
                }
            };
        }

        private static IList<PopupMenuItemModel> BuildPopupItems(int count)
        {
            var items = new List<PopupMenuItemModel>();
            for (var i = 0; i < count; i++)
            {
                items.Add(new PopupMenuItemModel
                {
                    CommandId = "cmd-" + i,
                    Label = "Item " + i,
                    Enabled = true,
                    ShortcutText = i == 0 ? "Enter" : string.Empty
                });
            }

            return items;
        }

        private static HoverTooltipRenderModel BuildHoverModel()
        {
            return new HoverTooltipRenderModel
            {
                Key = "hover:player.update",
                ContextKey = "ctx",
                DocumentPath = "Player.cs",
                AnchorRect = new RenderRect(120f, 120f, 60f, 18f),
                QualifiedPath = "Game.Player.Update",
                SummaryText = "Updates the player state.",
                DocumentationText = "Portable tooltip layout should not depend on IMGUI event semantics.",
                SignatureParts = new[]
                {
                    new EditorHoverContentPart { Text = "void", IsInteractive = false },
                    new EditorHoverContentPart { Text = " ", IsInteractive = false },
                    new EditorHoverContentPart
                    {
                        Text = "Update",
                        IsInteractive = true,
                        DocumentationText = "Symbol link documentation.",
                        SupplementalSections = new[]
                        {
                            new EditorHoverSection { Title = "Returns", Text = "No value." }
                        }
                    }
                },
                SupplementalSections = new[]
                {
                    new EditorHoverSection { Title = "Remarks", Text = "Called every frame." }
                },
                OverloadIndex = 0,
                OverloadCount = 1
            };
        }

        private sealed class RecordingRenderPipeline : IRenderPipeline
        {
            private readonly RecordingOverlayRendererFactory _overlayRendererFactory;
            private readonly RecordingPanelRenderer _panelRenderer;

            public RecordingRenderPipeline(IWorkbenchFrameContext frameContext)
            {
                Commands = new List<RecordingRenderCommand>();
                WorkbenchRenderer = new RecordingWorkbenchRenderer();
                _panelRenderer = new RecordingPanelRenderer(Commands);
                _overlayRendererFactory = new RecordingOverlayRendererFactory(frameContext, Commands);
            }

            public List<RecordingRenderCommand> Commands { get; private set; }
            public IWorkbenchRenderer WorkbenchRenderer { get; private set; }
            public IPanelRenderer PanelRenderer { get { return _panelRenderer; } }
            public IOverlayRendererFactory OverlayRendererFactory { get { return _overlayRendererFactory; } }
        }

        private sealed class RecordingWorkbenchRenderer : IWorkbenchRenderer
        {
            private static readonly RendererCapabilitySet CapabilitiesInstance = new RendererCapabilitySet();

            public string RendererId { get { return "recording"; } }
            public string DisplayName { get { return "Recording Backend"; } }
            public RendererCapabilitySet Capabilities { get { return CapabilitiesInstance; } }
        }

        private sealed class RecordingOverlayRendererFactory : IOverlayRendererFactory
        {
            private readonly IWorkbenchFrameContext _frameContext;
            private readonly List<RecordingRenderCommand> _commands;

            public RecordingOverlayRendererFactory(IWorkbenchFrameContext frameContext, List<RecordingRenderCommand> commands)
            {
                _frameContext = frameContext;
                _commands = commands;
            }

            public IHoverTooltipRenderer CreateHoverTooltipRenderer()
            {
                return new RecordingHoverTooltipRenderer(_frameContext, _commands);
            }

            public IPopupMenuRenderer CreatePopupMenuRenderer()
            {
                return new RecordingPopupMenuRenderer(_frameContext, _commands);
            }
        }

        private sealed class RecordingPanelRenderer : IPanelRenderer
        {
            private readonly List<RecordingRenderCommand> _commands;
            private readonly IPanelLayoutMeasurer _measurer = new RecordingPanelLayoutMeasurer();

            public RecordingPanelRenderer(List<RecordingRenderCommand> commands)
            {
                _commands = commands;
            }

            public PanelRenderResult Draw(RenderRect rect, PanelDocument document, RenderPoint scroll, PanelThemePalette theme)
            {
                var result = new PanelRenderResult();
                result.Scroll = scroll;
                if (document == null)
                {
                    return result;
                }

                var rootLayout = PanelLayoutPlanner.BuildRootLayout(new RenderRect(0f, 0f, rect.Width, rect.Height), document.HeaderActions);
                var contentLayout = PanelLayoutPlanner.BuildContentLayout(document, rootLayout.ContentViewport.Width, _measurer);
                _commands.Add(new RecordingRenderCommand("panel.root", rootLayout.PanelRect, document.Title));
                _commands.Add(new RecordingRenderCommand("panel.title", rootLayout.TitleRect, document.Title));
                _commands.Add(new RecordingRenderCommand("panel.subtitle", rootLayout.SubtitleRect, document.Subtitle));

                for (var sectionIndex = 0; sectionIndex < contentLayout.Sections.Count; sectionIndex++)
                {
                    var section = contentLayout.Sections[sectionIndex];
                    _commands.Add(new RecordingRenderCommand("panel.section", section.HeaderRect, section.Section != null ? section.Section.Title : string.Empty));
                    for (var elementIndex = 0; elementIndex < section.ElementLayouts.Count; elementIndex++)
                    {
                        var element = section.ElementLayouts[elementIndex];
                        if (element.MetadataLayout != null)
                        {
                            _commands.Add(new RecordingRenderCommand("panel.metadata", element.MetadataLayout.ValueRect, ((PanelMetadataElement)element.Element).Value));
                        }

                        if (element.TextLayout != null)
                        {
                            _commands.Add(new RecordingRenderCommand("panel.text", element.TextLayout.ValueRect, ((PanelTextElement)element.Element).Value));
                        }

                        if (element.ActionLayout != null)
                        {
                            _commands.Add(new RecordingRenderCommand("panel.action", element.ActionLayout.ButtonRect, ((PanelActionElement)element.Element).Action.Label));
                        }

                        if (element.CardLayout != null)
                        {
                            _commands.Add(new RecordingRenderCommand("panel.card", element.Bounds, ((PanelCardElement)element.Element).Title));
                            for (var rowIndex = 0; rowIndex < element.CardLayout.RowLayouts.Count; rowIndex++)
                            {
                                _commands.Add(new RecordingRenderCommand("panel.card.row", element.CardLayout.RowLayouts[rowIndex].ValueRect, element.CardLayout.Rows[rowIndex].Value));
                            }
                        }
                    }
                }

                return result;
            }
        }

        private sealed class RecordingPopupMenuRenderer : IPopupMenuRenderer
        {
            private readonly IWorkbenchFrameContext _frameContext;
            private readonly List<RecordingRenderCommand> _commands;
            private readonly PopupMenuRuntimeState _state = new PopupMenuRuntimeState();

            public RecordingPopupMenuRenderer(IWorkbenchFrameContext frameContext, List<RecordingRenderCommand> commands)
            {
                _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
                _commands = commands;
            }

            public void Reset()
            {
                PopupMenuInteractionController.Reset(_state);
            }

            public void QueueScrollDelta(float delta)
            {
                PopupMenuInteractionController.QueueScrollDelta(_state, delta);
            }

            public bool TryCapturePointerInput(RenderRect menuRect, RenderPoint localMouse)
            {
                var input = RuntimeUiPointerInputAdapter.FromWorkbenchFrameInput(_frameContext.Snapshot, localMouse);
                var capture = PopupMenuInteractionController.TryCapturePointerInput(_state, menuRect, input);
                if (capture.ShouldConsumeInput)
                {
                    _frameContext.ConsumeCurrentInput();
                }

                return capture.Captured;
            }

            public PopupMenuRenderResult Draw(RenderPoint position, RenderSize viewportSize, string headerText, IList<PopupMenuItemModel> items, RenderPoint localMouse, PopupMenuThemePalette theme)
            {
                var result = new PopupMenuRenderResult();
                PopupMenuInteractionController.ResetForMenu(_state, headerText, items ?? new PopupMenuItemModel[0]);
                var layout = PopupMenuLayoutPlanner.BuildLayout(position, viewportSize, items);
                var input = RuntimeUiPointerInputAdapter.FromWorkbenchFrameInput(_frameContext.Snapshot, localMouse);
                var preparedFrame = PopupMenuInteractionController.PrepareFrame(
                    _state,
                    input,
                    layout.MaxScroll,
                    RuntimeUiHitTest.Contains(layout.MenuRect, input.PointerPosition));
                var drawLayout = PopupMenuLayoutPlanner.BuildDrawLayout(position, viewportSize, items, preparedFrame.ScrollOffset, headerText);
                result.MenuRect = drawLayout.MenuRect;

                _commands.Add(new RecordingRenderCommand("popup.menu", drawLayout.MenuRect, headerText));
                _commands.Add(new RecordingRenderCommand("popup.header", drawLayout.HeaderTextRect, headerText));

                var viewportMouse = new RenderPoint(localMouse.X - drawLayout.ViewportRect.X, localMouse.Y - drawLayout.ViewportRect.Y);
                var queuedDownViewportMouse = new RenderPoint(
                    preparedFrame.QueuedPointerDownPosition.X - drawLayout.ViewportRect.X,
                    preparedFrame.QueuedPointerDownPosition.Y - drawLayout.ViewportRect.Y);
                var queuedUpViewportMouse = new RenderPoint(
                    preparedFrame.QueuedPointerUpPosition.X - drawLayout.ViewportRect.X,
                    preparedFrame.QueuedPointerUpPosition.Y - drawLayout.ViewportRect.Y);

                for (var i = 0; i < drawLayout.Items.Count; i++)
                {
                    var itemLayout = drawLayout.Items[i];
                    var item = itemLayout.Item;
                    if (item == null)
                    {
                        continue;
                    }

                    if (item.IsSeparator)
                    {
                        _commands.Add(new RecordingRenderCommand("popup.separator", itemLayout.SeparatorRect, string.Empty));
                        continue;
                    }

                    if (item.IsSectionHeader)
                    {
                        _commands.Add(new RecordingRenderCommand("popup.section", itemLayout.LabelRect, item.Label));
                        continue;
                    }

                    var interaction = PopupMenuInteractionController.EvaluateItemInteraction(
                        _state,
                        input,
                        item.CommandId,
                        item.Enabled,
                        RuntimeUiHitTest.Contains(itemLayout.Bounds, viewportMouse),
                        preparedFrame.HasQueuedPointerDown && preparedFrame.QueuedPointerDownButton == 0 && RuntimeUiHitTest.Contains(itemLayout.Bounds, queuedDownViewportMouse),
                        preparedFrame.HasQueuedPointerUp && preparedFrame.QueuedPointerUpButton == 0 && RuntimeUiHitTest.Contains(itemLayout.Bounds, queuedUpViewportMouse));

                    _commands.Add(new RecordingRenderCommand("popup.item", itemLayout.Bounds, item.Label));
                    if (drawLayout.HasScroll && drawLayout.ScrollChrome != null)
                    {
                        _commands.Add(new RecordingRenderCommand("popup.scroll", drawLayout.ScrollChrome.ChromeRect, string.Empty));
                    }

                    if (interaction.ShouldClose)
                    {
                        result.ShouldClose = true;
                        result.ActivatedCommandId = interaction.ActivatedCommandId;
                    }
                }

                var frameResult = PopupMenuInteractionController.CompleteFrame(_state, input, RuntimeUiHitTest.Contains(layout.MenuRect, input.PointerPosition));
                result.ShouldClose = result.ShouldClose || frameResult.ShouldClose;
                if (frameResult.ShouldClose && input.EventKind == RuntimeUiPointerEventKind.Down)
                {
                    _frameContext.ConsumeCurrentInput();
                }

                return result;
            }

            public RenderRect PredictMenuRect(RenderPoint position, RenderSize viewportSize, IList<PopupMenuItemModel> items)
            {
                return PopupMenuLayoutPlanner.BuildLayout(position, viewportSize, items).MenuRect;
            }
        }

        private sealed class RecordingHoverTooltipRenderer : IHoverTooltipRenderer
        {
            private readonly IWorkbenchFrameContext _frameContext;
            private readonly List<RecordingRenderCommand> _commands;
            private readonly HoverTooltipRuntimeState _state = new HoverTooltipRuntimeState();
            private readonly RecordingHoverTooltipLayoutMeasurer _measurer = new RecordingHoverTooltipLayoutMeasurer();

            private RenderRect _textAnchorRect = new RenderRect(0f, 0f, 0f, 0f);
            private string _text = string.Empty;

            public RecordingHoverTooltipRenderer(IWorkbenchFrameContext frameContext, List<RecordingRenderCommand> commands)
            {
                _frameContext = frameContext ?? NullWorkbenchFrameContext.Instance;
                _commands = commands;
            }

            public void ResetTextTooltip()
            {
                _textAnchorRect = new RenderRect(0f, 0f, 0f, 0f);
                _text = string.Empty;
            }

            public void RegisterTextTooltip(RenderRect anchorRect, string text)
            {
                _textAnchorRect = anchorRect;
                _text = text ?? string.Empty;
            }

            public void DrawTextTooltip(HoverTooltipThemePalette theme)
            {
                if (string.IsNullOrEmpty(_text))
                {
                    return;
                }

                var snapshot = _frameContext.Snapshot;
                var rect = HoverTooltipPlacement.BuildTextRect(
                    _textAnchorRect,
                    snapshot.CurrentMousePosition,
                    snapshot.ViewportSize,
                    Math.Max(240f, _text.Length * 7f),
                    30f);
                _commands.Add(new RecordingRenderCommand("tooltip.text", rect, _text));
            }

            public void ClearRichState()
            {
                HoverTooltipInteractionController.Reset(_state);
            }

            public bool DrawRichTooltip(HoverTooltipRenderModel currentModel, RenderPoint mousePosition, RenderSize viewportSize, bool hasMouse, HoverTooltipThemePalette theme, float tooltipWidth, out HoverTooltipRenderResult result)
            {
                var snapshot = _frameContext.Snapshot;
                var input = RuntimeUiPointerInputAdapter.FromWorkbenchFrameInput(snapshot, mousePosition);
                var plan = HoverTooltipLayoutPlanner.BuildLayout(
                    _state,
                    currentModel,
                    mousePosition,
                    viewportSize,
                    snapshot.ViewportSize,
                    hasMouse,
                    DateTime.UtcNow,
                    tooltipWidth,
                    420f,
                    input.AllowsVisualRefresh,
                    _measurer);

                result = new HoverTooltipRenderResult();
                if (!plan.Visible)
                {
                    result.HiddenReason = plan.HiddenReason;
                    return false;
                }

                _commands.Add(new RecordingRenderCommand("tooltip.box", plan.TooltipRect, string.Empty));
                _commands.Add(new RecordingRenderCommand("tooltip.path", plan.PathRect, plan.Model.QualifiedPath));
                _commands.Add(new RecordingRenderCommand("tooltip.meta", plan.MetaRect, plan.MetaText));
                for (var i = 0; i < plan.PartLayouts.Count; i++)
                {
                    _commands.Add(new RecordingRenderCommand("tooltip.part", plan.PartLayouts[i].Bounds, plan.PartLayouts[i].Part != null ? plan.PartLayouts[i].Part.Text : string.Empty));
                }

                if (!string.IsNullOrEmpty(plan.DetailText))
                {
                    _commands.Add(new RecordingRenderCommand("tooltip.detail", plan.DetailRect, plan.DetailText));
                }

                result.Visible = true;
                result.Model = plan.Model;
                result.HoveredPart = plan.HoveredPart;
                result.TooltipRect = plan.TooltipRect;
                result.ActivatedPart = HoverTooltipInteractionController.HandlePartPointerInput(_state, input, plan.HoveredPart);
                return true;
            }
        }

        private sealed class RecordingPanelLayoutMeasurer : IPanelLayoutMeasurer
        {
            public float MeasureMetadataHeight(float width, PanelMetadataElement element)
            {
                return 20f;
            }

            public float MeasureTextHeight(float width, PanelTextElement element)
            {
                var text = element != null ? element.Value ?? string.Empty : string.Empty;
                return 22f + (text.Length / 48) * 18f;
            }

            public float MeasureActionHeight(float width, PanelActionElement element)
            {
                return 44f;
            }

            public float MeasureCardHeight(float width, PanelCardElement element)
            {
                return 110f;
            }
        }

        private sealed class RecordingHoverTooltipLayoutMeasurer : IHoverTooltipLayoutMeasurer
        {
            public float MeasurePartWidth(EditorHoverContentPart part)
            {
                return Math.Max(6f, (part != null ? (part.Text ?? string.Empty).Length : 0) * 7f);
            }

            public float MeasurePathHeight(string text, float width)
            {
                return string.IsNullOrEmpty(text) ? 0f : 16f;
            }

            public float MeasureMetaHeight(string text, float width)
            {
                return string.IsNullOrEmpty(text) ? 0f : 16f;
            }

            public float MeasureDetailHeight(string text, float width)
            {
                return string.IsNullOrEmpty(text) ? 0f : 36f;
            }

            public float MeasureLineHeight()
            {
                return 18f;
            }
        }

        private sealed class TestWorkbenchFrameContext : IWorkbenchFrameContext
        {
            public WorkbenchFrameInputSnapshot Snapshot { get; private set; }
            public int ConsumedCount { get; private set; }

            public void SetSnapshot(WorkbenchFrameInputSnapshot snapshot)
            {
                Snapshot = snapshot;
            }

            public void ConsumeCurrentInput()
            {
                ConsumedCount++;
            }
        }

        private sealed class RecordingRenderCommand
        {
            public RecordingRenderCommand(string kind, RenderRect bounds, string text)
            {
                Kind = kind;
                Bounds = bounds;
                Text = text ?? string.Empty;
            }

            public string Kind { get; private set; }
            public RenderRect Bounds { get; private set; }
            public string Text { get; private set; }
        }
    }
}
