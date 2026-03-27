using Cortex.Host.Unity.Runtime;
using Cortex.Tests.Testing;
using Xunit;

namespace Cortex.Tests.Rendering
{
    public sealed class UnityWorkbenchRuntimeRenderingTests
    {
        [Fact]
        public void Constructor_UsesCentralRenderPipelineForWorkbenchRenderer()
        {
            UnityManagedAssemblyResolver.Run(delegate
            {
                var runtime = new UnityWorkbenchRuntime();

                Assert.NotNull(runtime.RenderPipeline);
                Assert.NotNull(runtime.RenderPipeline.WorkbenchRenderer);
                Assert.NotNull(runtime.RenderPipeline.PanelRenderer);
                Assert.Same(runtime.RenderPipeline.WorkbenchRenderer, runtime.Renderer);
            });
        }
    }
}
