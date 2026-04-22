using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Scenarios;
using ShelteredAPI.Content;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSpriteAssetResolver : IScenarioSpriteAssetResolver
    {
        private readonly object _sync = new object();
        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        public Sprite ResolveSprite(ScenarioDefinition definition, string packRoot, string spriteId, string relativePath, string runtimeSpriteKey, string contextLabel)
        {
            if (!string.IsNullOrEmpty(runtimeSpriteKey))
            {
                Sprite cachedRuntimeSprite = GetCached("runtime:" + runtimeSpriteKey);
                if (cachedRuntimeSprite != null)
                    return cachedRuntimeSprite;

                Sprite runtimeSprite;
                if (ScenarioSpriteReferenceLibrary.TryFindLoadedSprite(runtimeSpriteKey, out runtimeSprite))
                {
                    Cache("runtime:" + runtimeSpriteKey, runtimeSprite);
                    return runtimeSprite;
                }

                MMLog.WriteWarning("[ScenarioSpriteAssetResolver] Runtime sprite was not loaded for " + (contextLabel ?? "<context>")
                    + ": " + runtimeSpriteKey);
                return null;
            }

            string resolvedRelativePath = ResolveRelativePath(definition, spriteId, relativePath);
            if (string.IsNullOrEmpty(resolvedRelativePath) || string.IsNullOrEmpty(packRoot))
                return null;

            try
            {
                string fullPath = Path.GetFullPath(Path.Combine(packRoot, resolvedRelativePath));
                Sprite cachedSprite = GetCached(fullPath);
                if (cachedSprite != null)
                    return cachedSprite;

                Sprite loaded = AssetLoader.LoadSprite(packRoot, resolvedRelativePath, 100f);
                if (loaded != null)
                    Cache(fullPath, loaded);
                return loaded;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioSpriteAssetResolver] Failed to load sprite for " + (contextLabel ?? "<context>")
                    + ": " + resolvedRelativePath + " (" + ex.Message + ")");
                return null;
            }
        }

        public string ResolveRelativePath(ScenarioDefinition definition, string spriteId, string relativePath)
        {
            if (!string.IsNullOrEmpty(relativePath))
                return relativePath;

            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.CustomSprites == null || string.IsNullOrEmpty(spriteId))
                return null;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                if (sprite != null && string.Equals(sprite.Id, spriteId, StringComparison.OrdinalIgnoreCase))
                    return sprite.RelativePath;
            }

            return null;
        }

        public void Invalidate()
        {
            lock (_sync)
            {
                _spriteCache.Clear();
            }
        }

        private Sprite GetCached(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;

            lock (_sync)
            {
                Sprite sprite;
                return _spriteCache.TryGetValue(key, out sprite) ? sprite : null;
            }
        }

        private void Cache(string key, Sprite sprite)
        {
            if (string.IsNullOrEmpty(key) || sprite == null)
                return;

            lock (_sync)
            {
                _spriteCache[key] = sprite;
            }
        }
    }
}
