using Cortex.Rendering;
using Cortex.Rendering.Models;

namespace Cortex.Rendering.RuntimeUi
{
    public static class RuntimeUiPointerInputAdapter
    {
        public static RuntimeUiPointerFrameInput FromWorkbenchFrameInput(WorkbenchFrameInputSnapshot snapshot, RenderPoint pointerPosition)
        {
            var input = new RuntimeUiPointerFrameInput();
            input.HasPointer = snapshot.ViewportSize.Width > 0f && snapshot.ViewportSize.Height > 0f;
            input.PointerPosition = pointerPosition;
            input.PointerButton = snapshot.CurrentMouseButton;
            input.WheelScrollDelta = snapshot.WheelScrollDelta;
            input.AnalogScrollDelta = snapshot.AnalogScrollDelta;
            input.FrameId = snapshot.FrameId;
            input.AllowsVisualRefresh = snapshot.AllowsVisualRefresh;
            input.EventKind = MapEventKind(snapshot.CurrentEventKind);
            return input;
        }

        private static RuntimeUiPointerEventKind MapEventKind(WorkbenchInputEventKind eventKind)
        {
            switch (eventKind)
            {
                case WorkbenchInputEventKind.MouseDown:
                    return RuntimeUiPointerEventKind.Down;
                case WorkbenchInputEventKind.MouseUp:
                    return RuntimeUiPointerEventKind.Up;
                case WorkbenchInputEventKind.ScrollWheel:
                    return RuntimeUiPointerEventKind.Scroll;
                default:
                    return RuntimeUiPointerEventKind.None;
            }
        }
    }
}
