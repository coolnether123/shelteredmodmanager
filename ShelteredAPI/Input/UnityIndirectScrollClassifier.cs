using UnityEngine;

namespace ShelteredAPI.Input
{
    internal static class UnityIndirectScrollClassifier
    {
        private const float HorizontalThreshold = 0.01f;
        private const float VerticalThreshold = 0.01f;
        private const float GestureLatchSeconds = 0.18f;
        private const float WholeStepThreshold = 0.95f;
        private const int ConsecutiveVerticalFramesForIndirect = 2;

        private static int _lastFrame = -1;
        private static int _lastVerticalScrollFrame = -10;
        private static int _consecutiveVerticalFrames;
        private static float _gestureLatchUntil;

        public static bool IsIndirectScrollActive()
        {
            UpdateState();
            return Time.unscaledTime <= _gestureLatchUntil;
        }

        private static void UpdateState()
        {
            if (_lastFrame == Time.frameCount)
                return;

            _lastFrame = Time.frameCount;

            if (LooksLikeIndirectScroll(UnityEngine.Input.mouseScrollDelta))
                _gestureLatchUntil = Time.unscaledTime + GestureLatchSeconds;
        }

        private static bool LooksLikeIndirectScroll(Vector2 delta)
        {
            float absX = Mathf.Abs(delta.x);
            float absY = Mathf.Abs(delta.y);
            if (absX > HorizontalThreshold)
            {
                _consecutiveVerticalFrames = 0;
                return true;
            }

            if (absY <= VerticalThreshold)
            {
                _consecutiveVerticalFrames = 0;
                return false;
            }

            if (_lastVerticalScrollFrame == Time.frameCount - 1)
                _consecutiveVerticalFrames++;
            else
                _consecutiveVerticalFrames = 1;

            _lastVerticalScrollFrame = Time.frameCount;

            if (absY < WholeStepThreshold)
                return true;

            if (Mathf.Abs(absY - Mathf.Round(absY)) > 0.001f)
                return true;

            return _consecutiveVerticalFrames >= ConsecutiveVerticalFramesForIndirect;
        }
    }
}
