using System;
using System.Collections.Generic;
using System.Reflection;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Application.Authoring{
    internal sealed class ScenarioBuildDeletionAuthoringService
    {
        private static readonly MethodInfo OnGridUpdatedMethod = typeof(ShelterRoomGrid).GetMethod("OnGridUpdated", BindingFlags.NonPublic | BindingFlags.Instance);
        private readonly ObjectPlacementService _objectPlacementService;
        private readonly WallWiringEditService _wallWiringEditService;

        public ScenarioBuildDeletionAuthoringService(
            ObjectPlacementService objectPlacementService,
            WallWiringEditService wallWiringEditService)
        {
            _objectPlacementService = objectPlacementService;
            _wallWiringEditService = wallWiringEditService;
        }

        public bool CanDeleteObject(ScenarioAuthoringTarget target, out string reason)
        {
            Obj_Base obj;
            return TryResolveObject(target, out obj, out reason)
                && CanRemoveLiveObject(obj, out reason);
        }

        public bool CanDeleteRoom(ScenarioAuthoringTarget target, out string reason)
        {
            ShelterRoomGrid.GridCell cell;
            int gridX;
            int gridY;
            if (!TryResolveGridCellOrSingleDraftPlacement(target, ScenarioPlacementDefinitionKind.Room, out cell, out gridX, out gridY, out reason))
                return false;

            if (!IsRoomCell(cell))
            {
                reason = "Select a room tile before deleting a room.";
                return false;
            }

            if (!CanDeleteRoomContents(gridX, gridY, out reason))
                return false;

            reason = null;
            return true;
        }

        public bool CanDeleteLadder(ScenarioAuthoringTarget target, out string reason)
        {
            ShelterRoomGrid.GridCell cell;
            int gridX;
            int gridY;
            if (!TryResolveGridCellOrSingleDraftPlacement(target, ScenarioPlacementDefinitionKind.Ladder, out cell, out gridX, out gridY, out reason))
                return false;

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.HasLadder(gridX, gridY))
            {
                reason = "Select the top room cell of a ladder before deleting it.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool CanDeleteLight(ScenarioAuthoringTarget target, out string reason)
        {
            ShelterRoomGrid.GridCell cell;
            int gridX;
            int gridY;
            if (!TryResolveGridCellOrSingleDraftPlacement(target, ScenarioPlacementDefinitionKind.RoomLight, out cell, out gridX, out gridY, out reason))
                return false;

            if (cell == null || (UnityEngine.Object)cell.lightObject == (UnityEngine.Object)null)
            {
                reason = "Select a room cell with a light before deleting the light.";
                return false;
            }

            reason = null;
            return true;
        }

        public bool CanResetWall(ScenarioAuthoringTarget target, out string reason)
        {
            return CanDeleteRoomVisual(target, "Select a room tile before resetting its wall.", out reason);
        }

        public bool CanResetWire(ScenarioAuthoringTarget target, out string reason)
        {
            return CanDeleteRoomVisual(target, "Select a room tile before clearing its wiring.", out reason);
        }

        public bool DeleteObject(ScenarioAuthoringTarget target, out string message)
        {
            message = null;
            Obj_Base obj;
            string reason;
            if (!TryResolveObject(target, out obj, out reason))
            {
                message = reason;
                return false;
            }

            string objectName = ScenarioBunkerDraftService.SafeObjectName(obj);
            if (!CanRemoveLiveObject(obj, out reason))
            {
                message = reason;
                return false;
            }

            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
            {
                message = "ObjectManager is not ready; '" + objectName + "' was not deleted.";
                return false;
            }

            try
            {
                manager.RemoveObject(obj);
            }
            catch (Exception ex)
            {
                message = "Failed to delete '" + objectName + "': " + ex.Message;
                return false;
            }

            bool removedDraft = _objectPlacementService.RemovePlacement(delegate(ObjectPlacement placement)
            {
                return MatchesObjectPlacement(placement, obj);
            });

            message = "Deleted live object '" + objectName + "' and " + (removedDraft ? "removed its draft placement." : "found no matching draft placement.");
            MMLog.WriteInfo("[ScenarioBuildDeletion] " + message);
            return true;
        }

        public bool DeleteRoom(ScenarioAuthoringTarget target, out string message)
        {
            message = null;
            ShelterRoomGrid.GridCell cell;
            int gridX;
            int gridY;
            string reason;
            if (!TryResolveGridCellOrSingleDraftPlacement(target, ScenarioPlacementDefinitionKind.Room, out cell, out gridX, out gridY, out reason) || !IsRoomCell(cell))
            {
                message = reason ?? "Select a room tile before deleting a room.";
                return false;
            }

            if (!CanDeleteRoomContents(gridX, gridY, out reason))
            {
                message = reason;
                return false;
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            bool removedLight = grid.RemoveLight(gridX, gridY);
            bool removedTopLadders = grid.RemoveLadders(gridX, gridY);
            bool removedIncomingLadders = gridY > 0 && grid.RemoveLadders(gridX, gridY - 1);

            if (!DeleteRoomCell(grid, cell, gridX, gridY, out message))
                return false;

            int draftRemovals = 0;
            if (RemoveStructuralPlacement(ScenarioPlacementDefinitions.Room, gridX, gridY))
                draftRemovals++;
            if (RemoveStructuralPlacement(ScenarioPlacementDefinitions.RoomTop, gridX, gridY))
                draftRemovals++;
            if (RemoveStructuralPlacement(ScenarioPlacementDefinitions.RoomLight, gridX, gridY))
                draftRemovals++;
            if (RemoveStructuralPlacement(ScenarioPlacementDefinitions.Ladder, gridX, gridY))
                draftRemovals++;
            if (gridY > 0 && RemoveStructuralPlacement(ScenarioPlacementDefinitions.Ladder, gridX, gridY - 1))
                draftRemovals++;
            _wallWiringEditService.RemoveRoomEdit(gridX, gridY);

            message = "Deleted room " + gridX + "," + gridY + ", cleared dependent light/ladder state, and removed " + draftRemovals + " draft placement(s).";
            if (!removedLight && !removedTopLadders && !removedIncomingLadders)
                message += " No live dependent ladder or light was present.";
            MMLog.WriteInfo("[ScenarioBuildDeletion] " + message);
            return true;
        }

        public bool DeleteLadder(ScenarioAuthoringTarget target, out string message)
        {
            message = null;
            ShelterRoomGrid.GridCell cell;
            int gridX;
            int gridY;
            string reason;
            if (!TryResolveGridCellOrSingleDraftPlacement(target, ScenarioPlacementDefinitionKind.Ladder, out cell, out gridX, out gridY, out reason))
            {
                message = reason;
                return false;
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.RemoveLadders(gridX, gridY))
            {
                message = "No ladder could be removed from " + gridX + "," + gridY + ". Select the ladder's top room cell.";
                return false;
            }

            bool removedDraft = RemoveStructuralPlacement(ScenarioPlacementDefinitions.Ladder, gridX, gridY);
            message = "Deleted ladder at " + gridX + "," + gridY + " and " + (removedDraft ? "removed its draft placement." : "found no matching draft placement.");
            MMLog.WriteInfo("[ScenarioBuildDeletion] " + message);
            return true;
        }

        public bool DeleteLight(ScenarioAuthoringTarget target, out string message)
        {
            message = null;
            ShelterRoomGrid.GridCell cell;
            int gridX;
            int gridY;
            string reason;
            if (!TryResolveGridCellOrSingleDraftPlacement(target, ScenarioPlacementDefinitionKind.RoomLight, out cell, out gridX, out gridY, out reason))
            {
                message = reason;
                return false;
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.RemoveLight(gridX, gridY))
            {
                message = "No room light could be removed from " + gridX + "," + gridY + ".";
                return false;
            }

            bool removedDraft = RemoveStructuralPlacement(ScenarioPlacementDefinitions.RoomLight, gridX, gridY);
            message = "Deleted room light at " + gridX + "," + gridY + " and " + (removedDraft ? "removed its draft placement." : "found no matching draft placement.");
            MMLog.WriteInfo("[ScenarioBuildDeletion] " + message);
            return true;
        }

        public bool ResetWall(ScenarioAuthoringTarget target, out string message)
        {
            ShelterRoomGrid.GridCell cell;
            int gridX;
            int gridY;
            string reason;
            if (!TryResolveGridCell(target, out cell, out gridX, out gridY, out reason) || !IsRoomCell(cell))
            {
                message = reason ?? "Select a room tile before resetting its wall.";
                return false;
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.SetWall(gridX, gridY, 0))
            {
                message = "The wall at " + gridX + "," + gridY + " could not be reset.";
                return false;
            }

            if (!_wallWiringEditService.ResetWall(gridX, gridY))
            {
                message = "Reset wall at " + gridX + "," + gridY + ", but the draft clear could not be recorded.";
                return true;
            }

            message = "Reset wall at " + gridX + "," + gridY + " and stored the authored clear.";
            return true;
        }

        public bool ResetWire(ScenarioAuthoringTarget target, out string message)
        {
            ShelterRoomGrid.GridCell cell;
            int gridX;
            int gridY;
            string reason;
            if (!TryResolveGridCell(target, out cell, out gridX, out gridY, out reason) || !IsRoomCell(cell))
            {
                message = reason ?? "Select a room tile before clearing its wiring.";
                return false;
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.SetWiring(gridX, gridY, null))
            {
                message = "The wiring at " + gridX + "," + gridY + " could not be cleared.";
                return false;
            }

            if (!_wallWiringEditService.ResetWire(gridX, gridY))
            {
                message = "Cleared wiring at " + gridX + "," + gridY + ", but the draft clear could not be recorded.";
                return true;
            }

            message = "Cleared wiring at " + gridX + "," + gridY + " and stored the authored clear.";
            return true;
        }

        private bool TryResolveGridCellOrSingleDraftPlacement(
            ScenarioAuthoringTarget target,
            ScenarioPlacementDefinitionKind fallbackKind,
            out ShelterRoomGrid.GridCell cell,
            out int gridX,
            out int gridY,
            out string reason)
        {
            if (TryResolveGridCell(target, out cell, out gridX, out gridY, out reason))
                return true;

            if (target != null)
                return false;

            return TryResolveSingleDraftStructuralGrid(fallbackKind, out cell, out gridX, out gridY, out reason);
        }

        private bool TryResolveSingleDraftStructuralGrid(
            ScenarioPlacementDefinitionKind fallbackKind,
            out ShelterRoomGrid.GridCell cell,
            out int gridX,
            out int gridY,
            out string reason)
        {
            cell = null;
            gridX = -1;
            gridY = -1;
            reason = null;

            ObjectPlacement placement;
            if (!_objectPlacementService.TryFindSinglePlacement(delegate(ObjectPlacement candidate)
            {
                ScenarioPlacementDefinitionKind candidateKind;
                return ScenarioPlacementDefinitions.TryParseSpecialKind(candidate != null ? candidate.DefinitionReference : null, out candidateKind)
                    && candidateKind == fallbackKind;
            }, out placement))
            {
                reason = "Select a shelter grid target first.";
                return false;
            }

            if (!ScenarioPropertyBag.TryGetInt(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridX, out gridX)
                || !ScenarioPropertyBag.TryGetInt(placement.CustomProperties, ScenarioPlacementDefinitions.PropertyGridY, out gridY))
            {
                reason = "The single authored structural placement is missing grid coordinates.";
                return false;
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.isInitialized)
            {
                reason = "ShelterRoomGrid is not ready.";
                return false;
            }

            cell = grid.GetCell(gridX, gridY);
            if (cell == null)
            {
                reason = "The authored structural placement is outside the shelter grid.";
                return false;
            }

            reason = "Using the only authored " + FormatStructuralKind(fallbackKind) + " at " + gridX + "," + gridY + ".";
            return true;
        }

        private static string FormatStructuralKind(ScenarioPlacementDefinitionKind kind)
        {
            switch (kind)
            {
                case ScenarioPlacementDefinitionKind.Room:
                    return "room";
                case ScenarioPlacementDefinitionKind.Ladder:
                    return "ladder";
                case ScenarioPlacementDefinitionKind.RoomLight:
                    return "room light";
                default:
                    return "structural placement";
            }
        }

        private static bool TryResolveObject(ScenarioAuthoringTarget target, out Obj_Base obj, out string reason)
        {
            obj = null;
            reason = null;
            GameObject gameObject = ResolveGameObject(target);
            obj = gameObject != null ? gameObject.GetComponentInParent<Obj_Base>() : null;
            if (obj == null)
            {
                reason = "Select a live shelter object before using Delete Object.";
                return false;
            }

            ScenarioPlacementDefinitionKind specialKind;
            if (ScenarioPlacementDefinitions.TryParseSpecialKind(obj.GetObjectType().ToString(), out specialKind))
            {
                reason = "Use the room, ladder, or light delete command for shelter structure targets.";
                return false;
            }

            return true;
        }

        private static bool TryResolveGridCell(ScenarioAuthoringTarget target, out ShelterRoomGrid.GridCell cell, out int gridX, out int gridY, out string reason)
        {
            cell = null;
            gridX = -1;
            gridY = -1;
            reason = null;
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null || !grid.isInitialized)
            {
                reason = "ShelterRoomGrid is not ready.";
                return false;
            }

            if (target != null && target.GridX.HasValue && target.GridY.HasValue)
            {
                gridX = target.GridX.Value;
                gridY = target.GridY.Value;
            }
            else if (target == null || !grid.WorldCoordsToCellCoords(target.WorldPosition, out gridX, out gridY))
            {
                reason = "Select a shelter grid target first.";
                return false;
            }

            cell = grid.GetCell(gridX, gridY);
            if (cell == null)
            {
                reason = "The selected grid cell is outside the shelter.";
                return false;
            }

            return true;
        }

        private static bool CanDeleteRoomVisual(ScenarioAuthoringTarget target, string missingMessage, out string reason)
        {
            ShelterRoomGrid.GridCell cell;
            int gridX;
            int gridY;
            if (!TryResolveGridCell(target, out cell, out gridX, out gridY, out reason))
                return false;

            if (!IsRoomCell(cell))
            {
                reason = missingMessage;
                return false;
            }

            reason = null;
            return true;
        }

        private static bool IsRoomCell(ShelterRoomGrid.GridCell cell)
        {
            return cell != null
                && (cell.type == ShelterRoomGrid.CellType.Room || cell.type == ShelterRoomGrid.CellType.RoomTop);
        }

        private static bool CanRemoveLiveObject(Obj_Base obj, out string reason)
        {
            reason = null;
            if (obj == null)
            {
                reason = "Select a live shelter object before using Delete Object.";
                return false;
            }

            if (obj.GetObjectType() != ObjectManager.ObjectType.StorageArea)
                return true;

            Obj_Storage storage = obj.GetComponent<Obj_Storage>();
            InventoryManager inventory = InventoryManager.Instance;
            if ((UnityEngine.Object)storage == (UnityEngine.Object)null || inventory == null)
                return true;

            if (inventory.GetTotalStackCount() <= inventory.storageCapacity - storage.storageCapacity)
                return true;

            reason = "Removing this storage would exceed remaining capacity.";
            return false;
        }

        private static bool CanDeleteRoomContents(int gridX, int gridY, out string reason)
        {
            reason = null;
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            ObjectManager manager = ObjectManager.Instance;
            if (grid == null || manager == null)
            {
                reason = "ObjectManager is not ready; room contents could not be checked.";
                return false;
            }

            List<Obj_Base> containedObjects = GetObjectsInRoomCell(grid, manager, gridX, gridY);
            if (containedObjects.Count <= 0)
                return true;

            reason = "Room contains " + containedObjects.Count + " objects - delete or move them first.";
            return false;
        }

        private static List<Obj_Base> GetObjectsInRoomCell(ShelterRoomGrid grid, ObjectManager manager, int gridX, int gridY)
        {
            List<Obj_Base> containedObjects = new List<Obj_Base>();
            if (grid == null || manager == null)
                return containedObjects;

            List<Obj_Base> allObjects = manager.GetAllObjects();
            for (int index = 0; index < allObjects.Count; index++)
            {
                Obj_Base obj = allObjects[index];
                if ((UnityEngine.Object)obj == (UnityEngine.Object)null || IsStructuralObject(obj))
                    continue;

                int objectGridX;
                int objectGridY;
                if (!grid.WorldCoordsToCellCoords(obj.transform.position, out objectGridX, out objectGridY))
                    continue;

                if (objectGridX == gridX && objectGridY == gridY)
                    containedObjects.Add(obj);
            }

            return containedObjects;
        }

        private static bool IsStructuralObject(Obj_Base obj)
        {
            if (obj == null)
                return false;

            ScenarioPlacementDefinitionKind specialKind;
            return ScenarioPlacementDefinitions.TryParseSpecialKind(obj.GetObjectType().ToString(), out specialKind);
        }

        private static bool DeleteRoomCell(ShelterRoomGrid grid, ShelterRoomGrid.GridCell cell, int gridX, int gridY, out string message)
        {
            message = null;
            if (grid == null || cell == null || !IsRoomCell(cell))
            {
                message = "The selected cell is not a room.";
                return false;
            }

            if ((UnityEngine.Object)cell.prefab != (UnityEngine.Object)null)
            {
                ShelterRoom room = cell.prefab.GetComponent<ShelterRoom>();
                if ((UnityEngine.Object)room != (UnityEngine.Object)null)
                    room.RemovePathnodes();
                UnityEngine.Object.Destroy(cell.prefab);
                cell.prefab = null;
            }

            cell.type = ShelterRoomGrid.CellType.Dirt;
            cell.lightObject = null;
            InvokeGridUpdated(grid, gridX, gridY);
            return true;
        }

        private static void InvokeGridUpdated(ShelterRoomGrid grid, int gridX, int gridY)
        {
            if (grid == null || OnGridUpdatedMethod == null)
                return;

            try
            {
                OnGridUpdatedMethod.Invoke(grid, new object[] { gridX, gridY });
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBuildDeletion] Room grid refresh failed after deletion: " + ex.Message);
            }
        }

        private bool RemoveStructuralPlacement(string definitionReference, int gridX, int gridY)
        {
            return _objectPlacementService.RemovePlacement(delegate(ObjectPlacement placement)
            {
                return ScenarioBunkerDraftService.IsPlacementAtGrid(placement, definitionReference, gridX, gridY);
            });
        }

        private static bool MatchesObjectPlacement(ObjectPlacement placement, Obj_Base obj)
        {
            return ScenarioBunkerDraftService.MatchesPlacement(placement, obj);
        }

        private static GameObject ResolveGameObject(ScenarioAuthoringTarget target)
        {
            if (target == null || target.RuntimeObject == null)
                return null;

            GameObject gameObject = target.RuntimeObject as GameObject;
            if (gameObject != null)
                return gameObject;

            Component component = target.RuntimeObject as Component;
            return component != null ? component.gameObject : null;
        }
    }
}
