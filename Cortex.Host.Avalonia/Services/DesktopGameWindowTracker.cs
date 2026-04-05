using System;
using System.Runtime.InteropServices;

namespace Cortex.Host.Avalonia.Services
{
    internal sealed class DesktopGameWindowTracker
    {
        public bool TryGetWindowBounds(int processId, out PixelBounds bounds)
        {
            var state = new WindowSearchState { ProcessId = processId };
            var handle = GCHandle.Alloc(state);
            try
            {
                EnumWindows(FindWindow, GCHandle.ToIntPtr(handle));
            }
            finally
            {
                if (handle.IsAllocated)
                {
                    handle.Free();
                }
            }

            if (state.WindowHandle == IntPtr.Zero)
            {
                bounds = new PixelBounds();
                return false;
            }

            var isMinimized = IsIconic(state.WindowHandle);
            if (isMinimized)
            {
                bounds = new PixelBounds(0, 0, 0, 0, false, true);
                return true;
            }

            RECT clientRect;
            if (!GetClientRect(state.WindowHandle, out clientRect))
            {
                bounds = new PixelBounds();
                return false;
            }

            POINT clientOrigin;
            clientOrigin.X = clientRect.Left;
            clientOrigin.Y = clientRect.Top;
            if (!ClientToScreen(state.WindowHandle, ref clientOrigin))
            {
                bounds = new PixelBounds();
                return false;
            }

            bounds = new PixelBounds(
                clientOrigin.X,
                clientOrigin.Y,
                Math.Max(0, clientRect.Right - clientRect.Left),
                Math.Max(0, clientRect.Bottom - clientRect.Top),
                true,
                false);
            return true;
        }

        private static bool FindWindow(IntPtr handle, IntPtr statePtr)
        {
            var gcHandle = GCHandle.FromIntPtr(statePtr);
            var state = gcHandle.Target as WindowSearchState;
            if (state == null)
            {
                return false;
            }

            int processId;
            GetWindowThreadProcessId(handle, out processId);
            if (processId == state.ProcessId && (IsWindowVisible(handle) || IsIconic(handle)))
            {
                state.WindowHandle = handle;
                return false;
            }

            return true;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out int processId);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool ClientToScreen(IntPtr hWnd, ref POINT point);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        private sealed class WindowSearchState
        {
            public int ProcessId;
            public IntPtr WindowHandle;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        internal struct PixelBounds
        {
            public PixelBounds(int x, int y, int width, int height, bool isVisible, bool isMinimized)
            {
                X = x;
                Y = y;
                Width = width;
                Height = height;
                IsVisible = isVisible;
                IsMinimized = isMinimized;
            }

            public int X;
            public int Y;
            public int Width;
            public int Height;
            public bool IsVisible;
            public bool IsMinimized;
        }
    }
}
