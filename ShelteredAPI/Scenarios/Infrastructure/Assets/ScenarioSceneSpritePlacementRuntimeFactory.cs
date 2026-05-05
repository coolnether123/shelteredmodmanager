using UnityEngine;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Objects;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Infrastructure.Assets{
    internal sealed class ScenarioSceneSpritePlacementRuntimeFactory
    {
        public GameObject Create(GameObject root, SceneSpritePlacement placement, Sprite sprite, int index)
        {
            if (root == null || placement == null || sprite == null)
                return null;

            string scenarioObjectId = ScenarioSceneSpritePlacementIdentity.ResolveScenarioObjectId(placement, index);
            GameObject instance = new GameObject(!string.IsNullOrEmpty(placement.Id) ? placement.Id : scenarioObjectId);
            instance.transform.SetParent(root.transform, false);
            instance.transform.position = ResolveWorldPosition(placement);

            SpriteRenderer renderer = instance.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = placement.SortingOrder;
            if (!string.IsNullOrEmpty(placement.SortingLayerName))
                renderer.sortingLayerName = placement.SortingLayerName;

            ScenarioSceneSpritePlacementMarker marker = instance.AddComponent<ScenarioSceneSpritePlacementMarker>();
            marker.PlacementId = !string.IsNullOrEmpty(placement.Id) ? placement.Id : scenarioObjectId;
            marker.ScenarioObjectId = scenarioObjectId;
            marker.RuntimeBindingKey = ScenarioSceneSpritePlacementIdentity.ResolveRuntimeBindingKey(placement, scenarioObjectId);
            marker.StartState = placement.StartState;
            marker.GridX = placement.GridX.HasValue ? placement.GridX.Value : -1;
            marker.GridY = placement.GridY.HasValue ? placement.GridY.Value : -1;

            instance.SetActive(ShouldRenderAtStart(placement.StartState));
            return instance;
        }

        private static bool ShouldRenderAtStart(ScenarioObjectStartState startState)
        {
            return startState == ScenarioObjectStartState.StartsEnabled
                || startState == ScenarioObjectStartState.StartsDisabled
                || startState == ScenarioObjectStartState.StartsLocked;
        }

        private static Vector3 ResolveWorldPosition(SceneSpritePlacement placement)
        {
            if (placement.SnapToGrid && placement.GridX.HasValue && placement.GridY.HasValue)
                return ScenarioGridSnapService.GetCellCenterWorldPosition(placement.GridX.Value, placement.GridY.Value);

            if (placement.Position == null)
                return Vector3.zero;

            return new Vector3(placement.Position.X, placement.Position.Y, placement.Position.Z);
        }
    }
}
