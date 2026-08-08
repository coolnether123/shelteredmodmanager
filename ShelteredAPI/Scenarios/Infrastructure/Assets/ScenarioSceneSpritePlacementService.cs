using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal sealed class ScenarioSceneSpritePlacementService : IScenarioSceneSpritePlacementEngine
    {
        private readonly IScenarioSpriteAssetResolver _assetResolver;
        private readonly ScenarioSceneSpritePlacementRoot _placementRoot;
        private readonly ScenarioSceneSpritePlacementRuntimeFactory _runtimeFactory;

        public static ScenarioSceneSpritePlacementService Instance
        {
            get { return ScenarioRuntimeCompositionRoot.Resolve<ScenarioSceneSpritePlacementService>(); }
        }

        internal ScenarioSceneSpritePlacementService(
            IScenarioSpriteAssetResolver assetResolver,
            ScenarioSceneSpritePlacementRoot placementRoot,
            ScenarioSceneSpritePlacementRuntimeFactory runtimeFactory)
        {
            _assetResolver = assetResolver;
            _placementRoot = placementRoot;
            _runtimeFactory = runtimeFactory;
        }

        public int Activate(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result)
        {
            if (_placementRoot != null)
                _placementRoot.Clear();

            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.SceneSpritePlacements == null
                || definition.AssetReferences.SceneSpritePlacements.Count == 0)
            {
                return 0;
            }

            GameObject root = _placementRoot != null ? _placementRoot.CreateFresh() : null;
            string packRoot = !string.IsNullOrEmpty(scenarioFilePath) ? System.IO.Path.GetDirectoryName(scenarioFilePath) : null;
            int applied = 0;
            for (int i = 0; i < definition.AssetReferences.SceneSpritePlacements.Count; i++)
            {
                SceneSpritePlacement placement = definition.AssetReferences.SceneSpritePlacements[i];
                if (placement == null)
                    continue;

                Sprite sprite = _assetResolver.ResolveSprite(
                    definition,
                    packRoot,
                    placement.SpriteId,
                    placement.RelativePath,
                    placement.RuntimeSpriteKey,
                    "scene sprite placement '" + (placement.Id ?? ("#" + i)) + "'");
                if (sprite == null)
                    continue;

                if (_runtimeFactory == null || _runtimeFactory.Create(root, placement, sprite, i) == null)
                    continue;

                applied++;
            }

            if (result != null)
                result.BunkerChanges += applied;
            return applied;
        }

        public void Clear(string reason)
        {
            if (_placementRoot != null)
                _placementRoot.Clear();
        }
    }
}
