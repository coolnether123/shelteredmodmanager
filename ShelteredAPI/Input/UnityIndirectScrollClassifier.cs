using UnityEngine;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Classifies Unity scroll delta once per frame so wheel and indirect-touchpad consumers
    /// operate on the same source of truth.
    /// </summary>
    internal static class UnityIndirectScrollClassifier
    {
        private const float HorizontalThreshold = 0.01f;
        private const float VerticalThreshold = 0.01f;
        private const float GestureLatchSeconds = 0.18f;
        private const float GestureResetSeconds = 0.12f;
        private const float WholeStepThreshold = 0.95f;
        private const float WholeStepTolerance = 0.001f;
        private const int PinchConfirmationFrames = 3;

        private static int _lastFrame = -1;
        private static float _gestureLatchUntil;
        private static float _lastScrollTime = -1f;
        private static int _pinchCandidateFrames;
        private static bool _pinchGestureConfirmed;
        private static UnityScrollGestureSample _currentSample;

        public static bool IsIndirectScrollActive()
        {
            UpdateState();
            return Time.unscaledTime <= _gestureLatchUntil;
        }

        public static bool IsCurrentFrameIndirectScroll()
        {
            return GetCurrentSample().Kind == UnityScrollGestureKind.Indirect;
        }

        public static bool IsCurrentFrameMapPanGesture()
        {
            return IsMapPanGesture(GetCurrentSample());
        }

        public static bool IsCurrentFramePinchZoom()
        {
            return GetCurrentSample().Kind == UnityScrollGestureKind.Pinch;
        }

        public static UnityScrollGestureSample GetCurrentSample()
        {
            UpdateState();
            return _currentSample;
        }

        public static bool IsMapPanGesture(UnityScrollGestureSample sample)
        {
            if (sample.Kind == UnityScrollGestureKind.Indirect)
                return true;

            return sample.Kind == UnityScrollGestureKind.MouseWheel
                && sample.HasScroll
                && !IsPinchModifierHeld();
        }

        private static void UpdateState()
        {
            if (_lastFrame == Time.frameCount)
                return;

            _lastFrame = Time.frameCount;
            _currentSample = Classify(UnityEngine.Input.mouseScrollDelta);

            if (_currentSample.Kind == UnityScrollGestureKind.Indirect)
                _gestureLatchUntil = Time.unscaledTime + GestureLatchSeconds;
        }

        private static UnityScrollGestureSample Classify(Vector2 delta)
        {
            float absX = Mathf.Abs(delta.x);
            float absY = Mathf.Abs(delta.y);
            if (absX <= HorizontalThreshold && absY <= VerticalThreshold)
                return new UnityScrollGestureSample(delta, UnityScrollGestureKind.None);

            if (IsConfirmedPinchGesture())
                return new UnityScrollGestureSample(delta, UnityScrollGestureKind.Pinch);

            if (LooksLikeIndirectGesture(absX, absY))
                return new UnityScrollGestureSample(delta, UnityScrollGestureKind.Indirect);

            return new UnityScrollGestureSample(delta, UnityScrollGestureKind.MouseWheel);
        }

        private static bool LooksLikeIndirectGesture(float absX, float absY)
        {
            if (absX > HorizontalThreshold)
                return true;

            if (absY <= VerticalThreshold)
                return false;

            if (absY < WholeStepThreshold)
                return true;

            return Mathf.Abs(absY - Mathf.Round(absY)) > WholeStepTolerance;
        }

        private static bool IsConfirmedPinchGesture()
        {
            if (_lastScrollTime < 0f || Time.unscaledTime - _lastScrollTime > GestureResetSeconds)
            {
                _pinchCandidateFrames = 0;
                _pinchGestureConfirmed = false;
            }

            _lastScrollTime = Time.unscaledTime;

            if (!IsPinchModifierHeld())
            {
                _pinchCandidateFrames = 0;
                _pinchGestureConfirmed = false;
                return false;
            }

            if (!_pinchGestureConfirmed)
            {
                _pinchCandidateFrames++;
                _pinchGestureConfirmed = _pinchCandidateFrames >= PinchConfirmationFrames;
            }

            return _pinchGestureConfirmed;
        }

        private static bool IsPinchModifierHeld()
        {
            return UnityEngine.Input.GetKey(KeyCode.LeftControl)
                || UnityEngine.Input.GetKey(KeyCode.RightControl);
        }
    }
}
