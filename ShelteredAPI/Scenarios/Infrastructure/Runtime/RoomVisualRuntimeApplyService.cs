using System.Collections.Generic;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal static class RoomVisualRuntimeApplyService
    {
        public static bool ApplyWallEdit(ShelterRoomGrid grid, RoomEdit room, out string message)
        {
            message = null;
            if (room == null)
                return false;

            if (!string.IsNullOrEmpty(room.WallRuntimeSpriteKey)
                && ApplyWallRuntimeSprite(grid, room.GridX, room.GridY, room.WallRuntimeSpriteKey))
            {
                return true;
            }

            if (room.WallSpriteIndex.HasValue)
            {
                if (grid != null && grid.SetWall(room.GridX, room.GridY, room.WallSpriteIndex.Value))
                    return true;

                message = "Failed to set wall sprite at " + room.GridX + "," + room.GridY + ".";
                return false;
            }

            if (!string.IsNullOrEmpty(room.WallRuntimeSpriteKey))
                message = "Failed to resolve wall sprite at " + room.GridX + "," + room.GridY + ".";

            return false;
        }

        public static bool ApplyWireEdit(ShelterRoomGrid grid, List<Sprite> wires, RoomEdit room, out string message)
        {
            message = null;
            if (room == null)
                return false;

            if (!string.IsNullOrEmpty(room.WireRuntimeSpriteKey)
                && ApplyWireRuntimeSprite(grid, wires, room.GridX, room.GridY, room.WireRuntimeSpriteKey))
            {
                return true;
            }

            if (room.WireSpriteIndex.HasValue)
            {
                if (grid != null
                    && wires != null
                    && room.WireSpriteIndex.Value >= 0
                    && room.WireSpriteIndex.Value < wires.Count
                    && grid.SetWiring(room.GridX, room.GridY, wires[room.WireSpriteIndex.Value]))
                {
                    return true;
                }

                message = "Failed to set wiring sprite at " + room.GridX + "," + room.GridY + ".";
                return false;
            }

            if (!string.IsNullOrEmpty(room.WireRuntimeSpriteKey))
                message = "Failed to resolve wiring sprite at " + room.GridX + "," + room.GridY + ".";

            return false;
        }

        public static bool ApplyWallRuntimeSprite(ShelterRoomGrid grid, int gridX, int gridY, string runtimeSpriteKey)
        {
            if (grid == null || string.IsNullOrEmpty(runtimeSpriteKey))
                return false;

            Sprite sprite;
            if (!ScenarioSpriteReferenceLibrary.TryFindLoadedSprite(runtimeSpriteKey, out sprite) || sprite == null)
                return false;

            ShelterRoom room = ResolveRoom(grid, gridX, gridY);
            if (room == null)
                return false;

            int index = EnsureSprite(room.wallSprites, sprite);
            return index >= 0 && room.SetWallSprite(index);
        }

        public static bool ApplyWireRuntimeSprite(ShelterRoomGrid grid, List<Sprite> wires, int gridX, int gridY, string runtimeSpriteKey)
        {
            if (grid == null || wires == null || string.IsNullOrEmpty(runtimeSpriteKey))
                return false;

            Sprite sprite;
            if (!ScenarioSpriteReferenceLibrary.TryFindLoadedSprite(runtimeSpriteKey, out sprite) || sprite == null)
                return false;

            int index = EnsureSprite(wires, sprite);
            return index >= 0 && grid.SetWiring(gridX, gridY, wires[index]);
        }

        private static ShelterRoom ResolveRoom(ShelterRoomGrid grid, int gridX, int gridY)
        {
            ShelterRoomGrid.GridCell cell = grid != null ? grid.GetCell(gridX, gridY) : null;
            if (cell == null || cell.prefab == null)
                return null;

            return cell.prefab.GetComponent<ShelterRoom>();
        }

        private static int EnsureSprite(List<Sprite> sprites, Sprite sprite)
        {
            if (sprites == null || sprite == null)
                return -1;

            for (int i = 0; i < sprites.Count; i++)
            {
                if ((UnityEngine.Object)sprites[i] == (UnityEngine.Object)sprite)
                    return i;
            }

            sprites.Add(sprite);
            return sprites.Count - 1;
        }
    }
}
