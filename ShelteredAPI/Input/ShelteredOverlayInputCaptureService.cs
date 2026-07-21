using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Debugging;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Aggregates input ownership requests from runtime overlays without coupling
    /// the game-input patches to any particular overlay implementation.
    /// </summary>
    internal sealed class ShelteredOverlayInputCaptureService : IOverlayInputCaptureService
    {
        private sealed class CaptureState
        {
            public bool Mouse;
            public bool Keyboard;
        }

        private readonly object _sync = new object();
        private readonly Dictionary<string, CaptureState> _captures =
            new Dictionary<string, CaptureState>(StringComparer.OrdinalIgnoreCase);
        private bool _isMouseCaptured;
        private bool _isKeyboardCaptured;

        public bool IsMouseCaptured
        {
            get
            {
                lock (_sync)
                    return _isMouseCaptured;
            }
        }

        public bool IsKeyboardCaptured
        {
            get
            {
                lock (_sync)
                    return _isKeyboardCaptured;
            }
        }

        public void ReportCapture(string ownerId, bool captureMouse, bool captureKeyboard)
        {
            if (string.IsNullOrEmpty(ownerId))
                return;

            bool changed;
            bool mouseCaptured = false;
            bool keyboardCaptured = false;
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

                foreach (CaptureState state in _captures.Values)
                {
                    if (state == null)
                        continue;

                    mouseCaptured |= state.Mouse;
                    keyboardCaptured |= state.Keyboard;
                    if (mouseCaptured && keyboardCaptured)
                        break;
                }

                changed = _isMouseCaptured != mouseCaptured || _isKeyboardCaptured != keyboardCaptured;
                _isMouseCaptured = mouseCaptured;
                _isKeyboardCaptured = keyboardCaptured;
            }

            if (changed)
            {
                MMLog.WriteDebug("[ShelteredOverlayInputCapture] Capture changed. Mouse="
                    + mouseCaptured + ", Keyboard=" + keyboardCaptured + ", Owner=" + ownerId + ".");
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

        internal static bool ShouldSuppressAnyInput()
        {
            return ShouldSuppressMouseInput() || ShouldSuppressKeyboardInput();
        }

        internal static bool ShouldSuppressMouseInput()
        {
            IOverlayInputCaptureService captureService = ResolveService();
            return ShelteredFeedbackInputEnabler.IsOverlayVisible
                || (captureService != null && captureService.IsMouseCaptured);
        }

        internal static bool ShouldSuppressKeyboardInput()
        {
            IOverlayInputCaptureService captureService = ResolveService();
            return ShelteredFeedbackInputEnabler.IsOverlayVisible
                || (captureService != null && captureService.IsKeyboardCaptured);
        }

        private static IOverlayInputCaptureService ResolveService()
        {
            if (_captureService != null)
                return _captureService;

            IOverlayInputCaptureService captureService;
            if (!ModAPIRegistry.TryGetAPI<IOverlayInputCaptureService>(OverlayInputCaptureApi.Name, out captureService))
                return null;

            _captureService = captureService;
            return _captureService;
        }
    }
}
