namespace Cortex.Rendering.Virtualization
{
    public sealed class VirtualizationRange
    {
        public int StartIndex;
        public int ItemCount;
    }

    public sealed class VirtualizationMetrics
    {
        public float ViewportSize;
        public float ScrollOffset;
        public float ItemExtent;
        public int TotalItemCount;
    }
}
