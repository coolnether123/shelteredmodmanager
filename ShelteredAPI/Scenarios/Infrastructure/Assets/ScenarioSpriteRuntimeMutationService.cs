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

            public void Configure(ScenarioSpriteRuntimeResolver.ResolvedTarget runtimeTarget, Dictionary<string, Sprite> replacements)
            {
                _swaps.Clear();
                _replacementSprites.Clear();
                _spriteRenderer = runtimeTarget != null ? runtimeTarget.SpriteRenderer : null;
                _ui2DSprite = runtimeTarget != null ? runtimeTarget.Ui2DSprite : null;

                foreach (KeyValuePair<string, Sprite> pair in replacements)
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

            public void ClearAndRestore()
            {
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

            private void LateUpdate()
            {
                Sprite current = GetCurrentSprite();
                if (current == null || _swaps.Count == 0)
                    return;

                string key = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(current);
                AnimatedFrameSwap swap;
                if (!_swaps.TryGetValue(key, out swap) || swap == null || swap.Replacement == null)
                    return;

                ApplySprite(swap.Replacement);
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
