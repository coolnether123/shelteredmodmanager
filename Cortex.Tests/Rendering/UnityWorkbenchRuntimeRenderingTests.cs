using Cortex.Host.Unity.Runtime;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Abstractions;
using Cortex.Rendering;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Rendering.RuntimeUi;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Rendering
{
    public sealed class UnityWorkbenchRuntimeRenderingTests
    {
        [Fact]
        public void Constructor_UsesHostProvidedRuntimeUi()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var runtimeUi = new TestWorkbenchRuntimeUi("renderer.initial", "Initial Renderer", WorkbenchRuntimeUiLayoutMode.IntegratedShellWindow);
                var runtime = new UnityWorkbenchRuntime(null, runtimeUi);

                Assert.NotNull(runtime.RenderPipeline);
                Assert.NotNull(runtime.RenderPipeline.WorkbenchRenderer);
                Assert.NotNull(runtime.RenderPipeline.PanelRenderer);
                Assert.Same(runtimeUi, runtime.RuntimeUi);
                Assert.Same(runtimeUi.RenderPipeline, runtime.RenderPipeline);
                Assert.Same(runtimeUi.RenderPipeline.WorkbenchRenderer, runtime.Renderer);
                Assert.Same(runtimeUi.WorkbenchUiSurface, runtime.RuntimeUi.WorkbenchUiSurface);
                Assert.Same(runtimeUi.FrameContext, runtime.RuntimeUi.FrameContext);
            });
        }

        [Fact]
        public void SwitchRuntimeUi_ReplacesActiveRendererWithoutRecreatingRuntimeState()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var initialRuntimeUi = new TestWorkbenchRuntimeUi("renderer.initial", "Initial Renderer", WorkbenchRuntimeUiLayoutMode.IntegratedShellWindow);
                var replacementRuntimeUi = new TestWorkbenchRuntimeUi("renderer.overlay", "Overlay Renderer", WorkbenchRuntimeUiLayoutMode.OverlayWindows);
                var runtime = new UnityWorkbenchRuntime(null, initialRuntimeUi);
                var switcher = (IWorkbenchRuntimeUiSwitcher)runtime;

                runtime.WorkbenchState.ActiveContainerId = "editor";

                Assert.True(switcher.SwitchRuntimeUi(replacementRuntimeUi));
                Assert.Equal("editor", runtime.WorkbenchState.ActiveContainerId);
                Assert.Same(replacementRuntimeUi, runtime.RuntimeUi);
                Assert.Same(replacementRuntimeUi.RenderPipeline, runtime.RenderPipeline);
                Assert.Equal(WorkbenchRuntimeUiLayoutMode.OverlayWindows, runtime.RuntimeUi.LayoutMode);
                Assert.Equal("renderer.overlay", runtime.RenderPipeline.WorkbenchRenderer.RendererId);
            });
        }

        private sealed class TestWorkbenchRuntimeUi : IWorkbenchRuntimeUi
        {
            private readonly IRenderPipeline _renderPipeline;
            private readonly IWorkbenchUiSurface _workbenchUiSurface = NullWorkbenchUiSurface.Instance;

            public TestWorkbenchRuntimeUi(string rendererId, string displayName, WorkbenchRuntimeUiLayoutMode layoutMode)
            {
                _renderPipeline = new TestRenderPipeline(rendererId, displayName);
                LayoutMode = layoutMode;
            }

            public IRenderPipeline RenderPipeline
            {
                get { return _renderPipeline; }
            }

            public IWorkbenchUiSurface WorkbenchUiSurface
            {
                get { return _workbenchUiSurface; }
            }

            public IWorkbenchFrameContext FrameContext
            {
                get { return NullWorkbenchFrameContext.Instance; }
            }

            public WorkbenchRuntimeUiLayoutMode LayoutMode { get; private set; }
        }

        private sealed class TestRenderPipeline : IRenderPipeline
        {
            private readonly IWorkbenchRenderer _workbenchRenderer;

            public TestRenderPipeline(string rendererId, string displayName)
            {
                _workbenchRenderer = new TestWorkbenchRenderer(rendererId, displayName);
            }

            public IWorkbenchRenderer WorkbenchRenderer
            {
                get { return _workbenchRenderer; }
            }

            public IPanelRenderer PanelRenderer
            {
                get { return new TestPanelRenderer(); }
            }

            public IOverlayRendererFactory OverlayRendererFactory
            {
                get { return new NullOverlayRendererFactory(); }
            }
        }

        private sealed class TestWorkbenchRenderer : IWorkbenchRenderer
        {
            private readonly RendererCapabilitySet _capabilities = new RendererCapabilitySet();
            private readonly string _rendererId;
            private readonly string _displayName;

            public TestWorkbenchRenderer(string rendererId, string displayName)
            {
                _rendererId = rendererId;
                _displayName = displayName;
            }

            public string RendererId
            {
                get { return _rendererId; }
            }

            public string DisplayName
            {
                get { return _displayName; }
            }

            public RendererCapabilitySet Capabilities
            {
                get { return _capabilities; }
            }
        }

        private sealed class TestPanelRenderer : IPanelRenderer
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
            public void ResetTextTooltip() { }
            public void RegisterTextTooltip(RenderRect anchorRect, string text) { }
            public void DrawTextTooltip(HoverTooltipThemePalette theme) { }
            public void ClearRichState() { }

            public bool DrawRichTooltip(HoverTooltipRenderModel currentModel, RenderPoint mousePosition, RenderSize viewportSize, bool hasMouse, HoverTooltipThemePalette theme, float tooltipWidth, out HoverTooltipRenderResult result)
            {
                result = new HoverTooltipRenderResult();
                return false;
            }
        }

        private sealed class NullPopupMenuRenderer : IPopupMenuRenderer
        {
            public void Reset() { }
            public void QueueScrollDelta(float delta) { }
            public bool TryCapturePointerInput(RenderRect menuRect, RenderPoint localMouse) { return false; }

            public PopupMenuRenderResult Draw(RenderPoint position, RenderSize viewportSize, string headerText, System.Collections.Generic.IList<PopupMenuItemModel> items, RenderPoint localMouse, PopupMenuThemePalette theme)
            {
                return new PopupMenuRenderResult();
            }

            public RenderRect PredictMenuRect(RenderPoint position, RenderSize viewportSize, System.Collections.Generic.IList<PopupMenuItemModel> items)
            {
                return new RenderRect(position.X, position.Y, 0f, 0f);
            }
        }
    }
}
