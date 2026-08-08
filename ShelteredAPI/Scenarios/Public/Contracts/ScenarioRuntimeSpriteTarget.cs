using System;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using UnityEngine;

namespace ShelteredAPI.Scenarios.Public
{
    /// <summary>
    /// Read-only live component view returned by the runtime sprite-target resolver.
    /// The API owns path traversal and target selection; tooling may inspect the
    /// resolved Unity components without duplicating those policies.
    /// </summary>
    public sealed class ScenarioRuntimeSpriteTarget
    {
        public string TargetPath { get; internal set; }
        public Transform Transform { get; internal set; }
        public ScenarioSpriteTargetComponentKind Kind { get; internal set; }
        public SpriteRenderer SpriteRenderer { get; internal set; }
        public UI2DSprite Ui2DSprite { get; internal set; }
        public ParticleSystemRenderer ParticleRenderer { get; internal set; }

        public bool IsAlive
        {
            get
            {
                if (Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer)
                    return SpriteRenderer != null;
                if (Kind == ScenarioSpriteTargetComponentKind.UI2DSprite)
                    return Ui2DSprite != null;
                if (Kind == ScenarioSpriteTargetComponentKind.ParticleSystemRenderer)
                    return ParticleRenderer != null;
                return false;
            }
        }

        public Sprite CurrentSprite
        {
            get
            {
                if (Kind == ScenarioSpriteTargetComponentKind.SpriteRenderer && SpriteRenderer != null)
                    return SpriteRenderer.sprite;
                if (Kind == ScenarioSpriteTargetComponentKind.UI2DSprite && Ui2DSprite != null)
                    return Ui2DSprite.sprite2D;
                if (Kind == ScenarioSpriteTargetComponentKind.ParticleSystemRenderer && ParticleRenderer != null)
                {
                    Material material = ParticleRenderer.material;
                    if (material == null)
                        material = ParticleRenderer.sharedMaterial;
                    Texture2D texture = material != null ? material.mainTexture as Texture2D : null;
                    return ShelteredScenarioRuntime.CreateAndRegisterRuntimeSprite(texture, texture != null ? texture.name : null);
                }
                return null;
            }
        }

        public string SpriteName
        {
            get
            {
                Sprite sprite = CurrentSprite;
                return sprite != null && !string.IsNullOrEmpty(sprite.name) ? sprite.name : null;
            }
        }

        public string TextureName
        {
            get
            {
                Sprite sprite = CurrentSprite;
                Texture2D texture = sprite != null ? sprite.texture : null;
                return texture != null && !string.IsNullOrEmpty(texture.name) ? texture.name : null;
            }
        }

        internal static ScenarioRuntimeSpriteTarget FromResolvedTarget(ScenarioSpriteRuntimeResolver.ResolvedTarget source)
        {
            if (source == null)
                return null;
            return new ScenarioRuntimeSpriteTarget
            {
                TargetPath = source.TargetPath,
                Transform = source.Transform,
                Kind = source.Kind,
                SpriteRenderer = source.SpriteRenderer,
                Ui2DSprite = source.Ui2DSprite,
                ParticleRenderer = source.ParticleRenderer
            };
        }
    }
}
