using UnityEngine;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Tracks Unity touch contacts and exposes drag semantics for touch-capable devices.
    /// </summary>
    internal static class UnityTouchDragTracker
    {
        private const int NoFinger = -1;
        private const float DragThresholdPixels = 8f;
        private const float ScrollPixelsPerUnit = 35f;
        private const float MinDeltaForScrollPixels = 0.5f;

        private static int _cachedFrame = -1;
        private static int _activeFingerId = NoFinger;
        private static bool _touchHeld;
        private static Vector2 _touchPosition;
        private static Vector2 _touchDelta;
        private static float _dragDistance;
        private static bool _dragHeld;
        private static bool _dragDown;
        private static bool _dragUp;

        public static bool IsDragDown(float minUiX, float maxUiX)
        {
            UpdateFrameState();
            return _dragDown && IsTouchWithinXRange(minUiX, maxUiX);
        }

        public static bool IsDragHeld(float minUiX, float maxUiX)
        {
            UpdateFrameState();
            return _dragHeld && IsTouchWithinXRange(minUiX, maxUiX);
        }

        public static bool IsDragUp(float minUiX, float maxUiX)
        {
            UpdateFrameState();
            return _dragUp && IsTouchWithinXRange(minUiX, maxUiX);
        }

        public static bool TryGetVerticalScroll(float minUiX, float maxUiX, out float scroll)
        {
            scroll = 0f;
            UpdateFrameState();

            if (!_dragHeld) return false;
            if (!IsTouchWithinXRange(minUiX, maxUiX)) return false;
            if (Mathf.Abs(_touchDelta.y) < MinDeltaForScrollPixels) return false;

            scroll = -_touchDelta.y / ScrollPixelsPerUnit;
            return scroll != 0f;
        }

        private static void UpdateFrameState()
        {
            if (_cachedFrame == Time.frameCount) return;
            _cachedFrame = Time.frameCount;

            bool wasDragHeld = _dragHeld;

            _touchDelta = Vector2.zero;
            _dragDown = false;
            _dragUp = false;

            bool hasTouch = UnityEngine.Input.touchSupported && UnityEngine.Input.touchCount > 0;
            Touch touch = new Touch();
            bool foundTouch = false;

            if (hasTouch)
            {
                if (_activeFingerId != NoFinger && TryGetTouchByFinger(_activeFingerId, out touch))
                {
                    foundTouch = true;
                }
                else
                {
                    touch = UnityEngine.Input.GetTouch(0);
                    _activeFingerId = touch.fingerId;
                    foundTouch = true;
                }
            }

            if (!foundTouch)
            {
                _touchHeld = false;
                _activeFingerId = NoFinger;
                _dragDistance = 0f;
            }
            else
            {
                _touchPosition = touch.position;

                switch (touch.phase)
                {
                    case TouchPhase.Began:
                        _touchHeld = true;
                        _touchDelta = Vector2.zero;
                        _dragDistance = 0f;
                        break;

                    case TouchPhase.Moved:
                        if (!_touchHeld)
                        {
                            _touchHeld = true;
                            _dragDistance = 0f;
                        }

                        _touchDelta = touch.deltaPosition;
                        _dragDistance += _touchDelta.magnitude;
                        break;

                    case TouchPhase.Stationary:
                        if (!_touchHeld)
                            _touchHeld = true;

                        _touchDelta = Vector2.zero;
                        break;

                    case TouchPhase.Ended:
                    case TouchPhase.Canceled:
                        _touchDelta = touch.deltaPosition;
                        _touchHeld = false;
                        _activeFingerId = NoFinger;
                        _dragDistance = 0f;
                        break;
                }
            }

            _dragHeld = _touchHeld && _dragDistance >= DragThresholdPixels;
            _dragDown = !wasDragHeld && _dragHeld;
            _dragUp = wasDragHeld && !_dragHeld;
        }

        private static bool TryGetTouchByFinger(int fingerId, out Touch touch)
        {
            for (int i = 0; i < UnityEngine.Input.touchCount; i++)
            {
                Touch candidate = UnityEngine.Input.GetTouch(i);
                if (candidate.fingerId == fingerId)
                {
                    touch = candidate;
                    return true;
                }
            }

            touch = new Touch();
            return false;
        }

        private static bool IsTouchWithinXRange(float minUiX, float maxUiX)
        {
            float uiX = _touchPosition.x - (Screen.width * 0.5f);
            return uiX >= minUiX && uiX <= maxUiX;
        }
    }
}
