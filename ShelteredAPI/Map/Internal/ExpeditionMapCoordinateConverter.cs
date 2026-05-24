using UnityEngine;

namespace ShelteredAPI.Map.Internal
{
    internal static class ExpeditionMapCoordinateConverter
    {
        internal static bool TryWorldToGrid(
            ExpeditionMapWorldPosition worldPosition,
            out ExpeditionMapGridPosition gridPosition)
        {
            gridPosition = new ExpeditionMapGridPosition();
            if (!IsFinite(worldPosition.X) || !IsFinite(worldPosition.Y))
                return false;

            ExpeditionMap map = ExpeditionMap.Instance;
            if (map == null || map.width <= 0 || map.height <= 0 || ExplorationManager.Instance == null)
                return false;

            ExpeditionMap.GridRef grid = map.WorldPosToGridRef(new Vector2(worldPosition.X, worldPosition.Y));
            if (grid == null)
                return false;

            gridPosition = new ExpeditionMapGridPosition(grid.x, grid.y);
            return true;
        }

        internal static bool TryGridToWorldCenter(
            ExpeditionMapGridPosition gridPosition,
            out ExpeditionMapWorldPosition worldPosition)
        {
            worldPosition = new ExpeditionMapWorldPosition();

            ExpeditionMap map = ExpeditionMap.Instance;
            if (map == null || map.width <= 0 || map.height <= 0 || ExplorationManager.Instance == null)
                return false;
            if (gridPosition.X < 0 || gridPosition.Y < 0 || gridPosition.X >= map.width || gridPosition.Y >= map.height)
                return false;

            Vector2 lowerLeft = map.GridRefToWorldPos(new ExpeditionMap.GridRef(gridPosition.X, gridPosition.Y));
            Vector2 next = map.GridRefToWorldPos(new ExpeditionMap.GridRef(
                gridPosition.X < map.width - 1 ? gridPosition.X + 1 : gridPosition.X,
                gridPosition.Y < map.height - 1 ? gridPosition.Y + 1 : gridPosition.Y));

            float cellWidth = gridPosition.X < map.width - 1 ? next.x - lowerLeft.x : 0f;
            float cellHeight = gridPosition.Y < map.height - 1 ? next.y - lowerLeft.y : 0f;
            worldPosition = new ExpeditionMapWorldPosition(
                lowerLeft.x + (cellWidth * 0.5f),
                lowerLeft.y + (cellHeight * 0.5f));
            return IsFinite(worldPosition.X) && IsFinite(worldPosition.Y);
        }

        internal static bool TryWorldToMapPixels(
            ExpeditionMapWorldPosition worldPosition,
            out ExpeditionMapPixelPosition mapPosition)
        {
            mapPosition = new ExpeditionMapPixelPosition();
            if (!IsFinite(worldPosition.X) || !IsFinite(worldPosition.Y))
                return false;

            ExplorationManager exploration = ExplorationManager.Instance;
            if (exploration == null)
                return false;

            mapPosition = new ExpeditionMapPixelPosition(
                exploration.WorldToMapPixelsX(worldPosition.X),
                exploration.WorldToMapPixelsY(worldPosition.Y));
            return IsFinite(mapPosition.X) && IsFinite(mapPosition.Y);
        }

        internal static bool TryMapPixelsToWorld(
            ExpeditionMapPixelPosition mapPosition,
            out ExpeditionMapWorldPosition worldPosition)
        {
            worldPosition = new ExpeditionMapWorldPosition();
            if (!IsFinite(mapPosition.X) || !IsFinite(mapPosition.Y))
                return false;

            ExplorationManager exploration = ExplorationManager.Instance;
            if (exploration == null)
                return false;

            Vector2 world = exploration.MapPixelsToWorld(new Vector2(mapPosition.X, mapPosition.Y));
            worldPosition = new ExpeditionMapWorldPosition(world.x, world.y);
            return IsFinite(worldPosition.X) && IsFinite(worldPosition.Y);
        }

        internal static bool IsFinite(float value)
        {
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }
    }
}
