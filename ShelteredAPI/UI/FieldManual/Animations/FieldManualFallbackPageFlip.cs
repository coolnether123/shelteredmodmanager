using UnityEngine;

namespace ShelteredAPI.UI.FieldManual.Animations
{
    internal sealed class FieldManualFallbackPageFlip : MonoBehaviour
    {
        public float Duration = 0.18f;
        public float TravelDistance = 300f;
        public float StartScaleX = 0.95f;
        public float MinimumScaleX = 0.12f;
        public int Direction = 1;

        private UIWidget _widget;
        private float _startedAt;

        private void OnEnable()
        {
            _widget = GetComponent<UIWidget>();
            _startedAt = Time.realtimeSinceStartup;
        }

        private void Update()
        {
            float duration = Duration <= 0f ? 0.01f : Duration;
            float factor = Mathf.Clamp01((Time.realtimeSinceStartup - _startedAt) / duration);
            float curve = Mathf.Sin(factor * 3.14159274f);
            float scaleX = Mathf.Lerp(StartScaleX, MinimumScaleX, curve);

            transform.localScale = new Vector3(Mathf.Max(0.04f, scaleX), 1f, 1f);
            float startX = Direction >= 0 ? TravelDistance : -TravelDistance;
            float endX = Direction >= 0 ? -TravelDistance : TravelDistance;
            transform.localPosition = new Vector3(Mathf.Lerp(startX, endX, factor), 0f, 0f);

            if (_widget != null)
                _widget.alpha = Mathf.Clamp01(curve * 1.2f) * 0.88f;
        }
    }
}
