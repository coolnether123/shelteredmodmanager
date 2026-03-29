using Cortex.Renderers.Imgui;
using Cortex.Rendering.Abstractions;

namespace Cortex.Tests.Rendering
{
    public sealed class ImguiRendererContractTests : RendererContractTests
    {
        protected override IRenderPipeline CreateRenderPipeline()
        {
            return new ImguiRenderPipeline();
        }
    }
}
