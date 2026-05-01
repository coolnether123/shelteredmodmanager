using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ModAPI.Core;
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
                AddBunkerMessage(result, "ShelterRoomGrid is not ready; bunker changes skipped.");
                return;
            }

            ApplyObjectPlacements(definition.BunkerEdits.ObjectPlacements, result);

            List<Sprite> wires = ShelterRoomGridWiresSpritesField != null ? ShelterRoomGridWiresSpritesField.GetValue(grid) as List<Sprite> : null;
            for (int i = 0; definition.BunkerEdits.RoomChanges != null && i < definition.BunkerEdits.RoomChanges.Count; i++)
            {
                RoomEdit room = definition.BunkerEdits.RoomChanges[i];
                if (room == null)
                    continue;

                string wallMessage;
                try
                {
                    if (RoomVisualRuntimeApplyService.ApplyWallEdit(grid, room, out wallMessage))
                        result.BunkerChanges++;
                    else
                        AddBunkerMessage(result, wallMessage);
                }
                catch (Exception ex)
                {
                    AddBunkerMessage(result, "Failed to set wall sprite at " + room.GridX + "," + room.GridY + ": " + ex.Message);
                }

                string wireMessage;
                try
                {
                    if (RoomVisualRuntimeApplyService.ApplyWireEdit(grid, wires, room, out wireMessage))
                        result.BunkerChanges++;
                    else
                        AddBunkerMessage(result, wireMessage);
                }
                catch (Exception ex)
                {
                    AddBunkerMessage(result, "Failed to set wiring sprite at " + room.GridX + "," + room.GridY + ": " + ex.Message);
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

                try
                {
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
                catch (Exception ex)
                {
                    AddBunkerMessage(result, "Structure placement #" + i + " failed during apply: " + ex.Message);
                }
            }
        }

        private static void ApplyStandardObjectPlacements(List<ObjectPlacement> placements, ScenarioApplyResult result)
        {
            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
            {
                AddBunkerMessage(result, "ObjectManager is not ready; standard object placements skipped.");
                return;
            }

            for (int i = 0; i < placements.Count; i++)
            {
                ObjectPlacement placement = placements[i];
                if (placement == null || ScenarioPlacementDefinitions.IsSpecialDefinition(placement.DefinitionReference))
                    continue;

                if (!string.IsNullOrEmpty(placement.PrefabReference))
                {
                    AddBunkerMessage(result, "Object placement #" + i + " uses PrefabReference '" + placement.PrefabReference
                        + "' and is deferred because direct prefab-path instantiation is not safe for live saves.");
                    continue;
                }

                ObjectManager.ObjectType objectType;
                if (!TryParseObjectType(placement.DefinitionReference, out objectType))
                {
                    AddBunkerMessage(result, "Object placement #" + i + " has unknown DefinitionReference: " + (placement.DefinitionReference ?? string.Empty));
                    continue;
                }

                if (!manager.HasPrefab(objectType))
                {
                    AddBunkerMessage(result, "Object placement #" + i + " skipped because ObjectManager has no prefab for " + objectType + ".");
                    continue;
                }

                int level = ScenarioPropertyBag.GetInt(placement.CustomProperties, "level", 1);
                bool lockDeconstruct = ScenarioPropertyBag.GetBool(placement.CustomProperties, "lockDeconstruct", false);
                bool movable = ScenarioPropertyBag.GetBool(placement.CustomProperties, "movable", true);
                Vector2 position = new Vector2(
                    placement.Position != null ? placement.Position.X : 0f,
                    placement.Position != null ? placement.Position.Y : 0f);

                Obj_Base spawned;
                try
                {
                    spawned = manager.SpawnObject(objectType, level, position, lockDeconstruct, movable);
                }
                catch (Exception ex)
                {
                    AddBunkerMessage(result, "Object placement #" + i + " failed to spawn " + objectType + ": " + ex.Message);
                    continue;
                }

                if (spawned == null)
                {
                    AddBunkerMessage(result, "Object placement #" + i + " failed to spawn " + objectType + " at " + position.x + "," + position.y + ".");
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
                AddBunkerMessage(result, "Room placement #" + index + " could not resolve a shelter cell.");
                return;
            }

            if (!IsValidCell(grid, gridX, gridY))
            {
                AddBunkerMessage(result, "Room placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
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
                AddBunkerMessage(result, "Room placement #" + index + " failed at " + gridX + "," + gridY + ".");
        }

        private static void ApplyLadderPlacement(ShelterRoomGrid grid, ObjectPlacement placement, int index, ScenarioApplyResult result)
        {
            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                AddBunkerMessage(result, "Ladder placement #" + index + " could not resolve a shelter cell.");
                return;
            }

            if (!IsValidCell(grid, gridX, gridY))
            {
                AddBunkerMessage(result, "Ladder placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return;
            }

            if (grid.HasLadder(gridX, gridY))
                return;

            float horizontalPos = ResolveHorizontalPosition(grid, placement, gridX);
            if (grid.AddLadder(gridX, gridY, horizontalPos) != null)
                result.BunkerChanges++;
            else
                AddBunkerMessage(result, "Ladder placement #" + index + " failed at " + gridX + "," + gridY + ".");
        }

        private static void ApplyRoomLightPlacement(ShelterRoomGrid grid, ObjectPlacement placement, int index, ScenarioApplyResult result)
        {
            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                AddBunkerMessage(result, "Room light placement #" + index + " could not resolve a shelter cell.");
                return;
            }

            if (!IsValidCell(grid, gridX, gridY))
            {
                AddBunkerMessage(result, "Room light placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return;
            }

            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            if (cell != null && (UnityEngine.Object)cell.lightObject != (UnityEngine.Object)null)
                return;

            if (grid.AddLight(gridX, gridY))
                result.BunkerChanges++;
            else
                AddBunkerMessage(result, "Room light placement #" + index + " failed at " + gridX + "," + gridY + ".");
        }

        private static void AddBunkerMessage(ScenarioApplyResult result, string message)
        {
            if (result != null)
                result.AddMessage(message);
            if (!string.IsNullOrEmpty(message))
                MMLog.WriteWarning("[BunkerApplyService] " + message);
        }

        private static bool TryResolveGridCoordinates(ShelterRoomGrid grid, ObjectPlacement placement, out int gridX, out int gridY)
        {
            gridX = ScenarioPropertyBag.GetInt(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridX, int.MinValue);
            gridY = ScenarioPropertyBag.GetInt(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyGridY, int.MinValue);
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
            string storedValue = ScenarioPropertyBag.GetString(placement != null ? placement.CustomProperties : null, ScenarioPlacementDefinitions.PropertyHorizontalPos);
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

    }
}
