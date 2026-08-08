using UnityEngine;

namespace ShelteredScenarioEditor.Presentation.UiKit.Animation
{
    internal sealed class ScenarioUiTween
    {
        public float From;
        public float To;
        public float Value;
        public float Duration;
        public float Elapsed;
        public int Direction;
        public ScenarioUiEasing Easing;
        public bool IsRunning;

        public void Reset(float from, float to, float duration, ScenarioUiEasing easing)
        {
            From = from;
            To = to;
            Duration = Mathf.Max(0f, duration);
            Elapsed = 0f;
            Direction = to >= from ? 1 : -1;
            Easing = easing;
            IsRunning = Duration > 0.0001f;
            Value = IsRunning ? from : to;
        }

        public void Set(float value)
        {
            From = value;
            To = value;
            Value = value;
            Duration = 0f;
            Elapsed = 0f;
            Direction = 0;
            IsRunning = false;
        }

        public void Advance(float deltaTime)
        {
            if (!IsRunning)
                return;

            if (Duration <= 0.0001f)
            {
                Value = To;
                IsRunning = false;
                return;
            }

            Elapsed += Mathf.Max(0f, deltaTime);
            float t = Mathf.Clamp01(Elapsed / Duration);
            Value = Mathf.Lerp(From, To, ScenarioUiEasingFunctions.Apply(Easing, t));
            if (t >= 1f)
            {
                Value = To;
                IsRunning = false;
            }
        }
    }
}
