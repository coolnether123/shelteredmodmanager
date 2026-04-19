using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Cortex.Renderers.DearImgui.Native
{
    internal static unsafe class DearImguiNative
    {
        private static readonly object ExportSyncRoot = new object();
        private static readonly Dictionary<string, Delegate> ExportCache = new Dictionary<string, Delegate>(StringComparer.Ordinal);

        [StructLayout(LayoutKind.Sequential)]
        public struct ImVec2
        {
            public float X;
            public float Y;

            public ImVec2(float x, float y)
            {
                X = x;
                Y = y;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImVec4
        {
            public float X;
            public float Y;
            public float Z;
            public float W;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImGuiIO
        {
            public int ConfigFlags;
            public int BackendFlags;
            public ImVec2 DisplaySize;
            public float DeltaTime;
            public float IniSavingRate;
            public IntPtr IniFilename;
            public IntPtr LogFilename;
            public float MouseDoubleClickTime;
            public float MouseDoubleClickMaxDist;
            public float MouseDragThreshold;
            public float KeyRepeatDelay;
            public float KeyRepeatRate;
            public IntPtr UserData;
            public IntPtr Fonts;
            public float FontGlobalScale;
            [MarshalAs(UnmanagedType.I1)] public bool FontAllowUserScaling;
            public IntPtr FontDefault;
            public ImVec2 DisplayFramebufferScale;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImDrawVert
        {
            public ImVec2 Pos;
            public ImVec2 Uv;
            public uint Col;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImVector
        {
            public int Size;
            public int Capacity;
            public IntPtr Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImDrawCmd
        {
            public ImVec4 ClipRect;
            public IntPtr TextureId;
            public uint VtxOffset;
            public uint IdxOffset;
            public uint ElemCount;
            public IntPtr UserCallback;
            public IntPtr UserCallbackData;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImDrawList
        {
            public ImVector CmdBuffer;
            public ImVector IdxBuffer;
            public ImVector VtxBuffer;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ImDrawData
        {
            [MarshalAs(UnmanagedType.I1)] public bool Valid;
            public int CmdListsCount;
            public int TotalIdxCount;
            public int TotalVtxCount;
            public IntPtr CmdLists;
            public ImVec2 DisplayPos;
            public ImVec2 DisplaySize;
            public ImVec2 FramebufferScale;
        }

        public enum ImGuiCond
        {
            None = 0,
            Always = 1
        }

        [Flags]
        public enum ImGuiWindowFlags
        {
            None = 0,
            NoTitleBar = 1 << 0,
            NoResize = 1 << 1,
            NoScrollbar = 1 << 3,
            NoCollapse = 1 << 5,
            NoSavedSettings = 1 << 8,
            MenuBar = 1 << 10
        }

        public enum ImGuiSelectableFlags
        {
            None = 0
        }

        public enum ImGuiInputTextFlags
        {
            None = 0
        }

        public enum ImGuiCol
        {
            Text = 0,
            ChildBg = 7,
            Border = 8,
            Button = 21,
            ButtonHovered = 22,
            ButtonActive = 23,
            Header = 24,
            HeaderHovered = 25,
            HeaderActive = 26
        }

        public enum ImGuiMouseButton
        {
            Left = 0,
            Right = 1,
            Middle = 2
        }

        public enum ImGuiBackendFlags
        {
            RendererHasVtxOffset = 1 << 3
        }

        public enum ImGuiKey
        {
            None = 0,
            Tab = 512,
            LeftArrow,
            RightArrow,
            UpArrow,
            DownArrow,
            PageUp,
            PageDown,
            Home,
            End,
            Insert,
            Delete,
            Backspace,
            Space,
            Enter,
            Escape,
            LeftCtrl,
            LeftShift,
            LeftAlt,
            LeftSuper,
            RightCtrl,
            RightShift,
            RightAlt,
            RightSuper,
            Menu,
            Alpha0,
            Alpha1,
            Alpha2,
            Alpha3,
            Alpha4,
            Alpha5,
            Alpha6,
            Alpha7,
            Alpha8,
            Alpha9,
            A,
            B,
            C,
            D,
            E,
            F,
            G,
            H,
            I,
            J,
            K,
            L,
            M,
            N,
            O,
            P,
            Q,
            R,
            S,
            T,
            U,
            V,
            W,
            X,
            Y,
            Z,
            F1,
            F2,
            F3,
            F4,
            F5,
            F6,
            F7,
            F8,
            F9,
            F10,
            F11,
            F12,
            Apostrophe,
            Comma,
            Minus,
            Period,
            Slash,
            Semicolon,
            Equal,
            LeftBracket,
            Backslash,
            RightBracket,
            GraveAccent,
            CapsLock,
            ScrollLock,
            NumLock,
            PrintScreen,
            Pause,
            Keypad0,
            Keypad1,
            Keypad2,
            Keypad3,
            Keypad4,
            Keypad5,
            Keypad6,
            Keypad7,
            Keypad8,
            Keypad9,
            KeypadDecimal,
            KeypadDivide,
            KeypadMultiply,
            KeypadSubtract,
            KeypadAdd,
            KeypadEnter,
            KeypadEqual,
            GamepadStart,
            GamepadBack,
            GamepadFaceUp,
            GamepadFaceDown,
            GamepadFaceLeft,
            GamepadFaceRight,
            GamepadDpadUp,
            GamepadDpadDown,
            GamepadDpadLeft,
            GamepadDpadRight,
            GamepadL1,
            GamepadR1,
            GamepadL2,
            GamepadR2,
            GamepadL3,
            GamepadR3,
            GamepadLStickUp,
            GamepadLStickDown,
            GamepadLStickLeft,
            GamepadLStickRight,
            GamepadRStickUp,
            GamepadRStickDown,
            GamepadRStickLeft,
            GamepadRStickRight,
            ModCtrl,
            ModShift,
            ModAlt,
            ModSuper
        }

        public static IntPtr igCreateContext(IntPtr sharedFontAtlas)
        {
            return GetExport<igCreateContextDelegate>("igCreateContext")(sharedFontAtlas);
        }

        public static void igDestroyContext(IntPtr context)
        {
            GetExport<igDestroyContextDelegate>("igDestroyContext")(context);
        }

        public static ImGuiIO* igGetIO()
        {
            return GetExport<igGetIODelegate>("igGetIO")();
        }

        public static void igNewFrame()
        {
            GetExport<igNewFrameDelegate>("igNewFrame")();
        }

        public static void igRender()
        {
            GetExport<igRenderDelegate>("igRender")();
        }

        public static ImDrawData* igGetDrawData()
        {
            return GetExport<igGetDrawDataDelegate>("igGetDrawData")();
        }

        public static bool igBegin(string name, IntPtr openPointer, ImGuiWindowFlags flags)
        {
            return GetExport<igBeginDelegate>("igBegin")(name, openPointer, flags);
        }

        public static void igEnd()
        {
            GetExport<igEndDelegate>("igEnd")();
        }

        public static bool igBeginChild_Str(string strId, ImVec2 size, bool border, ImGuiWindowFlags flags)
        {
            return GetExport<igBeginChildStrDelegate>("igBeginChild_Str")(strId, size, border, flags);
        }

        public static void igEndChild()
        {
            GetExport<igEndChildDelegate>("igEndChild")();
        }

        public static void igSetNextWindowPos(ImVec2 position, ImGuiCond condition, ImVec2 pivot)
        {
            GetExport<igSetNextWindowPosDelegate>("igSetNextWindowPos")(position, condition, pivot);
        }

        public static void igSetNextWindowSize(ImVec2 size, ImGuiCond condition)
        {
            GetExport<igSetNextWindowSizeDelegate>("igSetNextWindowSize")(size, condition);
        }

        public static bool igBeginMainMenuBar()
        {
            return GetExport<igBeginMainMenuBarDelegate>("igBeginMainMenuBar")();
        }

        public static void igEndMainMenuBar()
        {
            GetExport<igEndMainMenuBarDelegate>("igEndMainMenuBar")();
        }

        public static bool igBeginMenuBar()
        {
            return GetExport<igBeginMenuBarDelegate>("igBeginMenuBar")();
        }

        public static void igEndMenuBar()
        {
            GetExport<igEndMenuBarDelegate>("igEndMenuBar")();
        }

        public static bool igBeginMenu(string label, bool enabled)
        {
            return GetExport<igBeginMenuDelegate>("igBeginMenu")(label, enabled);
        }

        public static void igEndMenu()
        {
            GetExport<igEndMenuDelegate>("igEndMenu")();
        }

        public static bool igMenuItem_Bool(string label, string shortcut, bool selected, bool enabled)
        {
            return GetExport<igMenuItemBoolDelegate>("igMenuItem_Bool")(label, shortcut, selected, enabled);
        }

        public static bool igButton(string label, ImVec2 size)
        {
            return GetExport<igButtonDelegate>("igButton")(label, size);
        }

        public static bool igCheckbox(string label, ref bool value)
        {
            return GetExport<igCheckboxDelegate>("igCheckbox")(label, ref value);
        }

        public static bool igSelectable_Bool(string label, bool selected, ImGuiSelectableFlags flags, ImVec2 size)
        {
            return GetExport<igSelectableBoolDelegate>("igSelectable_Bool")(label, selected, flags, size);
        }

        public static bool igInputText(string label, byte[] buffer, uint bufferSize, ImGuiInputTextFlags flags, IntPtr callback, IntPtr userData)
        {
            return GetExport<igInputTextDelegate>("igInputText")(label, buffer, bufferSize, flags, callback, userData);
        }

        public static void igTextUnformatted(string text, IntPtr textEnd)
        {
            GetExport<igTextUnformattedDelegate>("igTextUnformatted")(text, textEnd);
        }

        public static void igTextWrapped(string fmt)
        {
            GetExport<igTextWrappedDelegate>("igTextWrapped")(fmt);
        }

        public static void igSeparator()
        {
            GetExport<igSeparatorDelegate>("igSeparator")();
        }

        public static void igSameLine(float offsetFromStartX, float spacing)
        {
            GetExport<igSameLineDelegate>("igSameLine")(offsetFromStartX, spacing);
        }

        public static void igSpacing()
        {
            GetExport<igSpacingDelegate>("igSpacing")();
        }

        public static void igIndent(float indentW)
        {
            GetExport<igIndentDelegate>("igIndent")(indentW);
        }

        public static void igUnindent(float indentW)
        {
            GetExport<igUnindentDelegate>("igUnindent")(indentW);
        }

        public static void igSetNextItemWidth(float itemWidth)
        {
            GetExport<igSetNextItemWidthDelegate>("igSetNextItemWidth")(itemWidth);
        }

        public static void igPushStyleColor_Vec4(ImGuiCol idx, ImVec4 color)
        {
            GetExport<igPushStyleColorVec4Delegate>("igPushStyleColor_Vec4")(idx, color);
        }

        public static void igPopStyleColor(int count)
        {
            GetExport<igPopStyleColorDelegate>("igPopStyleColor")(count);
        }

        public static void ImFontAtlas_GetTexDataAsRGBA32(IntPtr atlas, out IntPtr pixels, out int width, out int height, out int bytesPerPixel)
        {
            GetExport<imFontAtlasGetTexDataAsRGBA32Delegate>("ImFontAtlas_GetTexDataAsRGBA32")(atlas, out pixels, out width, out height, out bytesPerPixel);
        }

        public static void ImFontAtlas_SetTexID(IntPtr atlas, IntPtr textureId)
        {
            GetExport<imFontAtlasSetTexIDDelegate>("ImFontAtlas_SetTexID")(atlas, textureId);
        }

        public static void ImGuiIO_AddInputCharacterUTF16(ImGuiIO* io, ushort character)
        {
            GetExport<imGuiIOAddInputCharacterUTF16Delegate>("ImGuiIO_AddInputCharacterUTF16")(new IntPtr(io), character);
        }

        public static void ImGuiIO_AddKeyEvent(ImGuiIO* io, ImGuiKey key, bool down)
        {
            GetExport<imGuiIOAddKeyEventDelegate>("ImGuiIO_AddKeyEvent")(new IntPtr(io), key, down);
        }

        public static void ImGuiIO_AddMousePosEvent(ImGuiIO* io, float x, float y)
        {
            GetExport<imGuiIOAddMousePosEventDelegate>("ImGuiIO_AddMousePosEvent")(new IntPtr(io), x, y);
        }

        public static void ImGuiIO_AddMouseButtonEvent(ImGuiIO* io, ImGuiMouseButton button, bool down)
        {
            GetExport<imGuiIOAddMouseButtonEventDelegate>("ImGuiIO_AddMouseButtonEvent")(new IntPtr(io), button, down);
        }

        public static void ImGuiIO_AddMouseWheelEvent(ImGuiIO* io, float wheelX, float wheelY)
        {
            GetExport<imGuiIOAddMouseWheelEventDelegate>("ImGuiIO_AddMouseWheelEvent")(new IntPtr(io), wheelX, wheelY);
        }

        private static TDelegate GetExport<TDelegate>(string exportName) where TDelegate : class
        {
            lock (ExportSyncRoot)
            {
                Delegate export;
                if (!ExportCache.TryGetValue(exportName, out export))
                {
                    export = (Delegate)Marshal.GetDelegateForFunctionPointer(
                        DearImguiNativeLoader.GetRequiredExport(exportName),
                        typeof(TDelegate));
                    ExportCache[exportName] = export;
                }

                var typedExport = export as TDelegate;
                if (typedExport == null)
                {
                    throw new InvalidOperationException("Resolved export '" + exportName + "' does not match delegate type " + typeof(TDelegate).FullName + ".");
                }

                return typedExport;
            }
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr igCreateContextDelegate(IntPtr sharedFontAtlas);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igDestroyContextDelegate(IntPtr context);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ImGuiIO* igGetIODelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igNewFrameDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igRenderDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate ImDrawData* igGetDrawDataDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igBeginDelegate([MarshalAs(UnmanagedType.LPStr)] string name, IntPtr openPointer, ImGuiWindowFlags flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igEndDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igBeginChildStrDelegate([MarshalAs(UnmanagedType.LPStr)] string strId, ImVec2 size, [MarshalAs(UnmanagedType.I1)] bool border, ImGuiWindowFlags flags);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igEndChildDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igSetNextWindowPosDelegate(ImVec2 position, ImGuiCond condition, ImVec2 pivot);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igSetNextWindowSizeDelegate(ImVec2 size, ImGuiCond condition);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igBeginMainMenuBarDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igEndMainMenuBarDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igBeginMenuBarDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igEndMenuBarDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igBeginMenuDelegate([MarshalAs(UnmanagedType.LPStr)] string label, [MarshalAs(UnmanagedType.I1)] bool enabled);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igEndMenuDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igMenuItemBoolDelegate([MarshalAs(UnmanagedType.LPStr)] string label, [MarshalAs(UnmanagedType.LPStr)] string shortcut, [MarshalAs(UnmanagedType.I1)] bool selected, [MarshalAs(UnmanagedType.I1)] bool enabled);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igButtonDelegate([MarshalAs(UnmanagedType.LPStr)] string label, ImVec2 size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igCheckboxDelegate([MarshalAs(UnmanagedType.LPStr)] string label, [MarshalAs(UnmanagedType.I1)] ref bool value);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igSelectableBoolDelegate([MarshalAs(UnmanagedType.LPStr)] string label, [MarshalAs(UnmanagedType.I1)] bool selected, ImGuiSelectableFlags flags, ImVec2 size);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        [return: MarshalAs(UnmanagedType.I1)]
        private delegate bool igInputTextDelegate([MarshalAs(UnmanagedType.LPStr)] string label, byte[] buffer, uint bufferSize, ImGuiInputTextFlags flags, IntPtr callback, IntPtr userData);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igTextUnformattedDelegate([MarshalAs(UnmanagedType.LPStr)] string text, IntPtr textEnd);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igTextWrappedDelegate([MarshalAs(UnmanagedType.LPStr)] string fmt);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igSeparatorDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igSameLineDelegate(float offsetFromStartX, float spacing);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igSpacingDelegate();

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igIndentDelegate(float indentW);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igUnindentDelegate(float indentW);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igSetNextItemWidthDelegate(float itemWidth);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igPushStyleColorVec4Delegate(ImGuiCol idx, ImVec4 color);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void igPopStyleColorDelegate(int count);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void imFontAtlasGetTexDataAsRGBA32Delegate(IntPtr atlas, out IntPtr pixels, out int width, out int height, out int bytesPerPixel);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void imFontAtlasSetTexIDDelegate(IntPtr atlas, IntPtr textureId);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void imGuiIOAddInputCharacterUTF16Delegate(IntPtr io, ushort character);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void imGuiIOAddKeyEventDelegate(IntPtr io, ImGuiKey key, [MarshalAs(UnmanagedType.I1)] bool down);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void imGuiIOAddMousePosEventDelegate(IntPtr io, float x, float y);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void imGuiIOAddMouseButtonEventDelegate(IntPtr io, ImGuiMouseButton button, [MarshalAs(UnmanagedType.I1)] bool down);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void imGuiIOAddMouseWheelEventDelegate(IntPtr io, float wheelX, float wheelY);
    }
}
