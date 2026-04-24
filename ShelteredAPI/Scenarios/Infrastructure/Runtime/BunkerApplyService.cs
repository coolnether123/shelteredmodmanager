using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class BunkerApplyService
    {
        private static readonly FieldInfo ShelterRoomGridWiresSpritesField = typeof(ShelterRoomGrid).GetField("wiresSprites", BindingFlags.NonPublic | BindingFlags.Instance);

        public void Apply(ScenarioDefinition definition, ScenarioApplyResult result)
        {
            if (definition == null || definition.BunkerEdits == null)
                return;

            bool hasRoomChanges = definition.BunkerEdits.RoomChanges != null && definition.BunkerEdits.RoomChanges.Count > 0;
            bool hasObjectPlacements = definition.BunkerEdits.ObjectPlacements != null && definition.BunkerEdits.ObjectPlacements.Count > 0;
            if (!hasRoomChanges && !hasObjectPlacements)
                return;

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.isInitialized)
            {
                result.AddMessage("ShelterRoomGrid is not ready; bunker changes skipped.");
                return;
            }

            ApplyObjectPlacements(definition.BunkerEdits.ObjectPlacements, result);

            List<Sprite> wires = ShelterRoomGridWiresSpritesField != null ? ShelterRoomGridWiresSpritesField.GetValue(grid) as List<Sprite> : null;
            for (int i = 0; definition.BunkerEdits.RoomChanges != null && i < definition.BunkerEdits.RoomChanges.Count; i++)
            {
                RoomEdit room = definition.BunkerEdits.RoomChanges[i];
                if (room == null)
                    continue;

                if (room.WallSpriteIndex.HasValue)
                {
                    if (grid.SetWall(room.GridX, room.GridY, room.WallSpriteIndex.Value))
                        result.BunkerChanges++;
                    else
                        result.AddMessage("Failed to set wall sprite at " + room.GridX + "," + room.GridY + ".");
                }

                if (room.WireSpriteIndex.HasValue)
                {
                    if (wires != null
                        && room.WireSpriteIndex.Value >= 0
                        && room.WireSpriteIndex.Value < wires.Count
                        && grid.SetWiring(room.GridX, room.GridY, wires[room.WireSpriteIndex.Value]))
                    {
                        result.BunkerChanges++;
                    }
                    else
                    {
                        result.AddMessage("Failed to set wiring sprite at " + room.GridX + "," + room.GridY + ".");
                    }
                }
            }
        }

        private static void ApplyObjectPlacements(List<ObjectPlacement> placements, ScenarioApplyResult result)
        {
            if (placements == null || placements.Count == 0)
                return;

            ApplyStructurePlacements(placements, ScenarioPlacementDefinitionKind.Room, result);
            ApplyStructurePlacements(placements, ScenarioPlacementDefinitionKind.Ladder, result);
            ApplyStructurePlacements(placements, ScenarioPlacementDefinitionKind.RoomLight, result);
            ApplyStandardObjectPlacements(placements, result);
        }

        private static void ApplyStructurePlacements(
            List<ObjectPlacement> placements,
            ScenarioPlacementDefinitionKind targetKind,
            ScenarioApplyResult result)
        {
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.isInitialized || placements == null)
                return;

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                ScenarioPlacementDefinitionKind kind;
                if (placement == null
                    || !ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out kind)
                    || kind != targetKind)
                {
                    continue;
                }

                switch (kind)
                {
                    case ScenarioPlacementDefinitionKind.Room:
                        ApplyRoomPlacement(grid, placement, i, result);
                        break;
                    case ScenarioPlacementDefinitionKind.Ladder:
                        ApplyLadderPlacement(grid, placement, i, result);
                        break;
                    case ScenarioPlacementDefinitionKind.RoomLight:
                        ApplyRoomLightPlacement(grid, placement, i, result);
                        break;
                }
            }
        }

        private static void ApplyStandardObjectPlacements(List<ObjectPlacement> placements, ScenarioApplyResult result)
        {
            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
            {
                result.AddMessage("ObjectManager is not ready; standard object placements skipped.");
                return;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                if (placement == null || ScenarioPlacementDefinitions.IsSpecialDefinition(placement.DefinitionReference))
                    continue;

                if (!string.IsNullOrEmpty(placement.PrefabReference))
                {
                    result.AddMessage("Object placement #" + i + " uses PrefabReference '" + placement.PrefabReference
                        + "' and is deferred because direct prefab-path instantiation is not safe for live saves.");
                    continue;
                }

                ObjectManager.ObjectType objectType;
                if (!TryParseObjectType(placement.DefinitionReference, out objectType))
                {
                    result.AddMessage("Object placement #" + i + " has unknown DefinitionReference: " + (placement.DefinitionReference ?? string.Empty));
                    continue;
                }

                if (!manager.HasPrefab(objectType))
                {
                    result.AddMessage("Object placement #" + i + " skipped because ObjectManager has no prefab for " + objectType + ".");
                    continue;
                }

                int level = GetIntProperty(placement.CustomProperties, "level", 1);
                bool lockDeconstruct = GetBoolProperty(placement.CustomProperties, "lockDeconstruct", false);
                bool movable = GetBoolProperty(placement.CustomProperties, "movable", true);
                Vector2 position = new Vector2(
                    placement.Position != null ? placement.Position.X : 0f,
                    placement.Position != null ? placement.Position.Y : 0f);

                Obj_Base spawned = manager.SpawnObject(objectType, level, position, lockDeconstruct, movable);
                if (spawned == null)
                {
                    result.AddMessage("Object placement #" + i + " failed to spawn " + objectType + " at " + position.x + "," + position.y + ".");
                    continue;
                }

                if (placement.Rotation != null)
                    spawned.transform.eulerAngles = new Vector3(placement.Rotation.X, placement.Rotation.Y, placement.Rotation.Z);

                result.BunkerChanges++;
            }
        }

        private static void ApplyRoomPlacement(ShelterRoomGrid grid, ObjectPlacement placement, int index, ScenarioApplyResult result)
        {
            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                result.AddMessage("Room placement #" + index + " could not resolve a shelter cell.");
                return;
            }

            if (!IsValidCell(grid, gridX, gridY))
            {
                result.AddMessage("Room placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return;
            }

            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            ShelterRoomGrid.CellType cellType = string.Equals(placement.DefinitionReference, ScenarioPlacementDefinitions.RoomTop, StringComparison.OrdinalIgnoreCase)
                ? ShelterRoomGrid.CellType.RoomTop
                : ShelterRoomGrid.CellType.Room;
            if (cell != null && cell.type == cellType)
                return;

            if (grid.SetCellType(gridX, gridY, cellType))
                result.BunkerChanges++;
            else
                result.AddMessage("Room placement #" + index + " failed at " + gridX + "," + gridY + ".");
        }

        private static void ApplyLadderPlacement(ShelterRoomGrid grid, ObjectPlacement placement, int index, ScenarioApplyResult result)
        {
            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                result.AddMessage("Ladder placement #" + index + " could not resolve a shelter cell.");
                return;
            }

            if (!IsValidCell(grid, gridX, gridY))
            {
                result.AddMessage("Ladder placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return;
            }

            if (grid.HasLadder(gridX, gridY))
                return;

            float horizontalPos = ResolveHorizontalPosition(grid, placement, gridX);
            if (grid.AddLadder(gridX, gridY, horizontalPos) != null)
                result.BunkerChanges++;
            else
                result.AddMessage("Ladder placement #" + index + " failed at " + gridX + "," + gridY + ".");
        }

        private static void ApplyRoomLightPlacement(ShelterRoomGrid grid, ObjectPlacement placement, int index, ScenarioApplyResult result)
        {
            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                result.AddMessage("Room light placement #" + index + " could not resolve a shelter cell.");
                return;
            }

            if (!IsValidCell(grid, gridX, gridY))
            {
                result.AddMessage("Room light placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return;
            }

            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            if (cell != null && (UnityEngine.Object)cell.lightObject != (UnityEngine.Object)null)
                return;

            if (grid.AddLight(gridX, gridY))
                result.BunkerChanges++;
            else
                result.AddMessage("Room light placement #" + index + " failed at " + gridX + "," + gridY + ".");
        }

        private static bool TryResolveGridCoordinates(ShelterRoomGrid grid, ObjectPlacement placement, out int gridX, out int gridY)
        {
            gridX = GetIntProperty(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridX, int.MinValue);
            gridY = GetIntProperty(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridY, int.MinValue);
            if (gridX != int.MinValue && gridY != int.MinValue)
                return true;

            Vector3 worldPosition = new Vector3(
                placement != null && placement.Position != null ? placement.Position.X : 0f,
                placement != null && placement.Position != null ? placement.Position.Y : 0f,
                placement != null && placement.Position != null ? placement.Position.Z : 0f);
            return grid != null && grid.WorldCoordsToCellCoords(worldPosition, out gridX, out gridY);
        }

        private static bool IsValidCell(ShelterRoomGrid grid, int gridX, int gridY)
        {
            return grid != null
                && gridX >= 0
                && gridX < grid.grid_width
                && gridY >= 0
                && gridY < grid.grid_height;
        }

        private static float ResolveHorizontalPosition(ShelterRoomGrid grid, ObjectPlacement placement, int gridX)
        {
            string storedValue = GetProperty(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyHorizontalPos);
            float parsedValue;
            if (!string.IsNullOrEmpty(storedValue)
                && float.TryParse(storedValue, NumberStyles.Float, CultureInfo.InvariantCulture, out parsedValue))
            {
                return Mathf.Clamp01(parsedValue);
            }

            float cellLeft = gridX * grid.grid_cell_width;
            float cellRight = cellLeft + grid.grid_cell_width;
            float width = cellRight - cellLeft;
            if (width <= 0f || placement == null || placement.Position == null)
                return 0.5f;

            return Mathf.Clamp01((placement.Position.X - cellLeft) / width);
        }

        private static bool TryParseObjectType(string value, out ObjectManager.ObjectType objectType)
        {
            objectType = ObjectManager.ObjectType.Undefined;
            if (string.IsNullOrEmpty(value))
                return false;

            try
            {
                objectType = (ObjectManager.ObjectType)Enum.Parse(typeof(ObjectManager.ObjectType), value, true);
                return objectType != ObjectManager.ObjectType.Undefined && objectType != ObjectManager.ObjectType.Max;
            }
            catch
            {
                return false;
            }
        }

        private static string GetProperty(List<ScenarioProperty> properties, string key)
        {
            for (int i = 0; properties != null && i < properties.Count; i++)
            {
                ScenarioProperty property = properties[i];
                if (property != null && string.Equals(property.Key, key, StringComparison.OrdinalIgnoreCase))
                    return property.Value;
            }

            return null;
        }

        private static int GetIntProperty(List<ScenarioProperty> properties, string key, int fallback)
        {
            int value;
            return int.TryParse(GetProperty(properties, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out value) ? value : fallback;
        }

        private static bool GetBoolProperty(List<ScenarioProperty> properties, string key, bool fallback)
        {
            bool value;
            return bool.TryParse(GetProperty(properties, key), out value) ? value : fallback;
        }
    }
}
