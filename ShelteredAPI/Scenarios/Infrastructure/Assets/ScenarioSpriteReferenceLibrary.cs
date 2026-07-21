using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;
namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal static class ScenarioSpriteReferenceLibrary
    {
        private static readonly object GeneratedSync = new object();
        private static readonly Dictionary<string, Sprite> GeneratedSprites = new Dictionary<string, Sprite>(StringComparer.OrdinalIgnoreCase);

        internal sealed class LoadedSpriteReference
        {
            public string RuntimeSpriteKey;
            public string SpriteName;
            public string TextureName;
            public Sprite Sprite;
        }

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

            List<LoadedSpriteReference> loaded = GetLoadedSprites();
            for (int i = 0; i < loaded.Count; i++)
            {
                LoadedSpriteReference candidate = loaded[i];
                if (candidate != null
                    && string.Equals(candidate.RuntimeSpriteKey, runtimeSpriteKey, StringComparison.OrdinalIgnoreCase)
                    && candidate.Sprite != null)
                {
                    sprite = candidate.Sprite;
                    return true;
                }
            }

            return false;
        }

        public static List<LoadedSpriteReference> GetLoadedSprites()
        {
            Dictionary<string, LoadedSpriteReference> byKey = new Dictionary<string, LoadedSpriteReference>(StringComparer.OrdinalIgnoreCase);
            lock (GeneratedSync)
            {
                foreach (KeyValuePair<string, Sprite> generated in GeneratedSprites)
                {
                    if (generated.Value == null || string.IsNullOrEmpty(generated.Key))
                        continue;

                    byKey[generated.Key] = new LoadedSpriteReference
                    {
                        RuntimeSpriteKey = generated.Key,
                        SpriteName = string.IsNullOrEmpty(generated.Value.name) ? "<generated>" : generated.Value.name,
                        TextureName = generated.Value.texture != null && !string.IsNullOrEmpty(generated.Value.texture.name) ? generated.Value.texture.name : "<generated>",
                        Sprite = generated.Value
                    };
                }
            }

            Sprite[] loadedSprites = Resources.FindObjectsOfTypeAll<Sprite>();
            for (int i = 0; loadedSprites != null && i < loadedSprites.Length; i++)
            {
                AddLoadedSprite(byKey, loadedSprites[i]);
            }

            Sprite[] resourceSprites = null;
            try
            {
                resourceSprites = Resources.LoadAll<Sprite>(string.Empty);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioSpriteReferenceLibrary.ResourcesLoadAll", "Could not enumerate Resources sprites for the scenario editor: " + ex.Message);
            }

            for (int i = 0; resourceSprites != null && i < resourceSprites.Length; i++)
            {
                AddLoadedSprite(byKey, resourceSprites[i]);
            }

            AddParticleSystemTextureSprites(byKey);
            AddCharacterTextureSprites(byKey);

            List<LoadedSpriteReference> result = new List<LoadedSpriteReference>(byKey.Values);
            result.Sort(CompareLoadedSpriteReference);
            return result;
        }

        private static void AddLoadedSprite(Dictionary<string, LoadedSpriteReference> byKey, Sprite sprite)
        {
            if (byKey == null || sprite == null || sprite.texture == null)
                return;

            string runtimeSpriteKey = CreateRuntimeSpriteKey(sprite);
            if (string.IsNullOrEmpty(runtimeSpriteKey) || byKey.ContainsKey(runtimeSpriteKey))
                return;

            byKey[runtimeSpriteKey] = new LoadedSpriteReference
            {
                RuntimeSpriteKey = runtimeSpriteKey,
                SpriteName = string.IsNullOrEmpty(sprite.name) ? "<unnamed>" : sprite.name,
                TextureName = sprite.texture != null && !string.IsNullOrEmpty(sprite.texture.name) ? sprite.texture.name : "<texture>",
                Sprite = sprite
            };
        }

        internal static Sprite CreateFullTextureSprite(Texture2D texture, string spriteName)
        {
            if (texture == null)
                return null;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f);
            if (sprite != null)
                sprite.name = !string.IsNullOrEmpty(spriteName) ? spriteName : (!string.IsNullOrEmpty(texture.name) ? texture.name : "<texture>");
            return sprite;
        }

        private static void AddParticleSystemTextureSprites(Dictionary<string, LoadedSpriteReference> byKey)
        {
            if (byKey == null)
                return;

            ParticleSystemRenderer[] renderers = Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>();
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                Material material = renderer != null ? renderer.sharedMaterial : null;
                Texture2D texture = material != null ? material.mainTexture as Texture2D : null;
                if (texture == null)
                    continue;

                string spriteName = !string.IsNullOrEmpty(texture.name) ? texture.name : (renderer.name + "_particle");
                string runtimeSpriteKey = CreateRuntimeSpriteKey(texture, spriteName);
                if (string.IsNullOrEmpty(runtimeSpriteKey) || byKey.ContainsKey(runtimeSpriteKey))
                    continue;

                Sprite sprite = CreateFullTextureSprite(texture, spriteName);
                if (sprite == null)
                    continue;

                byKey[runtimeSpriteKey] = new LoadedSpriteReference
                {
                    RuntimeSpriteKey = runtimeSpriteKey,
                    SpriteName = spriteName,
                    TextureName = !string.IsNullOrEmpty(texture.name) ? texture.name : "<particle texture>",
                    Sprite = sprite
                };
            }
        }

        private static void AddCharacterTextureSprites(Dictionary<string, LoadedSpriteReference> byKey)
        {
            CharacterMeshOptions options = CharacterMeshOptions.instance;
            if (byKey == null || (UnityEngine.Object)options == (UnityEngine.Object)null)
                return;

            List<string> meshIds = options.GetCharacterMeshIds();
            for (int i = 0; meshIds != null && i < meshIds.Count; i++)
            {
                CharacterMeshOptions.CharacterMeshType meshType = options.FindCharacterMesh(meshIds[i]);
                if (meshType == null)
                    continue;

                AddCharacterTextureList(byKey, meshType.m_headTextures);
                AddCharacterTextureList(byKey, meshType.m_torsoTextures);
                AddCharacterTextureList(byKey, meshType.m_legTextures);
            }
        }

        private static void AddCharacterTextureList(
            Dictionary<string, LoadedSpriteReference> byKey,
            List<CharacterMeshOptions.CharacterTexture> textures)
        {
            for (int i = 0; textures != null && i < textures.Count; i++)
            {
                CharacterMeshOptions.CharacterTexture entry = textures[i];
                if (entry == null || entry.m_texture == null || string.IsNullOrEmpty(entry.m_id))
                    continue;

                string runtimeSpriteKey = CreateRuntimeSpriteKey(entry.m_texture, entry.m_id);
                if (string.IsNullOrEmpty(runtimeSpriteKey) || byKey.ContainsKey(runtimeSpriteKey))
                    continue;

                Sprite sprite = CreateFullTextureSprite(entry.m_texture, entry.m_id);
                if (sprite == null)
                    continue;
                byKey[runtimeSpriteKey] = new LoadedSpriteReference
                {
                    RuntimeSpriteKey = runtimeSpriteKey,
                    SpriteName = entry.m_id,
                    TextureName = !string.IsNullOrEmpty(entry.m_texture.name) ? entry.m_texture.name : "<character texture>",
                    Sprite = sprite
                };
            }
        }

        private static int CompareLoadedSpriteReference(LoadedSpriteReference left, LoadedSpriteReference right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;

            int texture = string.Compare(left.TextureName, right.TextureName, StringComparison.OrdinalIgnoreCase);
            if (texture != 0) return texture;

            int sprite = string.Compare(left.SpriteName, right.SpriteName, StringComparison.OrdinalIgnoreCase);
            if (sprite != 0) return sprite;

            return string.Compare(left.RuntimeSpriteKey, right.RuntimeSpriteKey, StringComparison.OrdinalIgnoreCase);
        }
    }
}
