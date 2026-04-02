using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi.PopupMenus
{
    public sealed class PopupMenuRuntimeState
    {
        public float ScrollOffset;
        public string MenuKey = string.Empty;
        public float PendingScrollDelta;
        public string PressedCommandId = string.Empty;
        public int LastAnalogScrollFrameId = -1;
        public bool HasQueuedPointerDown;
        public bool HasQueuedPointerUp;
        public bool QueuedOutsideClickClose;
        public int QueuedPointerDownButton = -1;
        public int QueuedPointerUpButton = -1;
        public RenderPoint QueuedPointerDownPosition = RenderPoint.Zero;
        public RenderPoint QueuedPointerUpPosition = RenderPoint.Zero;
    }

    public struct PopupMenuCaptureResult
    {
        public bool Captured;
        public bool ShouldConsumeInput;
    }

    public struct PopupMenuPreparedFrame
    {
        public float ScrollOffset;
        public float MaxScroll;
        public bool HasScroll;
        public bool HasQueuedPointerDown;
        public bool HasQueuedPointerUp;
        public int QueuedPointerDownButton;
        public int QueuedPointerUpButton;
        public RenderPoint QueuedPointerDownPosition;
        public RenderPoint QueuedPointerUpPosition;
    }

    public struct PopupMenuItemInteractionResult
    {
        public bool IsPressedVisual;
        public bool ShouldClose;
        public string ActivatedCommandId;
    }

    public struct PopupMenuFrameResult
    {
        public bool ShouldClose;
    }
}
