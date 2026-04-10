using System;
using Cortex.Renderers.DearImgui.Native;
using UnityEngine;

namespace Cortex.Renderers.DearImgui
{
    internal static unsafe class DearImguiInputAdapter
    {
        private struct KeyBinding
        {
            public DearImguiNative.ImGuiKey ImGuiKey;
            public KeyCode UnityKey;

            public KeyBinding(DearImguiNative.ImGuiKey imGuiKey, KeyCode unityKey)
            {
                ImGuiKey = imGuiKey;
                UnityKey = unityKey;
            }
        }

        private static readonly KeyBinding[] KeyBindings =
        {
            new KeyBinding(DearImguiNative.ImGuiKey.Tab, KeyCode.Tab),
            new KeyBinding(DearImguiNative.ImGuiKey.LeftArrow, KeyCode.LeftArrow),
            new KeyBinding(DearImguiNative.ImGuiKey.RightArrow, KeyCode.RightArrow),
            new KeyBinding(DearImguiNative.ImGuiKey.UpArrow, KeyCode.UpArrow),
            new KeyBinding(DearImguiNative.ImGuiKey.DownArrow, KeyCode.DownArrow),
            new KeyBinding(DearImguiNative.ImGuiKey.PageUp, KeyCode.PageUp),
            new KeyBinding(DearImguiNative.ImGuiKey.PageDown, KeyCode.PageDown),
            new KeyBinding(DearImguiNative.ImGuiKey.Home, KeyCode.Home),
            new KeyBinding(DearImguiNative.ImGuiKey.End, KeyCode.End),
            new KeyBinding(DearImguiNative.ImGuiKey.Insert, KeyCode.Insert),
            new KeyBinding(DearImguiNative.ImGuiKey.Delete, KeyCode.Delete),
            new KeyBinding(DearImguiNative.ImGuiKey.Backspace, KeyCode.Backspace),
            new KeyBinding(DearImguiNative.ImGuiKey.Space, KeyCode.Space),
            new KeyBinding(DearImguiNative.ImGuiKey.Enter, KeyCode.Return),
            new KeyBinding(DearImguiNative.ImGuiKey.Escape, KeyCode.Escape),
            new KeyBinding(DearImguiNative.ImGuiKey.LeftCtrl, KeyCode.LeftControl),
            new KeyBinding(DearImguiNative.ImGuiKey.RightCtrl, KeyCode.RightControl),
            new KeyBinding(DearImguiNative.ImGuiKey.LeftShift, KeyCode.LeftShift),
            new KeyBinding(DearImguiNative.ImGuiKey.RightShift, KeyCode.RightShift),
            new KeyBinding(DearImguiNative.ImGuiKey.LeftAlt, KeyCode.LeftAlt),
            new KeyBinding(DearImguiNative.ImGuiKey.RightAlt, KeyCode.RightAlt),
            new KeyBinding(DearImguiNative.ImGuiKey.Apostrophe, KeyCode.Quote),
            new KeyBinding(DearImguiNative.ImGuiKey.Comma, KeyCode.Comma),
            new KeyBinding(DearImguiNative.ImGuiKey.Minus, KeyCode.Minus),
            new KeyBinding(DearImguiNative.ImGuiKey.Period, KeyCode.Period),
            new KeyBinding(DearImguiNative.ImGuiKey.Slash, KeyCode.Slash),
            new KeyBinding(DearImguiNative.ImGuiKey.Semicolon, KeyCode.Semicolon),
            new KeyBinding(DearImguiNative.ImGuiKey.Equal, KeyCode.Equals),
            new KeyBinding(DearImguiNative.ImGuiKey.LeftBracket, KeyCode.LeftBracket),
            new KeyBinding(DearImguiNative.ImGuiKey.Backslash, KeyCode.Backslash),
            new KeyBinding(DearImguiNative.ImGuiKey.RightBracket, KeyCode.RightBracket),
            new KeyBinding(DearImguiNative.ImGuiKey.GraveAccent, KeyCode.BackQuote),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha0, KeyCode.Alpha0),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha1, KeyCode.Alpha1),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha2, KeyCode.Alpha2),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha3, KeyCode.Alpha3),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha4, KeyCode.Alpha4),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha5, KeyCode.Alpha5),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha6, KeyCode.Alpha6),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha7, KeyCode.Alpha7),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha8, KeyCode.Alpha8),
            new KeyBinding(DearImguiNative.ImGuiKey.Alpha9, KeyCode.Alpha9),
            new KeyBinding(DearImguiNative.ImGuiKey.A, KeyCode.A),
            new KeyBinding(DearImguiNative.ImGuiKey.B, KeyCode.B),
            new KeyBinding(DearImguiNative.ImGuiKey.C, KeyCode.C),
            new KeyBinding(DearImguiNative.ImGuiKey.D, KeyCode.D),
            new KeyBinding(DearImguiNative.ImGuiKey.E, KeyCode.E),
            new KeyBinding(DearImguiNative.ImGuiKey.F, KeyCode.F),
            new KeyBinding(DearImguiNative.ImGuiKey.G, KeyCode.G),
            new KeyBinding(DearImguiNative.ImGuiKey.H, KeyCode.H),
            new KeyBinding(DearImguiNative.ImGuiKey.I, KeyCode.I),
            new KeyBinding(DearImguiNative.ImGuiKey.J, KeyCode.J),
            new KeyBinding(DearImguiNative.ImGuiKey.K, KeyCode.K),
            new KeyBinding(DearImguiNative.ImGuiKey.L, KeyCode.L),
            new KeyBinding(DearImguiNative.ImGuiKey.M, KeyCode.M),
            new KeyBinding(DearImguiNative.ImGuiKey.N, KeyCode.N),
            new KeyBinding(DearImguiNative.ImGuiKey.O, KeyCode.O),
            new KeyBinding(DearImguiNative.ImGuiKey.P, KeyCode.P),
            new KeyBinding(DearImguiNative.ImGuiKey.Q, KeyCode.Q),
            new KeyBinding(DearImguiNative.ImGuiKey.R, KeyCode.R),
            new KeyBinding(DearImguiNative.ImGuiKey.S, KeyCode.S),
            new KeyBinding(DearImguiNative.ImGuiKey.T, KeyCode.T),
            new KeyBinding(DearImguiNative.ImGuiKey.U, KeyCode.U),
            new KeyBinding(DearImguiNative.ImGuiKey.V, KeyCode.V),
            new KeyBinding(DearImguiNative.ImGuiKey.W, KeyCode.W),
            new KeyBinding(DearImguiNative.ImGuiKey.X, KeyCode.X),
            new KeyBinding(DearImguiNative.ImGuiKey.Y, KeyCode.Y),
            new KeyBinding(DearImguiNative.ImGuiKey.Z, KeyCode.Z),
            new KeyBinding(DearImguiNative.ImGuiKey.F1, KeyCode.F1),
            new KeyBinding(DearImguiNative.ImGuiKey.F2, KeyCode.F2),
            new KeyBinding(DearImguiNative.ImGuiKey.F3, KeyCode.F3),
            new KeyBinding(DearImguiNative.ImGuiKey.F4, KeyCode.F4),
            new KeyBinding(DearImguiNative.ImGuiKey.F5, KeyCode.F5),
            new KeyBinding(DearImguiNative.ImGuiKey.F6, KeyCode.F6),
            new KeyBinding(DearImguiNative.ImGuiKey.F7, KeyCode.F7),
            new KeyBinding(DearImguiNative.ImGuiKey.F8, KeyCode.F8),
            new KeyBinding(DearImguiNative.ImGuiKey.F9, KeyCode.F9),
            new KeyBinding(DearImguiNative.ImGuiKey.F10, KeyCode.F10),
            new KeyBinding(DearImguiNative.ImGuiKey.F11, KeyCode.F11),
            new KeyBinding(DearImguiNative.ImGuiKey.F12, KeyCode.F12)
        };

        public static void Update(DearImguiNative.ImGuiIO* io)
        {
            if (io == null)
            {
                return;
            }

            var mousePosition = Input.mousePosition;
            DearImguiNative.ImGuiIO_AddMousePosEvent(io, mousePosition.x, Screen.height - mousePosition.y);
            DearImguiNative.ImGuiIO_AddMouseButtonEvent(io, DearImguiNative.ImGuiMouseButton.Left, Input.GetMouseButton(0));
            DearImguiNative.ImGuiIO_AddMouseButtonEvent(io, DearImguiNative.ImGuiMouseButton.Right, Input.GetMouseButton(1));
            DearImguiNative.ImGuiIO_AddMouseButtonEvent(io, DearImguiNative.ImGuiMouseButton.Middle, Input.GetMouseButton(2));

            var wheelDelta = Input.GetAxis("Mouse ScrollWheel");
            if (Math.Abs(wheelDelta) > 0.0001f)
            {
                DearImguiNative.ImGuiIO_AddMouseWheelEvent(io, 0f, wheelDelta);
            }

            for (var i = 0; i < KeyBindings.Length; i++)
            {
                SetKey(io, KeyBindings[i].ImGuiKey, KeyBindings[i].UnityKey);
            }

            SetKey(io, DearImguiNative.ImGuiKey.ModCtrl, Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl));
            SetKey(io, DearImguiNative.ImGuiKey.ModShift, Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));
            SetKey(io, DearImguiNative.ImGuiKey.ModAlt, Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));

            var input = Input.inputString ?? string.Empty;
            for (var i = 0; i < input.Length; i++)
            {
                DearImguiNative.ImGuiIO_AddInputCharacterUTF16(io, input[i]);
            }
        }

        private static void SetKey(DearImguiNative.ImGuiIO* io, DearImguiNative.ImGuiKey key, KeyCode keyCode)
        {
            DearImguiNative.ImGuiIO_AddKeyEvent(io, key, Input.GetKey(keyCode));
        }

        private static void SetKey(DearImguiNative.ImGuiIO* io, DearImguiNative.ImGuiKey key, bool pressed)
        {
            DearImguiNative.ImGuiIO_AddKeyEvent(io, key, pressed);
        }
    }
}
