using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
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
                            ApplyRoomPlacement(grid, placement, i, result, false);
                            break;
                        case ScenarioPlacementDefinitionKind.Ladder:
                            ApplyLadderPlacement(grid, placement, i, result, false);
                            break;
                        case ScenarioPlacementDefinitionKind.RoomLight:
                            ApplyRoomLightPlacement(grid, placement, i, result, false);
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

                ApplyStandardObjectPlacement(placement, i, result, false);
            }
        }

        internal static bool TryMaterializePlacement(ScenarioDefinition definition, string objectId, ScenarioApplyResult result)
        {
            if (definition == null || definition.BunkerEdits == null || definition.BunkerEdits.ObjectPlacements == null || string.IsNullOrEmpty(objectId))
                return false;

            for (int i = 0; i < definition.BunkerEdits.ObjectPlacements.Count; i++)
            {
                ObjectPlacement placement = definition.BunkerEdits.ObjectPlacements[i];
                if (placement == null
                    || (!string.Equals(placement.ScenarioObjectId, objectId, StringComparison.OrdinalIgnoreCase)
                        && !string.Equals(placement.RuntimeBindingKey, objectId, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                return TryMaterializePlacement(placement, i, result, true);
            }

            return false;
        }

        internal static bool TryMaterializePlacement(ObjectPlacement placement, int index, ScenarioApplyResult result, bool forceMaterialize)
        {
            if (placement == null)
                return false;

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            ScenarioPlacementDefinitionKind kind;
            if (ScenarioPlacementDefinitions.TryParseSpecialKind(placement.DefinitionReference, out kind))
            {
                if (grid == null || !grid.isInitialized)
                    return false;

                switch (kind)
                {
                    case ScenarioPlacementDefinitionKind.Room:
                        return ApplyRoomPlacement(grid, placement, index, result, forceMaterialize);
                    case ScenarioPlacementDefinitionKind.Ladder:
                        return ApplyLadderPlacement(grid, placement, index, result, forceMaterialize);
                    case ScenarioPlacementDefinitionKind.RoomLight:
                        return ApplyRoomLightPlacement(grid, placement, index, result, forceMaterialize);
                }
            }

            return ApplyStandardObjectPlacement(placement, index, result, forceMaterialize) != null;
        }

        private static Obj_Base ApplyStandardObjectPlacement(ObjectPlacement placement, int index, ScenarioApplyResult result, bool forceMaterialize)
        {
            if (placement == null)
                return null;

            if (!forceMaterialize && !ScenarioObjectStartStateApplyService.ShouldMaterializeAtStart(placement))
                return null;

            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
            {
                AddBunkerMessage(result, "ObjectManager is not ready; object placement #" + index + " skipped.");
                return null;
            }

            if (!string.IsNullOrEmpty(placement.PrefabReference))
            {
                AddBunkerMessage(result, "Object placement #" + index + " uses PrefabReference '" + placement.PrefabReference
                    + "' and is deferred because direct prefab-path instantiation is not safe for live saves.");
                return null;
            }

            ObjectManager.ObjectType objectType;
            if (!TryParseObjectType(placement.DefinitionReference, out objectType))
            {
                AddBunkerMessage(result, "Object placement #" + index + " has unknown DefinitionReference: " + (placement.DefinitionReference ?? string.Empty));
                return null;
            }

            if (!manager.HasPrefab(objectType))
            {
                AddBunkerMessage(result, "Object placement #" + index + " skipped because ObjectManager has no prefab for " + objectType + ".");
                return null;
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
                AddBunkerMessage(result, "Object placement #" + index + " failed to spawn " + objectType + ": " + ex.Message);
                return null;
            }

            if (spawned == null)
            {
                AddBunkerMessage(result, "Object placement #" + index + " failed to spawn " + objectType + " at " + position.x + "," + position.y + ".");
                return null;
            }

            if (placement.Rotation != null)
                spawned.transform.eulerAngles = new Vector3(placement.Rotation.X, placement.Rotation.Y, placement.Rotation.Z);

            ScenarioObjectPlacementRuntimeBinding.Attach(spawned.gameObject, placement, spawned, index);
            ScenarioObjectStatePropertyService.Apply(spawned, placement);
            if (forceMaterialize)
            {
                spawned.EnableObject();
                spawned.selectable = true;
                spawned.gameObject.SetActive(true);
            }
            else
            {
                ScenarioObjectStartStateApplyService.ApplyToObject(spawned, placement, result);
            }
            if (result != null)
                result.BunkerChanges++;
            return spawned;
        }

        private static bool ApplyRoomPlacement(ShelterRoomGrid grid, ObjectPlacement placement, int index, ScenarioApplyResult result, bool forceMaterialize)
        {
            if (!forceMaterialize && !ScenarioObjectStartStateApplyService.ShouldMaterializeStructureAtStart(placement))
                return false;

            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                AddBunkerMessage(result, "Room placement #" + index + " could not resolve a shelter cell.");
                return false;
            }

            if (!IsValidCell(grid, gridX, gridY))
            {
                AddBunkerMessage(result, "Room placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return false;
            }

            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            ShelterRoomGrid.CellType cellType = string.Equals(placement.DefinitionReference, ScenarioPlacementDefinitions.RoomTop, StringComparison.OrdinalIgnoreCase)
                ? ShelterRoomGrid.CellType.RoomTop
                : ShelterRoomGrid.CellType.Room;
            if (cell != null && cell.type == cellType)
                return true;

            if (grid.SetCellType(gridX, gridY, cellType))
            {
                if (result != null)
                    result.BunkerChanges++;
                cell = grid.GetCell(gridX, gridY);
                if (cell != null && cell.prefab != null)
                {
                    ScenarioObjectPlacementRuntimeBinding.Attach(cell.prefab, placement, null, index);
                    if (!forceMaterialize)
                        ScenarioObjectStartStateApplyService.ApplyToStructure(cell.prefab, placement);
                }
                return true;
            }
            else
                AddBunkerMessage(result, "Room placement #" + index + " failed at " + gridX + "," + gridY + ".");
            return false;
        }

        private static bool ApplyLadderPlacement(ShelterRoomGrid grid, ObjectPlacement placement, int index, ScenarioApplyResult result, bool forceMaterialize)
        {
            if (!forceMaterialize && !ScenarioObjectStartStateApplyService.ShouldMaterializeStructureAtStart(placement))
                return false;

            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                AddBunkerMessage(result, "Ladder placement #" + index + " could not resolve a shelter cell.");
                return false;
            }

            if (!IsValidCell(grid, gridX, gridY))
            {
                AddBunkerMessage(result, "Ladder placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return false;
            }

            if (grid.HasLadder(gridX, gridY))
                return true;

            float horizontalPos = ResolveHorizontalPosition(grid, placement, gridX);
            ShelterLadder ladder = grid.AddLadder(gridX, gridY, horizontalPos);
            if (ladder != null)
            {
                if (result != null)
                    result.BunkerChanges++;
                ScenarioObjectPlacementRuntimeBinding.Attach(ladder.gameObject, placement, null, index);
                if (!forceMaterialize)
                    ScenarioObjectStartStateApplyService.ApplyToStructure(ladder.gameObject, placement);
                return true;
            }
            else
                AddBunkerMessage(result, "Ladder placement #" + index + " failed at " + gridX + "," + gridY + ".");
            return false;
        }

        private static bool ApplyRoomLightPlacement(ShelterRoomGrid grid, ObjectPlacement placement, int index, ScenarioApplyResult result, bool forceMaterialize)
        {
            if (!forceMaterialize && !ScenarioObjectStartStateApplyService.ShouldMaterializeStructureAtStart(placement))
                return false;

            int gridX;
            int gridY;
            if (!TryResolveGridCoordinates(grid, placement, out gridX, out gridY))
            {
                AddBunkerMessage(result, "Room light placement #" + index + " could not resolve a shelter cell.");
                return false;
            }

            if (!IsValidCell(grid, gridX, gridY))
            {
                AddBunkerMessage(result, "Room light placement #" + index + " is outside the shelter grid at " + gridX + "," + gridY + ".");
                return false;
            }

            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            if (cell != null && (UnityEngine.Object)cell.lightObject != (UnityEngine.Object)null)
                return true;

            if (grid.AddLight(gridX, gridY))
            {
                if (result != null)
                    result.BunkerChanges++;
                cell = grid.GetCell(gridX, gridY);
                if (cell != null && (UnityEngine.Object)cell.lightObject != (UnityEngine.Object)null)
                {
                    ScenarioObjectPlacementRuntimeBinding.Attach(cell.lightObject.gameObject, placement, cell.lightObject, index);
                    if (forceMaterialize)
                    {
                        cell.lightObject.EnableObject();
                        cell.lightObject.selectable = true;
                        cell.lightObject.gameObject.SetActive(true);
                    }
                    else
                    {
                        ScenarioObjectStartStateApplyService.ApplyToObject(cell.lightObject, placement, result);
                    }
                }
                return true;
            }
            else
                AddBunkerMessage(result, "Room light placement #" + index + " failed at " + gridX + "," + gridY + ".");
            return false;
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
