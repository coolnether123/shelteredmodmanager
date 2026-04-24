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
        private readonly SpritePatchApplyService _spritePatchApplyService;

        public ScenarioSpriteAssetResolver()
            : this(null)
        {
        }

        public ScenarioSpriteAssetResolver(SpritePatchApplyService spritePatchApplyService)
        {
            _spritePatchApplyService = spritePatchApplyService;
        }

        public Sprite ResolveSprite(ScenarioDefinition definition, string packRoot, string spriteId, string relativePath, string runtimeSpriteKey, string contextLabel)
        {
            return ResolveSpriteInternal(definition, packRoot, spriteId, relativePath, runtimeSpriteKey, contextLabel, 0);
        }

        private Sprite ResolveSpriteInternal(ScenarioDefinition definition, string packRoot, string spriteId, string relativePath, string runtimeSpriteKey, string contextLabel, int depth)
        {
            if (depth > 8)
                return null;

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

            SpriteRef spriteRef = ResolveSpriteReference(definition, spriteId, relativePath);
            if (spriteRef != null && !string.IsNullOrEmpty(spriteRef.PatchId))
            {
                Sprite patched = ResolvePatchedSprite(definition, packRoot, spriteRef, contextLabel, depth + 1);
                if (patched != null)
                    return patched;
            }

            string resolvedRelativePath = relativePath;
            if (string.IsNullOrEmpty(resolvedRelativePath) && spriteRef != null)
                resolvedRelativePath = spriteRef.RelativePath;
            if (string.IsNullOrEmpty(resolvedRelativePath))
                resolvedRelativePath = ResolveRelativePath(definition, spriteId, relativePath);
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

        private Sprite ResolvePatchedSprite(ScenarioDefinition definition, string packRoot, SpriteRef spriteRef, string contextLabel, int depth)
        {
            if (definition == null
                || definition.AssetReferences == null
                || spriteRef == null
                || string.IsNullOrEmpty(spriteRef.PatchId)
                || _spritePatchApplyService == null)
            {
                return null;
            }

            SpritePatchDefinition patch = FindPatch(definition.AssetReferences, spriteRef.PatchId);
            if (patch == null)
                return null;

            string cacheKey = "patch:" + spriteRef.PatchId;
            Sprite cached = GetCached(cacheKey);
            if (cached != null)
                return cached;

            Sprite baseSprite = ResolveSpriteInternal(
                definition,
                packRoot,
                patch.BaseSpriteId,
                patch.BaseRelativePath,
                patch.BaseRuntimeSpriteKey,
                contextLabel,
                depth);
            if (baseSprite == null)
                return null;

            Sprite patched = _spritePatchApplyService.Apply(patch, baseSprite);
            if (patched != null)
            {
                Cache(cacheKey, patched);
                ScenarioSpriteReferenceLibrary.RegisterGeneratedSprite(cacheKey, patched);
            }
            return patched;
        }

        private static SpriteRef ResolveSpriteReference(ScenarioDefinition definition, string spriteId, string relativePath)
        {
            if (definition == null || definition.AssetReferences == null || definition.AssetReferences.CustomSprites == null)
                return null;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                if (sprite == null)
                    continue;

                if (!string.IsNullOrEmpty(spriteId)
                    && string.Equals(sprite.Id, spriteId, StringComparison.OrdinalIgnoreCase))
                {
                    return sprite;
                }

                if (!string.IsNullOrEmpty(relativePath)
                    && string.Equals(sprite.RelativePath, relativePath, StringComparison.OrdinalIgnoreCase))
                {
                    return sprite;
                }
            }

            return null;
        }

        private static SpritePatchDefinition FindPatch(AssetReferencesDefinition assets, string patchId)
        {
            if (assets == null || assets.SpritePatches == null || string.IsNullOrEmpty(patchId))
                return null;

            for (int i = 0; i < assets.SpritePatches.Count; i++)
            {
                SpritePatchDefinition patch = assets.SpritePatches[i];
                if (patch != null && string.Equals(patch.Id, patchId, StringComparison.OrdinalIgnoreCase))
                    return patch;
            }

            return null;
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
