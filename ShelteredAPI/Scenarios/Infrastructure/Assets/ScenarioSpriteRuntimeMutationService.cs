using ModAPI.Scenarios;
using System;
using System.Collections.Generic;
using UnityEngine;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal static class ScenarioSpriteRuntimeMutationService
    {
        private sealed class AnimatedFrameSwap
        {
            public string SourceRuntimeSpriteKey;
            public Sprite Replacement;
        }

        private sealed class AnimatedFrameSwapDriver : MonoBehaviour
        {
            private readonly Dictionary<string, AnimatedFrameSwap> _swaps = new Dictionary<string, AnimatedFrameSwap>(StringComparer.OrdinalIgnoreCase);
            private SpriteRenderer _spriteRenderer;
            private UI2DSprite _ui2DSprite;
            private List<Sprite> _replacementSprites = new List<Sprite>();
            private List<Sprite> _previewFrames = new List<Sprite>();
            private List<float> _previewDurations = new List<float>();
            private Sprite _previewRestoreSprite;
            private int _previewFrameIndex;
            private float _previewAccumulator;
            private float _previewSpeed = 1f;
            private bool _previewPlaying;

            public void Configure(ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget, Dictionary<string, Sprite> replacements)
            {
                _swaps.Clear();
                _replacementSprites.Clear();
                BindTarget(runtimeTarget);

                foreach (KeyValuePair<string, Sprite> pair in replacements ?? new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase))
                {
                    if (string.IsNullOrEmpty(pair.Key) || pair.Value == null)
                        continue;

                    _swaps[pair.Key] = new AnimatedFrameSwap
                    {
                        SourceRuntimeSpriteKey = pair.Key,
                        Replacement = pair.Value
                    };
                    _replacementSprites.Add(pair.Value);
                }
            }

            public void BindTarget(ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget)
            {
                _spriteRenderer = runtimeTarget != null ? runtimeTarget.SpriteRenderer : null;
                _ui2DSprite = runtimeTarget != null ? runtimeTarget.Ui2DSprite : null;
            }

            public void ClearAndRestore()
            {
                StopPreview();
                Sprite current = GetCurrentSprite();
                if (current != null)
                {
                    for (int i = 0; i < _replacementSprites.Count; i++)
                    {
                        if ((UnityEngine.Object)current == (UnityEngine.Object)_replacementSprites[i])
                        {
                            foreach (AnimatedFrameSwap swap in _swaps.Values)
                            {
                                Sprite original;
                                if (ScenarioSpriteReferenceLibrary.TryFindLoadedSprite(swap.SourceRuntimeSpriteKey, out original) && original != null)
                                {
                                    ApplySprite(original);
                                    break;
                                }
                            }
                            break;
                        }
                    }
                }

                _swaps.Clear();
                _replacementSprites.Clear();
                Destroy(this);
            }

            public void ConfigurePreview(IList<Sprite> frames, IList<float> durations, float speed)
            {
                if (!_previewPlaying)
                    _previewRestoreSprite = GetCurrentSprite();

                _previewFrames.Clear();
                _previewDurations.Clear();
                for (int i = 0; frames != null && i < frames.Count; i++)
                {
                    Sprite frame = frames[i];
                    if (frame == null)
                        continue;

                    _previewFrames.Add(frame);
                    float duration = durations != null && i < durations.Count ? durations[i] : 0.167f;
                    _previewDurations.Add(Mathf.Max(0.01f, duration));
                }

                _previewSpeed = Mathf.Clamp(speed <= 0f ? 1f : speed, 0.25f, 2f);
                _previewPlaying = _previewFrames.Count > 0;
                _previewFrameIndex = Mathf.Clamp(_previewFrameIndex, 0, Math.Max(0, _previewFrames.Count - 1));
                _previewAccumulator = 0f;
                if (_previewPlaying)
                    ApplySprite(_previewFrames[_previewFrameIndex]);
            }

            public void StopPreview()
            {
                if (_previewPlaying && _previewRestoreSprite != null)
                    ApplySprite(_previewRestoreSprite);

                _previewPlaying = false;
                _previewRestoreSprite = null;
                _previewFrameIndex = 0;
                _previewAccumulator = 0f;
                _previewFrames.Clear();
                _previewDurations.Clear();
            }

            private void LateUpdate()
            {
                if (_previewPlaying)
                {
                    TickPreview();
                    return;
                }

                Sprite current = GetCurrentSprite();
                if (current == null || _swaps.Count == 0)
                    return;

                string key = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(current);
                AnimatedFrameSwap swap;
                if (!_swaps.TryGetValue(key, out swap) || swap == null || swap.Replacement == null)
                    return;

                ApplySprite(swap.Replacement);
            }

            private void TickPreview()
            {
                if (_previewFrames.Count == 0)
                    return;

                _previewFrameIndex = Mathf.Clamp(_previewFrameIndex, 0, _previewFrames.Count - 1);
                ApplySprite(_previewFrames[_previewFrameIndex]);
                float duration = _previewFrameIndex < _previewDurations.Count ? _previewDurations[_previewFrameIndex] : 0.167f;
                _previewAccumulator += Time.unscaledDeltaTime * Mathf.Clamp(_previewSpeed, 0.25f, 2f);
                if (_previewAccumulator < Mathf.Max(0.01f, duration))
                    return;

                _previewAccumulator = 0f;
                _previewFrameIndex = (_previewFrameIndex + 1) % _previewFrames.Count;
                ApplySprite(_previewFrames[_previewFrameIndex]);
            }

            private Sprite GetCurrentSprite()
            {
                if (_spriteRenderer != null)
                    return _spriteRenderer.sprite;
                if (_ui2DSprite != null)
                    return _ui2DSprite.sprite2D;
                return null;
            }

            private void ApplySprite(Sprite sprite)
            {
                if (_spriteRenderer != null)
                    _spriteRenderer.sprite = sprite;
                else if (_ui2DSprite != null)
                    _ui2DSprite.sprite2D = sprite;
            }
        }

        public static bool TryApply(
            ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget,
            Sprite sprite)
        {
            if (runtimeTarget == null || sprite == null)
                return false;

            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer
                && runtimeTarget.SpriteRenderer != null)
            {
                runtimeTarget.SpriteRenderer.sprite = sprite;
                return true;
            }

            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.UI2DSprite
                && runtimeTarget.Ui2DSprite != null)
            {
                runtimeTarget.Ui2DSprite.sprite2D = sprite;
                return true;
            }

            if (runtimeTarget.Kind == ScenarioSpriteTargetComponentKind.ParticleSystemRenderer
                && runtimeTarget.ParticleRenderer != null)
            {
                Texture2D texture = CreateTextureForParticleMaterial(sprite);
                if (texture == null)
                    return false;

                Material material = runtimeTarget.ParticleRenderer.material;
                if (material == null)
                    return false;

                material.mainTexture = texture;
                return true;
            }

            return false;
        }

        public static bool TryApplyAnimatedFrameSwaps(
            ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget,
            Dictionary<string, Sprite> replacements)
        {
            if (runtimeTarget == null || runtimeTarget.Transform == null || replacements == null || replacements.Count == 0)
                return false;

            if (runtimeTarget.Kind != ScenarioSpriteTargetComponentKind.SpriteRenderer
                && runtimeTarget.Kind != ScenarioSpriteTargetComponentKind.UI2DSprite)
            {
                return false;
            }

            AnimatedFrameSwapDriver driver = runtimeTarget.Transform.GetComponent<AnimatedFrameSwapDriver>();
            if (driver == null)
                driver = runtimeTarget.Transform.gameObject.AddComponent<AnimatedFrameSwapDriver>();

            driver.Configure(runtimeTarget, replacements);
            return true;
        }

        public static bool TryPlayEditedAnimation(
            ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget,
            IList<Sprite> frames,
            IList<float> durations,
            float speed)
        {
            if (runtimeTarget == null || runtimeTarget.Transform == null || frames == null || frames.Count == 0)
                return false;

            if (runtimeTarget.Kind != ScenarioSpriteTargetComponentKind.SpriteRenderer
                && runtimeTarget.Kind != ScenarioSpriteTargetComponentKind.UI2DSprite)
            {
                return false;
            }

            AnimatedFrameSwapDriver driver = runtimeTarget.Transform.GetComponent<AnimatedFrameSwapDriver>();
            if (driver == null)
                driver = runtimeTarget.Transform.gameObject.AddComponent<AnimatedFrameSwapDriver>();

            driver.BindTarget(runtimeTarget);
            driver.ConfigurePreview(frames, durations, speed);
            return true;
        }

        public static void StopEditedAnimation(ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget)
        {
            if (runtimeTarget == null || runtimeTarget.Transform == null)
                return;

            AnimatedFrameSwapDriver driver = runtimeTarget.Transform.GetComponent<AnimatedFrameSwapDriver>();
            if (driver != null)
                driver.StopPreview();
        }

        public static void ClearAnimatedFrameSwaps(ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget)
        {
            if (runtimeTarget == null || runtimeTarget.Transform == null)
                return;

            AnimatedFrameSwapDriver driver = runtimeTarget.Transform.GetComponent<AnimatedFrameSwapDriver>();
            if (driver != null)
                driver.ClearAndRestore();
        }

        private static Texture2D CreateTextureForParticleMaterial(Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return null;

            Rect rect = sprite.textureRect;
            int x = Mathf.RoundToInt(rect.x);
            int y = Mathf.RoundToInt(rect.y);
            int width = Mathf.RoundToInt(rect.width);
            int height = Mathf.RoundToInt(rect.height);
            if (x == 0
                && y == 0
                && width == sprite.texture.width
                && height == sprite.texture.height)
            {
                return sprite.texture;
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.ARGB32, false);
            texture.filterMode = sprite.texture.filterMode;
            texture.wrapMode = sprite.texture.wrapMode;
            texture.name = !string.IsNullOrEmpty(sprite.name) ? sprite.name : sprite.texture.name;
            try
            {
                texture.SetPixels(sprite.texture.GetPixels(x, y, width, height));
                texture.Apply();
                return texture;
            }
            catch
            {
                return sprite.texture;
            }
        }
    }
}
