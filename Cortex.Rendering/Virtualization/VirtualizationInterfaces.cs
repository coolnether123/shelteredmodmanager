namespace Cortex.Rendering.Virtualization
{
    public interface IVirtualizationController
    {
        VirtualizationRange CalculateVisibleRange(VirtualizationMetrics metrics);
    }
}
