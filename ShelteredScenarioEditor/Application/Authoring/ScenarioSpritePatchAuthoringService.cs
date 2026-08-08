using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Assets;
using ShelteredScenarioEditor.Infrastructure.Assets;
namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioSpritePatchAuthoringService
    {
        private readonly SpritePatchBuilder _spritePatchBuilder;

        public ScenarioSpritePatchAuthoringService(SpritePatchBuilder spritePatchBuilder)
        {
            _spritePatchBuilder = spritePatchBuilder;
        }

        public string UpsertPatchSpriteAsset(
            ScenarioDefinition definition,
            string spriteId,
            string displayName,
            string baseSpriteId,
            string baseRelativePath,
            string baseRuntimeSpriteKey,
            Texture2D baselineTexture,
            Texture2D editedTexture,
            out string message)
        {
            message = null;
            if (definition == null || string.IsNullOrEmpty(spriteId))
            {
                message = "No scenario sprite asset was available for the patch.";
                return null;
            }

            if (definition.AssetReferences == null)
                definition.AssetReferences = new AssetReferencesDefinition();

            if (string.IsNullOrEmpty(baseSpriteId)
                && string.IsNullOrEmpty(baseRelativePath)
                && string.IsNullOrEmpty(baseRuntimeSpriteKey))
            {
                message = "Custom sprite patches must reference an existing runtime or scenario sprite; baseline texture export is disabled.";
                return null;
            }

            string patchId = spriteId + ".patch";
            SpritePatchDefinition patch = _spritePatchBuilder.Build(
                patchId,
                string.IsNullOrEmpty(displayName) ? spriteId : displayName,
                baseSpriteId,
                baseRelativePath,
                baseRuntimeSpriteKey,
                baselineTexture,
                editedTexture);
            if (patch == null)
            {
                message = "Custom sprite patch could not be generated.";
                return null;
            }

            UpsertPatchDefinition(definition, patch);
            UpsertCustomSpriteReference(definition, spriteId, patchId);
            return patchId;
        }

        private static void UpsertCustomSpriteReference(ScenarioDefinition definition, string spriteId, string patchId)
        {
            if (definition == null || definition.AssetReferences == null || string.IsNullOrEmpty(spriteId))
                return;

            for (int i = 0; i < definition.AssetReferences.CustomSprites.Count; i++)
            {
                SpriteRef sprite = definition.AssetReferences.CustomSprites[i];
                if (sprite != null && string.Equals(sprite.Id, spriteId, System.StringComparison.OrdinalIgnoreCase))
                {
                    sprite.RelativePath = null;
                    sprite.PatchId = patchId;
                    return;
                }
            }

            definition.AssetReferences.CustomSprites.Add(new SpriteRef
            {
                Id = spriteId,
                PatchId = patchId
            });
        }

        private static void UpsertPatchDefinition(ScenarioDefinition definition, SpritePatchDefinition patch)
        {
            if (definition == null || definition.AssetReferences == null || patch == null)
                return;

            for (int i = 0; i < definition.AssetReferences.SpritePatches.Count; i++)
            {
                SpritePatchDefinition existing = definition.AssetReferences.SpritePatches[i];
                if (existing != null && string.Equals(existing.Id, patch.Id, System.StringComparison.OrdinalIgnoreCase))
                {
                    definition.AssetReferences.SpritePatches[i] = patch;
                    return;
                }
            }

            definition.AssetReferences.SpritePatches.Add(patch);
        }
    }
}
