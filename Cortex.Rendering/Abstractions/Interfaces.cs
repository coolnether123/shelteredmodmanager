using Cortex.Rendering.Models;

namespace Cortex.Rendering.Abstractions
{
    public interface IWorkbenchRenderer
    {
        string RendererId { get; }
        string DisplayName { get; }
        RendererCapabilitySet Capabilities { get; }
    }

    public interface ITextMeasurementService
    {
        float MeasureTextWidth(string text);
        float MeasureLineHeight();
    }
}
