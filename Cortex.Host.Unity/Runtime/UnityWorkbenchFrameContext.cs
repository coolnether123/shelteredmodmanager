using Cortex.Rendering;
using Cortex.Rendering.Models;
using UnityEngine;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityWorkbenchFrameContext : IWorkbenchFrameContext
    {
        public WorkbenchFrameInputSnapshot Snapshot
        {
            get
            {
                var currentEvent = Event.current;
                return new WorkbenchFrameInputSnapshot
                {
                    ViewportSize = new RenderSize(Screen.width > 0 ? Screen.width : 1920f, Screen.height > 0 ? Screen.height : 1080f),
                    AllowsVisualRefresh = AllowsVisualRefresh(currentEvent != null ? currentEvent.type : EventType.Ignore),
                    HotControl = GUIUtility.hotControl,
                    KeyboardControl = GUIUtility.keyboardControl,
                    HasCurrentEvent = currentEvent != null,
                    CurrentEventKind = MapEventKind(currentEvent != null ? currentEvent.type : EventType.Ignore),
                    CurrentRawEventKind = MapEventKind(currentEvent != null ? currentEvent.rawType : EventType.Ignore),
                    CurrentKey = MapKey(currentEvent != null ? currentEvent.keyCode : KeyCode.None),
                    CurrentMouseButton = currentEvent != null ? currentEvent.button : -1,
                    WheelScrollDelta = currentEvent != null && currentEvent.type == EventType.ScrollWheel ? currentEvent.delta.y : 0f,
                    AnalogScrollDelta = ReadAnalogScrollDelta(),
                    FrameId = Time.frameCount,
                    CurrentMousePosition = GetCurrentMousePosition(),
                    PointerPosition = GetPointerPosition()
                };
            }
        }

        public void ConsumeCurrentInput()
        {
            if (Event.current != null)
            {
                Event.current.Use();
            }
        }

        private static RenderPoint GetCurrentMousePosition()
        {
            if (Event.current == null)
            {
                return RenderPoint.Zero;
            }

            return new RenderPoint(Event.current.mousePosition.x, Event.current.mousePosition.y);
        }

        private static RenderPoint GetPointerPosition()
        {
            if (Event.current != null)
            {
                return GetCurrentMousePosition();
            }

            var screenMouse = Input.mousePosition;
            var screenHeight = Screen.height > 0 ? Screen.height : 1080f;
            return new RenderPoint(screenMouse.x, screenHeight - screenMouse.y);
        }

        private static WorkbenchInputKey MapKey(KeyCode keyCode)
        {
            return keyCode == KeyCode.Escape
                ? WorkbenchInputKey.Escape
                : (keyCode == KeyCode.None ? WorkbenchInputKey.None : WorkbenchInputKey.Other);
        }

        private static WorkbenchInputEventKind MapEventKind(EventType eventType)
        {
            switch (eventType)
            {
                case EventType.MouseDown:
                    return WorkbenchInputEventKind.MouseDown;
                case EventType.MouseUp:
                    return WorkbenchInputEventKind.MouseUp;
                case EventType.MouseDrag:
                    return WorkbenchInputEventKind.MouseDrag;
                case EventType.KeyDown:
                    return WorkbenchInputEventKind.KeyDown;
                case EventType.Repaint:
                    return WorkbenchInputEventKind.Repaint;
                case EventType.ScrollWheel:
                    return WorkbenchInputEventKind.ScrollWheel;
                case EventType.Ignore:
                    return WorkbenchInputEventKind.None;
                default:
                    return WorkbenchInputEventKind.Other;
            }
        }

        private static bool AllowsVisualRefresh(EventType eventType)
        {
            switch (eventType)
            {
                case EventType.Repaint:
                case EventType.MouseMove:
                case EventType.MouseDrag:
                case EventType.MouseDown:
                case EventType.MouseUp:
                case EventType.Ignore:
                    return true;
                default:
                    return false;
            }
        }

        private static float ReadAnalogScrollDelta()
        {
            var smoothedAxis = Input.GetAxis("Mouse ScrollWheel");
            var instantAxis = Input.GetAxisRaw("Mouse ScrollWheel");
            return Mathf.Abs(instantAxis) > Mathf.Abs(smoothedAxis) ? instantAxis : smoothedAxis;
        }
    }
}
