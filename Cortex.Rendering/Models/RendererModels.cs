namespace Cortex.Rendering.Models
{
    public sealed class RendererCapabilitySet
    {
        public bool SupportsRetainedLayout;
        public bool SupportsClipping;
        public bool SupportsLayeredOverlays;
        public bool SupportsTextMeasurementCache;
        public bool SupportsIme;
        public bool SupportsVirtualization;
        public bool SupportsDragPreview;
        public bool SupportsCursorControl;
        public bool SupportsRichText;
        public bool SupportsPointerCapture;
        public bool SupportsKeyboardRouting;
        public bool SupportsScrollableSurfaces;
        public bool SupportsCaretRendering;
        public bool SupportsDenseCollections;
        public string CapabilityVersion;

        public RendererCapabilitySet()
        {
            CapabilityVersion = "1";
        }
    }
}
