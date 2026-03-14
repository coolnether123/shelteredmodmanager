using UnityEngine;

namespace ShelteredAPI.Input
{
    internal static class ShelteredInputTuning
    {
        public const float DefaultZoomSpeed = 1f;
        public const float DefaultTouchpadMovementSpeed = 2f;
        public const float DefaultMouseScrollSpeed = 1f;
        public const float MinSpeedScale = 0.1f;
        public const float MaxSpeedScale = 5f;
        public const float SpeedStep = 0.05f;

        private static float _zoomSpeed = DefaultZoomSpeed;
        private static float _touchpadMovementSpeed = DefaultTouchpadMovementSpeed;
        private static float _mouseScrollSpeed = DefaultMouseScrollSpeed;

        public static float ZoomSpeed
        {
            get { return _zoomSpeed; }
            set { _zoomSpeed = NormalizeSpeedScale(value, DefaultZoomSpeed); }
        }

        public static float TouchpadMovementSpeed
        {
            get { return _touchpadMovementSpeed; }
            set { _touchpadMovementSpeed = NormalizeSpeedScale(value, DefaultTouchpadMovementSpeed); }
        }

        public static float MouseScrollSpeed
        {
            get { return _mouseScrollSpeed; }
            set { _mouseScrollSpeed = NormalizeSpeedScale(value, DefaultMouseScrollSpeed); }
        }

        public static float NormalizeSpeedScale(float value, float fallback)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
                value = fallback;

            return Mathf.Clamp(value, MinSpeedScale, MaxSpeedScale);
        }
    }
}
