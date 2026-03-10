using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Aggregates overlay capture requests from game-facing tools such as Cortex.
    /// </summary>
    public sealed class ShelteredOverlayInputCaptureService : IOverlayInputCaptureService
    {
        private sealed class CaptureState
        {
            public bool Mouse;
            public bool Keyboard;
        }

        private readonly object _sync = new object();
        private readonly Dictionary<string, CaptureState> _captures = new Dictionary<string, CaptureState>(StringComparer.OrdinalIgnoreCase);
        private bool _isMouseCaptured;
        private bool _isKeyboardCaptured;

        public bool IsMouseCaptured
        {
            get
            {
                lock (_sync)
                {
                    return _isMouseCaptured;
                }
            }
        }

        public bool IsKeyboardCaptured
        {
            get
            {
                lock (_sync)
                {
                    return _isKeyboardCaptured;
                }
            }
        }

        public void ReportCapture(string ownerId, bool captureMouse, bool captureKeyboard)
        {
            if (string.IsNullOrEmpty(ownerId))
            {
                return;
            }

            bool changed;
            bool isMouseCaptured;
            bool isKeyboardCaptured;

            lock (_sync)
            {
                if (!captureMouse && !captureKeyboard)
                {
                    _captures.Remove(ownerId);
                }
                else
                {
                    CaptureState state;
                    if (!_captures.TryGetValue(ownerId, out state) || state == null)
                    {
                        state = new CaptureState();
                        _captures[ownerId] = state;
                    }

                    state.Mouse = captureMouse;
                    state.Keyboard = captureKeyboard;
                }

                isMouseCaptured = false;
                isKeyboardCaptured = false;

                foreach (var pair in _captures)
                {
                    var state = pair.Value;
                    if (state == null)
                    {
                        continue;
                    }

                    isMouseCaptured |= state.Mouse;
                    isKeyboardCaptured |= state.Keyboard;

                    if (isMouseCaptured && isKeyboardCaptured)
                    {
                        break;
                    }
                }

                changed = _isMouseCaptured != isMouseCaptured || _isKeyboardCaptured != isKeyboardCaptured;
                _isMouseCaptured = isMouseCaptured;
                _isKeyboardCaptured = isKeyboardCaptured;
            }

            if (changed)
            {
                MMLog.WriteInfo("[ShelteredOverlayInputCapture] Capture changed. Mouse=" + isMouseCaptured + ", Keyboard=" + isKeyboardCaptured + ", Owner=" + ownerId + ".");
            }
        }

        public void ReleaseCapture(string ownerId)
        {
            ReportCapture(ownerId, false, false);
        }
    }

    internal static class OverlayInputCaptureRuntime
    {
        private static IOverlayInputCaptureService _captureService;
        private static bool _suppressionLogged;

        internal static bool ShouldSuppressAnyInput()
        {
            bool mouseCaptured;
            bool keyboardCaptured;
            GetCaptureState(out mouseCaptured, out keyboardCaptured);
            return mouseCaptured || keyboardCaptured;
        }

        internal static bool ShouldSuppressMouseInput()
        {
            bool mouseCaptured;
            bool keyboardCaptured;
            GetCaptureState(out mouseCaptured, out keyboardCaptured);
            return mouseCaptured;
        }

        internal static bool ShouldSuppressKeyboardInput()
        {
            bool mouseCaptured;
            bool keyboardCaptured;
            GetCaptureState(out mouseCaptured, out keyboardCaptured);
            return keyboardCaptured;
        }

        private static void GetCaptureState(out bool mouseCaptured, out bool keyboardCaptured)
        {
            mouseCaptured = false;
            keyboardCaptured = false;

            var captureService = ResolveService();
            if (captureService == null)
            {
                TrackSuppressionTransition(false);
                return;
            }

            mouseCaptured = captureService.IsMouseCaptured;
            keyboardCaptured = captureService.IsKeyboardCaptured;
            TrackSuppressionTransition(mouseCaptured || keyboardCaptured);
        }

        private static IOverlayInputCaptureService ResolveService()
        {
            if (_captureService != null)
            {
                return _captureService;
            }

            if (!ModAPIRegistry.IsAPIRegistered(OverlayInputCaptureApi.Name))
            {
                return null;
            }

            IOverlayInputCaptureService captureService;
            if (!ModAPIRegistry.TryGetAPI<IOverlayInputCaptureService>(OverlayInputCaptureApi.Name, out captureService))
            {
                return null;
            }

            _captureService = captureService;
            return _captureService;
        }

        private static void TrackSuppressionTransition(bool isSuppressed)
        {
            if (isSuppressed)
            {
                if (_suppressionLogged)
                {
                    return;
                }

                _suppressionLogged = true;
                MMLog.WriteInfo("[OverlayInputCapture] Game input suppression active.");
                return;
            }

            if (!_suppressionLogged)
            {
                return;
            }

            _suppressionLogged = false;
            MMLog.WriteInfo("[OverlayInputCapture] Game input suppression released.");
        }
    }
}
