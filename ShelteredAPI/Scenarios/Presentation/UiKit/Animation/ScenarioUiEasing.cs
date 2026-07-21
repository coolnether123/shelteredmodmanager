using UnityEngine;

namespace ShelteredAPI.Scenarios.Presentation.UiKit.Animation
{
    internal enum ScenarioUiEasing
    {
        Linear = 0,
        EaseOut = 1,
        EaseInOut = 2,
        PopupOut = 3
    }

    internal static class ScenarioUiEasingFunctions
    {
        public static float Apply(ScenarioUiEasing easing, float t)
        {
            t = Mathf.Clamp01(t);
            switch (easing)
            {
                case ScenarioUiEasing.EaseOut:
                    return Mathf.Sin(t * Mathf.PI * 0.5f);

                case ScenarioUiEasing.EaseInOut:
                    return 0.5f - (Mathf.Cos(t * Mathf.PI) * 0.5f);

                case ScenarioUiEasing.PopupOut:
                    return 1f - Mathf.Pow(1f - t, 3f);

                default:
                    return t;
            }
        }
    }
}
