using UnityEngine;

namespace ShelteredAPI.Map.Internal
{
    internal static class HomeShelterPositionResolver
    {
        public static bool TryResolveWorldPosition(ExplorationManager exploration, out Vector2 worldPosition)
        {
            if (TryResolveRegisteredWorldPosition(out worldPosition))
                return true;

            return TryResolveVanillaWorldPosition(exploration, out worldPosition);
        }

        public static bool TryResolveMapPixels(ExplorationManager exploration, out Vector2 mapPixels)
        {
            mapPixels = Vector2.zero;

            HomeShelterPositionSnapshot snapshot;
            if (HomeShelterPositionRegistry.TryGetActive(out snapshot) && snapshot.HasMapPosition)
            {
                mapPixels = new Vector2(snapshot.MapPosition.X, snapshot.MapPosition.Y);
                return IsUsable(mapPixels);
            }

            if (GameModeManager.instance == null)
                return false;

            Vector3 vanilla = GameModeManager.instance.shelterMapWorldPosition;
            mapPixels = new Vector2(vanilla.x, vanilla.y);
            return IsUsable(mapPixels);
        }

        private static bool TryResolveRegisteredWorldPosition(out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;

            HomeShelterPositionSnapshot snapshot;
            if (!HomeShelterPositionRegistry.TryGetActive(out snapshot) || !snapshot.HasWorldPosition)
                return false;

            worldPosition = new Vector2(snapshot.WorldPosition.X, snapshot.WorldPosition.Y);
            return IsUsable(worldPosition);
        }

        private static bool TryResolveVanillaWorldPosition(ExplorationManager exploration, out Vector2 worldPosition)
        {
            worldPosition = Vector2.zero;
            if (GameModeManager.instance == null || exploration == null)
                return false;

            Vector3 mapPosition = GameModeManager.instance.shelterMapWorldPosition;
            worldPosition = exploration.MapPixelsToWorld(new Vector2(mapPosition.x, mapPosition.y));
            return IsUsable(worldPosition);
        }

        private static bool IsUsable(Vector2 value)
        {
            return !float.IsNaN(value.x)
                && !float.IsNaN(value.y)
                && !float.IsInfinity(value.x)
                && !float.IsInfinity(value.y);
        }
    }
}
