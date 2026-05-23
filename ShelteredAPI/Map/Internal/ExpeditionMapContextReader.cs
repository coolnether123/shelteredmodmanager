using UnityEngine;

namespace ShelteredAPI.Map.Internal
{
    /// <summary>
    /// The only map-context component that reads live Sheltered runtime singletons.
    /// </summary>
    internal static class ExpeditionMapContextReader
    {
        private const int VanillaMapWidth = 40;
        private const int VanillaMapHeight = 16;

        public static ExpeditionMapContext Capture()
        {
            ExpeditionMap map = ExpeditionMap.Instance;
            ExplorationManager exploration = ExplorationManager.Instance;
            if (map == null)
                return Unavailable("ExpeditionMap instance is unavailable.");
            if (exploration == null)
                return Unavailable("ExplorationManager instance is unavailable.", map);

            int width = map.width;
            int height = map.height;
            bool geometryAvailable = width > 0
                && height > 0
                && exploration.worldWidth > 0
                && exploration.worldHeight > 0;
            bool isValid = geometryAvailable && map.initialised;
            string reason = null;
            if (!geometryAvailable)
                reason = "Expedition map geometry is not initialized.";
            else if (!map.initialised)
                reason = "ExpeditionMap has not finished generation.";

            bool hasHome = false;
            ExpeditionMapWorldPosition homeWorld = new ExpeditionMapWorldPosition();
            ExpeditionMapGridPosition homeGrid = new ExpeditionMapGridPosition();
            if (isValid)
                hasHome = TryGetHomeShelterPosition(map, exploration, out homeWorld, out homeGrid);

            return new ExpeditionMapContext(
                true,
                isValid,
                reason,
                width,
                height,
                VanillaMapWidth,
                VanillaMapHeight,
                geometryAvailable ? width / (float)VanillaMapWidth : 1f,
                isValid,
                1f,
                false,
                map.randomSeed,
                map.randomSeed != 0,
                homeWorld,
                homeGrid,
                hasHome,
                exploration.worldUnitsPerMile,
                isValid && exploration.worldUnitsPerMile > 0f,
                exploration.worldWidth,
                exploration.worldHeight);
        }

        private static ExpeditionMapContext Unavailable(string reason)
        {
            return Unavailable(reason, null);
        }

        private static ExpeditionMapContext Unavailable(string reason, ExpeditionMap map)
        {
            return new ExpeditionMapContext(
                false,
                false,
                reason,
                map != null ? map.width : 0,
                map != null ? map.height : 0,
                VanillaMapWidth,
                VanillaMapHeight,
                1f,
                false,
                1f,
                false,
                map != null ? map.randomSeed : 0,
                map != null && map.randomSeed != 0,
                new ExpeditionMapWorldPosition(),
                new ExpeditionMapGridPosition(),
                false,
                0f,
                false,
                0f,
                0f);
        }

        private static bool TryGetHomeShelterPosition(
            ExpeditionMap map,
            ExplorationManager exploration,
            out ExpeditionMapWorldPosition homeWorld,
            out ExpeditionMapGridPosition homeGrid)
        {
            homeWorld = new ExpeditionMapWorldPosition(0f, 0f);
            if (GameModeManager.instance != null
                && GameModeManager.instance.currentGameMode == GameModeManager.GameMode.Stasis)
            {
                Vector3 mapPosition = GameModeManager.instance.shelterMapWorldPosition;
                Vector2 world = exploration.MapPixelsToWorld(new Vector2(mapPosition.x, mapPosition.y));
                homeWorld = new ExpeditionMapWorldPosition(world.x, world.y);
            }

            ExpeditionMap.GridRef grid = map.WorldPosToGridRef(new Vector2(homeWorld.X, homeWorld.Y));
            if (grid == null)
            {
                homeGrid = new ExpeditionMapGridPosition();
                return false;
            }

            homeGrid = new ExpeditionMapGridPosition(grid.x, grid.y);
            return true;
        }
    }
}
