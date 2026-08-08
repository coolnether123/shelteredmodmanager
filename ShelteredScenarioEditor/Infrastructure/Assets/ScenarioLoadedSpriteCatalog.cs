using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Public;
using UnityEngine;

namespace ShelteredScenarioEditor.Infrastructure.Assets
{
    internal sealed class ScenarioLoadedSpriteReference
    {
        public string RuntimeSpriteKey;
        public string SpriteName;
        public string TextureName;
        public Sprite Sprite;
    }

    /// <summary>Editor-only enumeration for sprite authoring pickers.</summary>
    internal static class ScenarioLoadedSpriteCatalog
    {
        public static ScenarioLoadedSpriteReference[] Capture()
        {
            Dictionary<string, ScenarioLoadedSpriteReference> byKey =
                new Dictionary<string, ScenarioLoadedSpriteReference>(StringComparer.OrdinalIgnoreCase);

            AddSprites(byKey, Resources.FindObjectsOfTypeAll<Sprite>());
            try
            {
                AddSprites(byKey, Resources.LoadAll<Sprite>(string.Empty));
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ScenarioLoadedSpriteCatalog.ResourcesLoadAll",
                    "Could not enumerate Resources sprites for the scenario editor: " + ex.Message);
            }

            AddParticleTextures(byKey);
            AddCharacterTextures(byKey);

            List<ScenarioLoadedSpriteReference> result =
                new List<ScenarioLoadedSpriteReference>(byKey.Values);
            result.Sort(Compare);
            return result.ToArray();
        }

        private static void AddSprites(
            Dictionary<string, ScenarioLoadedSpriteReference> byKey,
            Sprite[] sprites)
        {
            for (int i = 0; sprites != null && i < sprites.Length; i++)
                AddSprite(byKey, sprites[i]);
        }

        private static void AddSprite(
            Dictionary<string, ScenarioLoadedSpriteReference> byKey,
            Sprite sprite)
        {
            if (sprite == null || sprite.texture == null)
                return;

            string key = ShelteredScenarioRuntime.CreateRuntimeSpriteKey(sprite);
            if (!string.IsNullOrEmpty(key) && !byKey.ContainsKey(key))
                byKey[key] = CreateSnapshot(key, sprite);
        }

        private static void AddParticleTextures(Dictionary<string, ScenarioLoadedSpriteReference> byKey)
        {
            ParticleSystemRenderer[] renderers = Resources.FindObjectsOfTypeAll<ParticleSystemRenderer>();
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                ParticleSystemRenderer renderer = renderers[i];
                Material material = renderer != null ? renderer.sharedMaterial : null;
                Texture2D texture = material != null ? material.mainTexture as Texture2D : null;
                if (texture != null)
                {
                    string name = !string.IsNullOrEmpty(texture.name)
                        ? texture.name
                        : renderer.name + "_particle";
                    AddTextureSprite(byKey, texture, name);
                }
            }
        }

        private static void AddCharacterTextures(Dictionary<string, ScenarioLoadedSpriteReference> byKey)
        {
            CharacterMeshOptions options = CharacterMeshOptions.instance;
            if ((UnityEngine.Object)options == (UnityEngine.Object)null)
                return;

            List<string> meshIds = options.GetCharacterMeshIds();
            for (int i = 0; meshIds != null && i < meshIds.Count; i++)
            {
                CharacterMeshOptions.CharacterMeshType mesh = options.FindCharacterMesh(meshIds[i]);
                if (mesh == null)
                    continue;
                AddCharacterTextureList(byKey, mesh.m_headTextures);
                AddCharacterTextureList(byKey, mesh.m_torsoTextures);
                AddCharacterTextureList(byKey, mesh.m_legTextures);
            }
        }

        private static void AddCharacterTextureList(
            Dictionary<string, ScenarioLoadedSpriteReference> byKey,
            List<CharacterMeshOptions.CharacterTexture> textures)
        {
            for (int i = 0; textures != null && i < textures.Count; i++)
            {
                CharacterMeshOptions.CharacterTexture entry = textures[i];
                if (entry != null && entry.m_texture != null && !string.IsNullOrEmpty(entry.m_id))
                    AddTextureSprite(byKey, entry.m_texture, entry.m_id);
            }
        }

        private static void AddTextureSprite(
            Dictionary<string, ScenarioLoadedSpriteReference> byKey,
            Texture2D texture,
            string name)
        {
            string key = ShelteredScenarioRuntime.CreateRuntimeSpriteKey(texture, name);
            if (string.IsNullOrEmpty(key) || byKey.ContainsKey(key))
                return;
            Sprite sprite = ShelteredScenarioRuntime.CreateAndRegisterRuntimeSprite(texture, name);
            if (sprite != null)
                byKey[key] = CreateSnapshot(key, sprite);
        }

        private static ScenarioLoadedSpriteReference CreateSnapshot(string key, Sprite sprite)
        {
            return new ScenarioLoadedSpriteReference
            {
                RuntimeSpriteKey = key,
                SpriteName = !string.IsNullOrEmpty(sprite.name) ? sprite.name : "<unnamed>",
                TextureName = sprite.texture != null && !string.IsNullOrEmpty(sprite.texture.name)
                    ? sprite.texture.name
                    : "<texture>",
                Sprite = sprite
            };
        }

        private static int Compare(ScenarioLoadedSpriteReference left, ScenarioLoadedSpriteReference right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left == null) return 1;
            if (right == null) return -1;
            int texture = string.Compare(left.TextureName, right.TextureName, StringComparison.OrdinalIgnoreCase);
            if (texture != 0) return texture;
            int sprite = string.Compare(left.SpriteName, right.SpriteName, StringComparison.OrdinalIgnoreCase);
            return sprite != 0
                ? sprite
                : string.Compare(left.RuntimeSpriteKey, right.RuntimeSpriteKey, StringComparison.OrdinalIgnoreCase);
        }
    }
}
