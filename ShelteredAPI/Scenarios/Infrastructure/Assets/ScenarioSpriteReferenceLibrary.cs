using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;
namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal static class ScenarioSpriteReferenceLibrary
    {
        private static readonly object GeneratedSync = new object();
        private static readonly Dictionary<string, Sprite> GeneratedSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, Sprite> FullTextureSprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static string CreateRuntimeSpriteKey(Sprite sprite)
        {
            if (sprite == null)
                return null;

            Texture2D texture = sprite.texture;
            Rect rect = sprite.rect;
            return CreateRuntimeSpriteKey(
                texture,
                sprite.name,
                Mathf.RoundToInt(rect.x),
                Mathf.RoundToInt(rect.y),
                Mathf.RoundToInt(rect.width),
                Mathf.RoundToInt(rect.height));
        }

        public static string CreateRuntimeSpriteKey(Texture2D texture, string spriteName)
        {
            if (texture == null)
                return null;

            return CreateRuntimeSpriteKey(texture, spriteName, 0, 0, texture.width, texture.height);
        }

        private static string CreateRuntimeSpriteKey(Texture2D texture, string spriteName, int x, int y, int width, int height)
        {
            if (texture == null)
                return null;

            string textureName = texture.name ?? string.Empty;
            return textureName + "|" + (spriteName ?? string.Empty) + "|"
                + x + "," + y + ","
                + width + "," + height;
        }

        public static void RegisterGeneratedSprite(string runtimeSpriteKey, Sprite sprite)
        {
            if (string.IsNullOrEmpty(runtimeSpriteKey) || sprite == null)
                return;

            lock (GeneratedSync)
            {
                GeneratedSprites[runtimeSpriteKey] = sprite;
            }
        }

        public static bool TryFindLoadedSprite(string runtimeSpriteKey, out Sprite sprite)
        {
            sprite = null;
            if (string.IsNullOrEmpty(runtimeSpriteKey))
                return false;

            lock (GeneratedSync)
            {
                if (GeneratedSprites.TryGetValue(runtimeSpriteKey, out sprite) && sprite != null)
                    return true;
            }

            // Runtime keys can collide across inactive prefab/resource copies
            // that share a texture and sprite name. Prefer the copy that is
            // actually rendering in the loaded scene so a persisted pixel
            // patch is rebuilt from the player's visible asset.
            SpriteRenderer[] activeRenderers = Resources.FindObjectsOfTypeAll<SpriteRenderer>();
            for (int i = 0; activeRenderers != null && i < activeRenderers.Length; i++)
            {
                SpriteRenderer renderer = activeRenderers[i];
                Sprite renderedSprite = renderer != null ? renderer.sprite : null;
                if (renderer == null
                    || !renderer.gameObject.activeInHierarchy
                    || renderedSprite == null
                    || !string.Equals(CreateRuntimeSpriteKey(renderedSprite), runtimeSpriteKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                sprite = renderedSprite;
                return true;
            }

            // If no active renderer owns the key, compare every matching
            // resource copy instead of accepting Resources' nondeterministic
            // enumeration order. Transparent placeholder/prefab copies score
            // below the real source artwork used to seed older runtime-key
            // patches.
            Sprite[] matchingSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            Sprite bestMatchingSprite = null;
            double bestAlphaScore = -1d;
            for (int i = 0; matchingSprites != null && i < matchingSprites.Length; i++)
            {
                Sprite candidate = matchingSprites[i];
                if (candidate == null
                    || candidate.texture == null
                    || !string.Equals(CreateRuntimeSpriteKey(candidate), runtimeSpriteKey, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                double alphaScore = 0d;
                try
                {
                    Rect rect = candidate.textureRect;
                    Color[] pixels = candidate.texture.GetPixels(
                        Mathf.RoundToInt(rect.x),
                        Mathf.RoundToInt(rect.y),
                        Mathf.Max(1, Mathf.RoundToInt(rect.width)),
                        Mathf.Max(1, Mathf.RoundToInt(rect.height)));
                    for (int pixelIndex = 0; pixels != null && pixelIndex < pixels.Length; pixelIndex++)
                        alphaScore += pixels[pixelIndex].a;
                }
                catch
                {
                    alphaScore = 0d;
                }

                if (bestMatchingSprite == null || alphaScore > bestAlphaScore)
                {
                    bestMatchingSprite = candidate;
                    bestAlphaScore = alphaScore;
                }
            }

            if (bestMatchingSprite != null)
            {
                sprite = bestMatchingSprite;
                return true;
            }

            return false;
        }

        internal static Sprite GetOrCreateFullTextureSprite(Texture2D texture, string spriteName)
        {
            if (texture == null)
                return null;

            string resolvedName = !string.IsNullOrEmpty(spriteName)
                ? spriteName
                : (!string.IsNullOrEmpty(texture.name) ? texture.name : "<texture>");
            string cacheKey = texture.GetInstanceID().ToString() + "|" + resolvedName;
            lock (GeneratedSync)
            {
                Sprite sprite;
                if (FullTextureSprites.TryGetValue(cacheKey, out sprite) && sprite != null)
                    return sprite;

                sprite = Sprite.Create(
                    texture,
                    new Rect(0f, 0f, texture.width, texture.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
                if (sprite != null)
                {
                    sprite.name = resolvedName;
                    FullTextureSprites[cacheKey] = sprite;
                }
                return sprite;
            }
        }

    }
}
