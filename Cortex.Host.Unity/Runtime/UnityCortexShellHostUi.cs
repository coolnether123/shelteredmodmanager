using Cortex.Presentation.Abstractions;
using UnityEngine;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class UnityCortexShellHostUi : ICortexShellHostUi
    {
        public int ScreenWidth
        {
            get { return Screen.width > 0 ? Screen.width : 1920; }
        }

        public int ScreenHeight
        {
            get { return Screen.height > 0 ? Screen.height : 1080; }
        }

        public int HotControl
        {
            get { return GUIUtility.hotControl; }
        }

        public int KeyboardControl
        {
            get { return GUIUtility.keyboardControl; }
        }

        public bool HasCurrentEvent
        {
            get { return Event.current != null; }
        }

        public CortexShellInputEventKind CurrentEventKind
        {
            get { return MapEventKind(Event.current != null ? Event.current.type : EventType.Ignore); }
        }

        public CortexShellInputEventKind CurrentEventRawKind
        {
            get { return MapEventKind(Event.current != null ? Event.current.rawType : EventType.Ignore); }
        }

        public CortexShellInputKey CurrentKey
        {
            get
            {
                if (Event.current == null)
                {
                    return CortexShellInputKey.None;
                }

                return Event.current.keyCode == KeyCode.Escape
                    ? CortexShellInputKey.Escape
                    : CortexShellInputKey.Other;
            }
        }

        public int CurrentMouseButton
        {
            get { return Event.current != null ? Event.current.button : -1; }
        }

        public CortexShellPointerPosition CurrentMousePosition
        {
            get
            {
                if (Event.current == null)
                {
                    return new CortexShellPointerPosition(0f, 0f);
                }

                return new CortexShellPointerPosition(Event.current.mousePosition.x, Event.current.mousePosition.y);
            }
        }

        public CortexShellPointerPosition PointerPosition
        {
            get
            {
                if (Event.current != null)
                {
                    return CurrentMousePosition;
                }

                var screenMouse = Input.mousePosition;
                return new CortexShellPointerPosition(screenMouse.x, ScreenHeight - screenMouse.y);
            }
        }

        public void ConsumeCurrentEvent()
        {
            if (Event.current != null)
            {
                Event.current.Use();
            }
        }

        private static CortexShellInputEventKind MapEventKind(EventType eventType)
        {
            switch (eventType)
            {
                case EventType.MouseDown:
                    return CortexShellInputEventKind.MouseDown;
                case EventType.MouseUp:
                    return CortexShellInputEventKind.MouseUp;
                case EventType.MouseDrag:
                    return CortexShellInputEventKind.MouseDrag;
                case EventType.KeyDown:
                    return CortexShellInputEventKind.KeyDown;
                case EventType.Repaint:
                    return CortexShellInputEventKind.Repaint;
                case EventType.ScrollWheel:
                    return CortexShellInputEventKind.ScrollWheel;
                case EventType.Ignore:
                    return CortexShellInputEventKind.None;
                default:
                    return CortexShellInputEventKind.Other;
            }
        }
    }
}
