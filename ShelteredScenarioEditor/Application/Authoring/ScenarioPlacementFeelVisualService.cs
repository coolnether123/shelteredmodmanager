using UnityEngine;

namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioPlacementFeelVisualService
    {
        private const float GhostPreviewAlpha = 0.62f;
        private const float SettleStartScale = 1.06f;
        private const float SettleStartAlpha = 0.8f;
        private const float SettleDurationSeconds = 0.15f;

        internal GhostPreviewHandle CreateGhostPreview(Obj_GhostBase ghost)
        {
            return ghost != null ? new GhostPreviewHandle(ghost.gameObject) : null;
        }

        public void PlaySettle(GameObject target)
        {
            if (target == null)
                return;

            PlacementSettleDriver existing = target.GetComponent<PlacementSettleDriver>();
            if (existing != null)
                existing.RestoreAndDestroy();

            PlacementSettleDriver driver = target.AddComponent<PlacementSettleDriver>();
            driver.Begin(SettleStartScale, SettleStartAlpha, SettleDurationSeconds);
        }

        internal sealed class GhostPreviewHandle
        {
            private readonly SpriteRenderer[] _renderers;
            private readonly Color[] _originalColors;

            internal GhostPreviewHandle(GameObject root)
            {
                _renderers = root != null ? root.GetComponentsInChildren<SpriteRenderer>(true) : new SpriteRenderer[0];
                _originalColors = new Color[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++)
                    _originalColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;
            }

            public void Apply()
            {
                for (int i = 0; i < _renderers.Length; i++)
                {
                    SpriteRenderer renderer = _renderers[i];
                    if (renderer == null)
                        continue;

                    Color color = renderer.color;
                    float originalAlpha = i < _originalColors.Length ? _originalColors[i].a : color.a;
                    color.a = originalAlpha * GhostPreviewAlpha;
                    renderer.color = color;
                }
            }

            public void Restore()
            {
                for (int i = 0; i < _renderers.Length && i < _originalColors.Length; i++)
                {
                    if (_renderers[i] != null)
                        _renderers[i].color = _originalColors[i];
                }
            }
        }

        private sealed class PlacementSettleDriver : MonoBehaviour
        {
            private SpriteRenderer[] _renderers;
            private Color[] _originalColors;
            private Vector3 _originalScale;
            private float _startScale;
            private float _startAlpha;
            private float _duration;
            private float _elapsed;
            private bool _restorePending;

            public void Begin(float startScale, float startAlpha, float duration)
            {
                _renderers = GetComponentsInChildren<SpriteRenderer>(true);
                _originalColors = new Color[_renderers.Length];
                for (int i = 0; i < _renderers.Length; i++)
                    _originalColors[i] = _renderers[i] != null ? _renderers[i].color : Color.white;

                _originalScale = transform.localScale;
                _startScale = startScale;
                _startAlpha = startAlpha;
                _duration = Mathf.Max(0.01f, duration);
                _elapsed = 0f;
                _restorePending = true;
                Apply(0f);
            }

            private void Update()
            {
                if (!_restorePending)
                    return;

                _elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(_elapsed / _duration);
                Apply(EaseOutCubic(t));
                if (t >= 1f)
                    RestoreAndDestroy();
            }

            public void RestoreAndDestroy()
            {
                RestoreOriginals();
                Destroy(this);
            }

            private void OnDisable()
            {
                RestoreOriginals();
            }

            private void OnDestroy()
            {
                RestoreOriginals();
            }

            private void Apply(float eased)
            {
                float scale = Mathf.Lerp(_startScale, 1f, eased);
                transform.localScale = new Vector3(
                    _originalScale.x * scale,
                    _originalScale.y * scale,
                    _originalScale.z * scale);

                for (int i = 0; i < _renderers.Length; i++)
                {
                    SpriteRenderer renderer = _renderers[i];
                    if (renderer == null)
                        continue;

                    Color original = _originalColors[i];
                    original.a = Mathf.Lerp(_originalColors[i].a * _startAlpha, _originalColors[i].a, eased);
                    renderer.color = original;
                }
            }

            private void RestoreOriginals()
            {
                if (!_restorePending)
                    return;

                transform.localScale = _originalScale;
                for (int i = 0; i < _renderers.Length && i < _originalColors.Length; i++)
                {
                    if (_renderers[i] != null)
                        _renderers[i].color = _originalColors[i];
                }

                _restorePending = false;
            }

            private static float EaseOutCubic(float t)
            {
                float inverse = 1f - t;
                return 1f - inverse * inverse * inverse;
            }
        }
    }
}
