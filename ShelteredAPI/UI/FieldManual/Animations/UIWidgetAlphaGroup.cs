using System.Collections.Generic;
using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Animations
{
    /// <summary>
    /// Applies a normalized alpha multiplier to visible child NGUI widgets.
    /// </summary>
    internal sealed class UIWidgetAlphaGroup : MonoBehaviour
    {
        private static readonly Dictionary<UIWidget, float> OriginalAlphas = new Dictionary<UIWidget, float>();

        private UIWidget[] _widgets = new UIWidget[0];
        private float[] _baseAlphas = new float[0];
        private float _from;
        private float _to;
        private float _duration;
        private float _delay;
        private float _startedAt;
        private UITweener.Method _method;

        public void Play(FieldManualTransitionProfile profile)
        {
            if (profile == null)
                return;

            CaptureWidgets();
            _from = profile.FromAlpha;
            _to = profile.ToAlpha;
            _duration = profile.Duration;
            _delay = profile.Delay;
            _method = profile.Method;
            _startedAt = Time.realtimeSinceStartup;

            ApplyAlpha(_from);

            if (_duration <= 0f && _delay <= 0f)
            {
                Complete(_to);
                return;
            }

            enabled = true;
        }

        public void Complete(float alpha)
        {
            ApplyAlpha(alpha);
            enabled = false;
        }

        private void Update()
        {
            float elapsed = Time.realtimeSinceStartup - _startedAt;
            if (elapsed < _delay)
                return;

            float factor = _duration <= 0f ? 1f : Mathf.Clamp01((elapsed - _delay) / _duration);
            ApplyAlpha(Mathf.Lerp(_from, _to, Ease(factor, _method)));

            if (factor >= 1f)
                enabled = false;
        }

        private void CaptureWidgets()
        {
            PruneDestroyedAlphaEntries();
            _widgets = GetComponentsInChildren<UIWidget>(false);
            _baseAlphas = new float[_widgets.Length];

            for (int i = 0; i < _widgets.Length; i++)
            {
                UIWidget widget = _widgets[i];
                if (widget == null)
                {
                    _baseAlphas[i] = 0f;
                    continue;
                }

                float baseAlpha;
                if (!OriginalAlphas.TryGetValue(widget, out baseAlpha))
                {
                    baseAlpha = widget.alpha;
                    OriginalAlphas[widget] = baseAlpha;
                }

                _baseAlphas[i] = baseAlpha;
            }
        }

        private static void PruneDestroyedAlphaEntries()
        {
            if (OriginalAlphas.Count == 0)
                return;

            List<UIWidget> destroyed = null;
            foreach (UIWidget widget in OriginalAlphas.Keys)
            {
                if (widget != null)
                    continue;

                if (destroyed == null)
                    destroyed = new List<UIWidget>();
                destroyed.Add(widget);
            }

            if (destroyed == null)
                return;

            for (int i = 0; i < destroyed.Count; i++)
                OriginalAlphas.Remove(destroyed[i]);
        }

        private void ApplyAlpha(float alpha)
        {
            float normalized = Mathf.Clamp01(alpha);
            for (int i = 0; i < _widgets.Length; i++)
            {
                UIWidget widget = _widgets[i];
                if (widget == null)
                    continue;

                widget.alpha = _baseAlphas[i] * normalized;
            }
        }

        private static float Ease(float factor, UITweener.Method method)
        {
            float clamped = Mathf.Clamp01(factor);
            if (method == UITweener.Method.EaseIn)
                return 1f - Mathf.Sin(1.57079637f * (1f - clamped));
            if (method == UITweener.Method.EaseOut)
                return Mathf.Sin(1.57079637f * clamped);
            if (method == UITweener.Method.EaseInOut)
                return clamped - Mathf.Sin(clamped * 6.28318548f) / 6.28318548f;

            return clamped;
        }
    }
}
