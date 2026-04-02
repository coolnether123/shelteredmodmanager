using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi
{
    public enum RuntimeUiPointerEventKind
    {
        None = 0,
        Down = 1,
        Up = 2,
        Scroll = 3
    }

    public struct RuntimeUiPointerFrameInput
    {
        public bool HasPointer;
        public RenderPoint PointerPosition;
        public RuntimeUiPointerEventKind EventKind;
        public bool AllowsVisualRefresh;
        public int PointerButton;
        public float WheelScrollDelta;
        public float AnalogScrollDelta;
        public int FrameId;
    }

    public static class RuntimeUiHitTest
    {
        public static bool Contains(RenderRect rect, RenderPoint point)
        {
            return point.X >= rect.X &&
                point.Y >= rect.Y &&
                point.X <= rect.X + rect.Width &&
                point.Y <= rect.Y + rect.Height;
        }
    }
}
