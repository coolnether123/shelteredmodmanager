using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using UnityEngine;

namespace ShelteredScenarioEditor.Infrastructure.Assets
{
    /// <summary>
    /// Editor-facing adapter over the API's canonical sprite asset pipeline.
    /// </summary>
    internal sealed class ScenarioEditorSpriteAssetService
    {
        public Sprite ResolveSprite(
            ScenarioDefinition definition,
            string packRoot,
            string spriteId,
            string relativePath,
            string runtimeSpriteKey,
            string contextLabel)
        {
            return ShelteredScenarioRuntime.ResolveSpriteAsset(
                definition,
                packRoot,
                spriteId,
                relativePath,
                runtimeSpriteKey,
                contextLabel);
        }

        public void Invalidate()
        {
            ShelteredScenarioRuntime.InvalidateSpriteAssets();
        }
    }
}
