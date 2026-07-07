using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal static class ScenarioSpriteRuntimeMutationService
    {
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
