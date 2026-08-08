using UnityEngine;

namespace ShelteredScenarioEditor.Presentation.UiKit.Animation
{
    internal enum ScenarioUiSlideDirection
    {
        None = 0,
        Left = 1,
        Right = 2,
        Up = 3,
        Down = 4
    }

    internal static class ScenarioUiAnimator
    {
        private const float SlideDistanceMultiplier = 1.08f;
        private const float SafePadding = 12f;

        public static Rect ResolveSlidingRect(Rect rect, float openProgress, ScenarioUiSlideDirection direction, float distance)
        {
            if (rect.width <= 0f || rect.height <= 0f)
                return rect;

            float open = Mathf.Clamp01(openProgress);
            float closedOffset = Mathf.Max(0f, 1f - open) * (Mathf.Max(0f, distance) + SafePadding);
            if (closedOffset <= 0f || direction == ScenarioUiSlideDirection.None)
                return rect;

            switch (direction)
            {
                case ScenarioUiSlideDirection.Left:
                    return new Rect(rect.x - closedOffset, rect.y, rect.width, rect.height);
                case ScenarioUiSlideDirection.Right:
                    return new Rect(rect.x + closedOffset, rect.y, rect.width, rect.height);
                case ScenarioUiSlideDirection.Up:
                    return new Rect(rect.x, rect.y - closedOffset, rect.width, rect.height);
                case ScenarioUiSlideDirection.Down:
                    return new Rect(rect.x, rect.y + closedOffset, rect.width, rect.height);
                default:
                    return rect;
            }
        }

        public static float ResolveSlideDistance(Rect rect, ScenarioUiSlideDirection direction)
        {
            float magnitude = direction == ScenarioUiSlideDirection.Left || direction == ScenarioUiSlideDirection.Right
                ? rect.width
                : rect.height;
            return magnitude * SlideDistanceMultiplier;
        }

        public static float ResolveVisibility(
            ScenarioUiTweenSet tweens,
            string key,
            bool visible,
            float openDuration,
            float closeDuration,
            ScenarioUiEasing easing,
            bool blocksWorldInput)
        {
            if (tweens == null)
                return visible ? 1f : 0f;

            return GetVisibility(tweens, key, visible, openDuration, closeDuration, easing, blocksWorldInput);
        }

        private static float GetVisibility(
            ScenarioUiTweenSet tweens,
            string key,
            bool visible,
            float openDuration,
            float closeDuration,
            ScenarioUiEasing easing,
            bool blocksWorldInput)
        {
            if (string.IsNullOrEmpty(key))
                return visible ? 1f : 0f;

            float target = visible ? 1f : 0f;
            if (visible)
                tweens.PlayFromCurrent(key, target, openDuration, easing, 1f);
            else
                tweens.PlayFromCurrent(key, target, closeDuration, easing, 0f);

            float value = tweens.GetValue(key, target);
            if (blocksWorldInput && tweens.IsRunning(key))
                return value;

            return value;
        }
    }
}
