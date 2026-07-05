using System;
using System.Collections.Generic;

namespace ShelteredAPI.Scenarios.Presentation.UiKit.Animation
{
    internal sealed class ScenarioUiTweenSet
    {
        private readonly Dictionary<string, ScenarioUiTween> _tweens;

        public ScenarioUiTweenSet()
        {
            _tweens = new Dictionary<string, ScenarioUiTween>(StringComparer.OrdinalIgnoreCase);
        }

        public ScenarioUiTween GetOrCreate(string key, float initialValue)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            ScenarioUiTween tween;
            if (_tweens.TryGetValue(key, out tween))
                return tween;

            tween = new ScenarioUiTween();
            tween.Set(initialValue);
            _tweens.Add(key, tween);
            return tween;
        }

        public float GetValue(string key, float fallback)
        {
            ScenarioUiTween tween;
            return !string.IsNullOrEmpty(key) && _tweens.TryGetValue(key, out tween)
                ? tween.Value
                : fallback;
        }

        public bool IsRunning(string key)
        {
            ScenarioUiTween tween;
            return !string.IsNullOrEmpty(key) && _tweens.TryGetValue(key, out tween) && tween.IsRunning;
        }

        public void Play(string key, float from, float to, float duration, ScenarioUiEasing easing)
        {
            ScenarioUiTween tween = GetOrCreate(key, from);
            if (tween == null)
                return;

            if (tween.IsRunning || Math.Abs(tween.To - to) > 0.0001f || Math.Abs(tween.Value - to) > 0.0001f)
                tween.Reset(from, to, duration, easing);
        }

        public void PlayFromCurrent(string key, float to, float duration, ScenarioUiEasing easing, float fallback)
        {
            ScenarioUiTween tween = GetOrCreate(key, fallback);
            if (tween == null)
                return;

            if (Math.Abs(tween.To - to) <= 0.0001f && (!tween.IsRunning || duration > 0f))
                return;

            tween.Reset(tween.Value, to, duration, easing);
        }

        public void Set(string key, float value)
        {
            ScenarioUiTween tween = GetOrCreate(key, value);
            if (tween != null)
                tween.Set(value);
        }

        public void Remove(string key)
        {
            if (!string.IsNullOrEmpty(key))
                _tweens.Remove(key);
        }

        public void Advance(float deltaTime)
        {
            foreach (KeyValuePair<string, ScenarioUiTween> pair in _tweens)
            {
                if (pair.Value != null)
                    pair.Value.Advance(deltaTime);
            }
        }
    }
}
