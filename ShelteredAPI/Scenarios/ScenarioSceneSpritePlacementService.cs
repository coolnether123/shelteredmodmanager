using System;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioSceneSpritePlacementService : IScenarioSceneSpritePlacementEngine
    {
        private readonly IScenarioSpriteAssetResolver _assetResolver;
        private GameObject _runtimeRoot;

        public static ScenarioSceneSpritePlacementService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioSceneSpritePlacementService>(); }
        }

        internal ScenarioSceneSpritePlacementService(IScenarioSpriteAssetResolver assetResolver)
        {
            _assetResolver = assetResolver;
        }

        public int Activate(ScenarioDefinition definition, string scenarioFilePath, ScenarioApplyResult result)
        {
            ClearRuntimeObjects();
            if (definition == null
                || definition.AssetReferences == null
                || definition.AssetReferences.SceneSpritePlacements == null
                || definition.AssetReferences.SceneSpritePlacements.Count == 0)
            {
                return 0;
            }

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

                CreateRuntimePlacement(placement, sprite, i);
                applied++;
            }

            if (result != null)
                result.BunkerChanges += applied;
            return applied;
        }

        public void Clear(string reason)
        {
            ClearRuntimeObjects();
        }

        private void CreateRuntimePlacement(SceneSpritePlacement placement, Sprite sprite, int index)
        {
            GameObject root = GetOrCreateRuntimeRoot();
            if (root == null)
                return;

            GameObject instance = new GameObject(!string.IsNullOrEmpty(placement.Id) ? placement.Id : ("SceneSprite_" + index));
            instance.transform.SetParent(root.transform, false);
            instance.transform.position = ResolveWorldPosition(placement);

            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = placement.SortingOrder;
            if (!string.IsNullOrEmpty(placement.SortingLayerName))
                renderer.sortingLayerName = placement.SortingLayerName;

            ScenarioSceneSpritePlacementMarker marker = instance.AddComponent<ScenarioSceneSpritePlacementMarker>();
            marker.PlacementId = placement.Id;
            marker.GridX = placement.GridX.HasValue ? placement.GridX.Value : -1;
            marker.GridY = placement.GridY.HasValue ? placement.GridY.Value : -1;
        }

        private static Vector3 ResolveWorldPosition(SceneSpritePlacement placement)
        {
            if (placement == null)
                return Vector3.zero;

            if (placement.SnapToGrid && placement.GridX.HasValue && placement.GridY.HasValue)
                return ScenarioGridSnapService.GetCellCenterWorldPosition(placement.GridX.Value, placement.GridY.Value);

            if (placement.Position == null)
                return Vector3.zero;

            return new Vector3(placement.Position.X, placement.Position.Y, placement.Position.Z);
        }

        private GameObject GetOrCreateRuntimeRoot()
        {
            if (_runtimeRoot != null)
                return _runtimeRoot;

            _runtimeRoot = GameObject.Find("ShelteredAPI.SceneSpritePlacements");
            if (_runtimeRoot == null)
                _runtimeRoot = new GameObject("ShelteredAPI.SceneSpritePlacements");
            return _runtimeRoot;
        }

        private void ClearRuntimeObjects()
        {
            if (_runtimeRoot != null)
            {
                UnityEngine.Object.Destroy(_runtimeRoot);
                _runtimeRoot = null;
            }
        }
    }
}
