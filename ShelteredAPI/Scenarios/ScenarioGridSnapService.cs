using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioGridSnapService
    {
        public static bool TryGetCell(Vector3 worldPosition, out int gridX, out int gridY)
        {
            gridX = -1;
            gridY = -1;

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            return grid != null && grid.isInitialized && grid.WorldCoordsToCellCoords(worldPosition, out gridX, out gridY);
        }

        public static bool TrySnapWorldPosition(Vector3 worldPosition, out int gridX, out int gridY, out Vector3 snappedWorldPosition)
        {
            snappedWorldPosition = worldPosition;
            if (!TryGetCell(worldPosition, out gridX, out gridY))
                return false;

            snappedWorldPosition = GetCellCenterWorldPosition(gridX, gridY);
            return true;
        }

        public static Vector3 GetCellCenterWorldPosition(int gridX, int gridY)
        {
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null)
                return Vector3.zero;

            Vector3 origin = grid.transform.position + grid.CellCoordsToWorldCoords(gridX, gridY);
            origin.x += grid.grid_cell_width * 0.5f;
            origin.y -= grid.grid_cell_height * 0.5f;
            return origin;
        }
    }
}
