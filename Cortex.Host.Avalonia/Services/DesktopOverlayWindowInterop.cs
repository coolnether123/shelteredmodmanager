using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Controls.Platform;
using Cortex.Bridge;

namespace Cortex.Host.Avalonia.Services
{
    internal static class DesktopOverlayWindowInterop
    {
        private const int GwlExStyle = -20;
        private const int GwlWndProc = -4;
        private const uint WmNcHitTest = 0x0084;
        private const int WsExToolWindow = 0x00000080;
        private const int WsExTransparent = 0x00000020;
        private const int HtTransparent = -1;
        private const int HtClient = 1;
        private static readonly object SyncRoot = new object();
        private static readonly Dictionary<IntPtr, WindowHitTestState> WindowStates = new Dictionary<IntPtr, WindowHitTestState>();
        private static readonly WindowProcDelegate SharedWindowProc = CustomWindowProc;

        public static void ApplyOverlayStyle(Window window, bool interactive, IList<OverlayHitRegion> hitRegions)
        {
            if (window == null)
            {
                return;
            }

            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null || platformHandle.Handle == IntPtr.Zero)
            {
                return;
            }

            RegisterWindowHitTestState(platformHandle.Handle, interactive, hitRegions);
            var extendedStyle = GetWindowLong(platformHandle.Handle, GwlExStyle);
            extendedStyle |= WsExToolWindow;
            if (interactive)
            {
                extendedStyle &= ~WsExTransparent;
            }
            else
            {
                extendedStyle |= WsExTransparent;
            }

            SetWindowLong(platformHandle.Handle, GwlExStyle, extendedStyle);
        }

        public static void Detach(Window window)
        {
            if (window == null)
            {
                return;
            }

            var platformHandle = window.TryGetPlatformHandle();
            if (platformHandle == null || platformHandle.Handle == IntPtr.Zero)
            {
                return;
            }

            lock (SyncRoot)
            {
                WindowHitTestState state;
                if (!WindowStates.TryGetValue(platformHandle.Handle, out state))
                {
                    return;
                }

                SetWindowLongPtr(platformHandle.Handle, GwlWndProc, state.PreviousWndProc);
                WindowStates.Remove(platformHandle.Handle);
            }
        }

        private static void RegisterWindowHitTestState(IntPtr handle, bool interactive, IList<OverlayHitRegion> hitRegions)
        {
            lock (SyncRoot)
            {
                WindowHitTestState state;
                if (!WindowStates.TryGetValue(handle, out state))
                {
                    state = new WindowHitTestState
                    {
                        PreviousWndProc = SetWindowLongPtr(handle, GwlWndProc, Marshal.GetFunctionPointerForDelegate(SharedWindowProc))
                    };
                    WindowStates[handle] = state;
                }

                state.Interactive = interactive;
                state.Regions = CopyInteractiveRegions(hitRegions);
            }
        }

        private static IntPtr CustomWindowProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam)
        {
            WindowHitTestState state;
            lock (SyncRoot)
            {
                WindowStates.TryGetValue(handle, out state);
            }

            if (state == null)
            {
                return DefWindowProc(handle, message, wParam, lParam);
            }

            if (message == WmNcHitTest)
            {
                if (!state.Interactive)
                {
                    return new IntPtr(HtTransparent);
                }

                if (state.Regions.Count == 0)
                {
                    return new IntPtr(HtClient);
                }

                RECT windowRect;
                if (!GetWindowRect(handle, out windowRect))
                {
                    return new IntPtr(HtClient);
                }

                var screenX = GetSignedLowWord(lParam);
                var screenY = GetSignedHighWord(lParam);
                var localX = screenX - windowRect.Left;
                var localY = screenY - windowRect.Top;

                for (var i = 0; i < state.Regions.Count; i++)
                {
                    if (state.Regions[i].Contains(localX, localY))
                    {
                        return new IntPtr(HtClient);
                    }
                }

                return new IntPtr(HtTransparent);
            }

            return CallWindowProc(state.PreviousWndProc, handle, message, wParam, lParam);
        }

        private static List<InteractiveRegion> CopyInteractiveRegions(IList<OverlayHitRegion> hitRegions)
        {
            var regions = new List<InteractiveRegion>();
            if (hitRegions == null)
            {
                return regions;
            }

            for (var i = 0; i < hitRegions.Count; i++)
            {
                var region = hitRegions[i];
                if (region == null || !region.Interactive || region.Bounds == null)
                {
                    continue;
                }

                regions.Add(new InteractiveRegion(
                    region.Bounds.X,
                    region.Bounds.Y,
                    region.Bounds.Width,
                    region.Bounds.Height));
            }

            return regions;
        }

        private static int GetSignedLowWord(IntPtr value)
        {
            return unchecked((short)((long)value & 0xFFFF));
        }

        private static int GetSignedHighWord(IntPtr value)
        {
            return unchecked((short)(((long)value >> 16) & 0xFFFF));
        }

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong(IntPtr handle, int index);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong(IntPtr handle, int index, int newLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr(IntPtr handle, int index, IntPtr newLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr previousWindowProc, IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr handle, out RECT rect);

        private delegate IntPtr WindowProcDelegate(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

        private sealed class WindowHitTestState
        {
            public IntPtr PreviousWndProc;
            public bool Interactive;
            public List<InteractiveRegion> Regions = new List<InteractiveRegion>();
        }

        private sealed class InteractiveRegion
        {
            private readonly double _x;
            private readonly double _y;
            private readonly double _width;
            private readonly double _height;

            public InteractiveRegion(double x, double y, double width, double height)
            {
                _x = x;
                _y = y;
                _width = width;
                _height = height;
            }

            public bool Contains(int x, int y)
            {
                return x >= _x &&
                    y >= _y &&
                    x < (_x + _width) &&
                    y < (_y + _height);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
    }
}
