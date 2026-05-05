using ShelteredAPI.Content;

namespace ShelteredAPI.Input
{
    /// <summary>
    /// Stable Sheltered-facing input facade for runtime readiness, action context lookup,
    /// and Sheltered-specific tuning values.
    /// </summary>
    public static class ShelteredInput
    {
        public static void EnsureReady()
        {
            ShelteredVanillaInputActions.EnsureRuntimeLoaded();
        }

        public static void RegisterVanillaActions()
        {
            ShelteredVanillaInputActions.EnsureRegistered();
        }

        public static bool IsShelteredAction(string actionId)
        {
            return ShelteredInputActions.IsShelteredAction(actionId);
        }

        public static InputContext GetContextForActionId(string actionId)
        {
            return ShelteredVanillaInputActions.GetContextForActionId(actionId);
        }

        public static float ZoomSpeed
        {
            get { return ShelteredInputTuning.ZoomSpeed; }
            set { ShelteredInputTuning.ZoomSpeed = value; }
        }

        public static float TouchpadMovementSpeed
        {
            get { return ShelteredInputTuning.TouchpadMovementSpeed; }
            set { ShelteredInputTuning.TouchpadMovementSpeed = value; }
        }

        public static float MouseScrollSpeed
        {
            get { return ShelteredInputTuning.MouseScrollSpeed; }
            set { ShelteredInputTuning.MouseScrollSpeed = value; }
        }

        public static float NormalizeSpeedScale(float value, float fallback)
        {
            return ShelteredInputTuning.NormalizeSpeedScale(value, fallback);
        }

        public static float DefaultZoomSpeed
        {
            get { return ShelteredInputTuning.DefaultZoomSpeed; }
        }

        public static float DefaultTouchpadMovementSpeed
        {
            get { return ShelteredInputTuning.DefaultTouchpadMovementSpeed; }
        }

        public static float DefaultMouseScrollSpeed
        {
            get { return ShelteredInputTuning.DefaultMouseScrollSpeed; }
        }

        public static float MinSpeedScale
        {
            get { return ShelteredInputTuning.MinSpeedScale; }
        }

        public static float MaxSpeedScale
        {
            get { return ShelteredInputTuning.MaxSpeedScale; }
        }

        public static float SpeedStep
        {
            get { return ShelteredInputTuning.SpeedStep; }
        }
    }
}
