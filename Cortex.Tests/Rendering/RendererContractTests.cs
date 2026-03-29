using System;
using System.Collections.Generic;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Rendering
{
    public abstract class RendererContractTests
    {
        protected abstract IRenderPipeline CreateRenderPipeline();

        [Fact]
        public void RenderPipeline_ProvidesRequiredRenderers()
        {
            RunInUnityContext(delegate
            {
                using (var scope = CreatePipelineScope())
                {
                    Assert.NotNull(scope.Pipeline);
                    Assert.NotNull(scope.Pipeline.WorkbenchRenderer);
                    Assert.NotNull(scope.Pipeline.PanelRenderer);
                    Assert.NotNull(scope.Pipeline.OverlayRendererFactory);
                }
            });
        }

        [Fact]
        public void WorkbenchRenderer_ExposesStableMetadata()
        {
            RunInUnityContext(delegate
            {
                using (var scope = CreatePipelineScope())
                {
                    Assert.False(string.IsNullOrEmpty(scope.Pipeline.WorkbenchRenderer.RendererId));
                    Assert.False(string.IsNullOrEmpty(scope.Pipeline.WorkbenchRenderer.DisplayName));
                    Assert.NotNull(scope.Pipeline.WorkbenchRenderer.Capabilities);
                }
            });
        }

        [Fact]
        public void OverlayRendererFactory_CreatesDistinctOverlayInstances()
        {
            RunInUnityContext(delegate
            {
                using (var scope = CreatePipelineScope())
                {
                    var popupA = scope.Pipeline.OverlayRendererFactory.CreatePopupMenuRenderer();
                    var popupB = scope.Pipeline.OverlayRendererFactory.CreatePopupMenuRenderer();
                    var hoverA = scope.Pipeline.OverlayRendererFactory.CreateHoverTooltipRenderer();
                    var hoverB = scope.Pipeline.OverlayRendererFactory.CreateHoverTooltipRenderer();

                    Assert.NotNull(popupA);
                    Assert.NotNull(popupB);
                    Assert.NotNull(hoverA);
                    Assert.NotNull(hoverB);
                    Assert.NotSame(popupA, popupB);
                    Assert.NotSame(hoverA, hoverB);
                }
            });
        }

        [Fact]
        public void PopupMenuRenderer_PredictMenuRect_StaysWithinViewport()
        {
            RunInUnityContext(delegate
            {
                using (var scope = CreatePipelineScope())
                {
                    var renderer = scope.Pipeline.OverlayRendererFactory.CreatePopupMenuRenderer();
                    var viewport = new RenderSize(400f, 300f);
                    var rect = renderer.PredictMenuRect(
                        new RenderPoint(390f, 290f),
                        viewport,
                        BuildPopupItems(10));

                    Assert.True(rect.Width > 0f);
                    Assert.True(rect.Height > 0f);
                    Assert.True(rect.X >= 0f);
                    Assert.True(rect.Y >= 0f);
                    Assert.True(rect.X + rect.Width <= viewport.Width);
                    Assert.True(rect.Y + rect.Height <= viewport.Height);
                }
            });
        }

        [Fact]
        public void PopupMenuRenderer_PredictMenuRect_GrowsWithContentUntilViewportCap()
        {
            RunInUnityContext(delegate
            {
                using (var scope = CreatePipelineScope())
                {
                    var renderer = scope.Pipeline.OverlayRendererFactory.CreatePopupMenuRenderer();
                    var viewport = new RenderSize(420f, 180f);
                    var shortRect = renderer.PredictMenuRect(
                        new RenderPoint(24f, 24f),
                        viewport,
                        BuildPopupItems(2));
                    var longRect = renderer.PredictMenuRect(
                        new RenderPoint(24f, 24f),
                        viewport,
                        BuildPopupItems(30));

                    Assert.True(longRect.Height >= shortRect.Height);
                    Assert.True(longRect.Height <= viewport.Height);
                }
            });
        }

        [Fact]
        public void PopupMenuRenderer_PredictMenuRect_IsStableForEquivalentInput()
        {
            RunInUnityContext(delegate
            {
                using (var scope = CreatePipelineScope())
                {
                    var renderer = scope.Pipeline.OverlayRendererFactory.CreatePopupMenuRenderer();
                    var items = BuildPopupItems(8);
                    var first = renderer.PredictMenuRect(new RenderPoint(32f, 18f), new RenderSize(500f, 320f), items);
                    var second = renderer.PredictMenuRect(new RenderPoint(32f, 18f), new RenderSize(500f, 320f), items);

                    Assert.Equal(first.X, second.X);
                    Assert.Equal(first.Y, second.Y);
                    Assert.Equal(first.Width, second.Width);
                    Assert.Equal(first.Height, second.Height);
                }
            });
        }

        [Fact]
        public void PopupMenuRenderer_PredictMenuRect_HandlesEmptyItemSets()
        {
            RunInUnityContext(delegate
            {
                using (var scope = CreatePipelineScope())
                {
                    var renderer = scope.Pipeline.OverlayRendererFactory.CreatePopupMenuRenderer();
                    var rect = renderer.PredictMenuRect(
                        new RenderPoint(12f, 8f),
                        new RenderSize(320f, 240f),
                        new PopupMenuItemModel[0]);

                    Assert.True(rect.Width > 0f);
                    Assert.True(rect.Height > 0f);
                }
            });
        }

        protected virtual void RunInUnityContext(Action action)
        {
            UnityManagedAssemblyResolver.Run(action);
        }

        private PipelineScope CreatePipelineScope()
        {
            return new PipelineScope(CreateRenderPipeline());
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
                    ShortcutText = i % 3 == 0 ? "Ctrl+" + i : string.Empty
                });
            }

            return items;
        }

        private sealed class PipelineScope : IDisposable
        {
            private readonly IDisposable _disposable;

            public PipelineScope(IRenderPipeline pipeline)
            {
                Pipeline = pipeline;
                _disposable = pipeline as IDisposable;
            }

            public IRenderPipeline Pipeline { get; private set; }

            public void Dispose()
            {
                if (_disposable != null)
                {
                    _disposable.Dispose();
                }
            }
        }
    }
}
