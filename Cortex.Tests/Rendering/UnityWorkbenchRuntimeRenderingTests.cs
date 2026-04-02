using Cortex.Host.Unity.Runtime;
using Cortex.Plugins.Abstractions;
using Cortex.Rendering.Abstractions;
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
                var runtimeUi = new TestWorkbenchRuntimeUi();
                var runtime = new UnityWorkbenchRuntime(null, runtimeUi);

                Assert.NotNull(runtime.RenderPipeline);
                Assert.NotNull(runtime.RenderPipeline.WorkbenchRenderer);
                Assert.NotNull(runtime.RenderPipeline.PanelRenderer);
                Assert.Same(runtimeUi, runtime.RuntimeUi);
                Assert.Same(runtime.RenderPipeline.WorkbenchRenderer, runtime.Renderer);
                Assert.Same(runtimeUi.RenderPipeline, runtime.RenderPipeline);
                Assert.Same(runtimeUi.WorkbenchUiSurface, runtime.RuntimeUi.WorkbenchUiSurface);
                Assert.Same(runtimeUi.FrameContext, runtime.RuntimeUi.FrameContext);
            });
        }

        private sealed class TestWorkbenchRuntimeUi : IWorkbenchRuntimeUi
        {
            private readonly IRenderPipeline _renderPipeline = NullWorkbenchRuntimeUi.Instance.RenderPipeline;
            private readonly IWorkbenchUiSurface _workbenchUiSurface = NullWorkbenchUiSurface.Instance;

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
        }
    }
}
