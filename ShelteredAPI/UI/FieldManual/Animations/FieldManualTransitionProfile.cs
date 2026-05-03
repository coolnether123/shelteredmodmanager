namespace ShelteredAPI.UI.FieldManual.Animations
{
    /// <summary>
    /// Immutable settings for a field-manual UI transition.
    /// </summary>
    internal sealed class FieldManualTransitionProfile
    {
        public readonly float FromAlpha;
        public readonly float ToAlpha;
        public readonly float Duration;
        public readonly float Delay;
        public readonly UITweener.Method Method;

        public static readonly FieldManualTransitionProfile VanillaPageInfoFade =
            FadeIn(0.18f, 0.02f, UITweener.Method.EaseOut);

        private FieldManualTransitionProfile(float fromAlpha, float toAlpha, float duration, float delay, UITweener.Method method)
        {
            FromAlpha = fromAlpha;
            ToAlpha = toAlpha;
            Duration = duration < 0f ? 0f : duration;
            Delay = delay < 0f ? 0f : delay;
            Method = method;
        }

        public static FieldManualTransitionProfile FadeIn(float duration, float delay, UITweener.Method method)
        {
            return new FieldManualTransitionProfile(0f, 1f, duration, delay, method);
        }

        public static FieldManualTransitionProfile FadeOut(float duration, float delay, UITweener.Method method)
        {
            return new FieldManualTransitionProfile(1f, 0f, duration, delay, method);
        }

        public static FieldManualTransitionProfile Between(float fromAlpha, float toAlpha, float duration, float delay, UITweener.Method method)
        {
            return new FieldManualTransitionProfile(fromAlpha, toAlpha, duration, delay, method);
        }
    }
}
