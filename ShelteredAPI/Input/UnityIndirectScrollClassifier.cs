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
        private const float WholeStepThreshold = 0.95f;
        private const float WholeStepTolerance = 0.001f;

        private static int _lastFrame = -1;
        private static float _gestureLatchUntil;
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

        public static UnityScrollGestureSample GetCurrentSample()
        {
            UpdateState();
            return _currentSample;
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
    }
}
