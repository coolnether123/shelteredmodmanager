using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
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
            string textureName = texture != null ? texture.name ?? string.Empty : string.Empty;
            string spriteName = sprite.name ?? string.Empty;
            return textureName + "|" + spriteName + "|"
                + Mathf.RoundToInt(rect.x) + "," + Mathf.RoundToInt(rect.y) + ","
                + Mathf.RoundToInt(rect.width) + "," + Mathf.RoundToInt(rect.height);
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
                Sprite sprite = loadedSprites[i];
                if (sprite == null || sprite.texture == null)
                    continue;

                string runtimeSpriteKey = CreateRuntimeSpriteKey(sprite);
                if (string.IsNullOrEmpty(runtimeSpriteKey) || byKey.ContainsKey(runtimeSpriteKey))
                    continue;

                byKey[runtimeSpriteKey] = new LoadedSpriteReference
                {
                    RuntimeSpriteKey = runtimeSpriteKey,
                    SpriteName = string.IsNullOrEmpty(sprite.name) ? "<unnamed>" : sprite.name,
                    TextureName = sprite.texture != null && !string.IsNullOrEmpty(sprite.texture.name) ? sprite.texture.name : "<texture>",
                    Sprite = sprite
                };
            }

            List<LoadedSpriteReference> result = new List<LoadedSpriteReference>(byKey.Values);
            result.Sort(CompareLoadedSpriteReference);
            return result;
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
