using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioBuildPlacementAuthoringService
    {
        internal sealed class PaletteEntryModel
        {
            public string ActionId;
            public string Label;
            public string Hint;
            public string Source;
            public string Badge;
            public Sprite Preview;
            public bool Enabled;
            public bool Active;
        }

        internal sealed class PaletteSectionModel
        {
            public string Id;
            public string Title;
            public string EmptyMessage;
            public List<PaletteEntryModel> Entries;
        }

        internal sealed class StatusModel
        {
            public bool PlacementActive;
            public bool CanCancel;
            public string Title;
            public string Guidance;
            public string Detail;
        }

        private enum PlacementSessionKind
        {
            Object = 0,
            Room = 1,
            Ladder = 2,
            RoomLight = 3
        }

        private sealed class ActivePlacementSession
        {
            public PlacementSessionKind Kind;
            public Obj_GhostBase Ghost;
            public string Label;
            public string DefinitionReference;
            public ObjectManager.ObjectType ObjectType;
            public int Level;
            public bool PlaceableOnSurface;
            public float ColliderWidth;
        }

        private static readonly ScenarioBuildPlacementAuthoringService _instance = new ScenarioBuildPlacementAuthoringService();
        private static readonly FieldInfo WiresSpritesField = typeof(ShelterRoomGrid).GetField("wiresSprites", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly string[] ObjectSectionOrder = new[]
        {
            "Workbenches & Stations",
            "Shelter Systems",
            "Storage & Utility",
            "Furniture & Misc"
        };

        private ActivePlacementSession _activePlacement;

        public static ScenarioBuildPlacementAuthoringService Instance
        {
            get { return _instance; }
        }

        public bool HasActivePlacement
        {
            get { return _activePlacement != null && _activePlacement.Ghost != null; }
        }

        private ScenarioBuildPlacementAuthoringService()
        {
        }

        public void Reset()
        {
            CancelActivePlacement(null);
        }

        public StatusModel GetStatusModel(ScenarioAuthoringState state, ScenarioEditorSession session)
        {
            ScenarioAuthoringTool tool = state != null ? state.ActiveTool : ScenarioAuthoringTool.Select;
            StatusModel model = new StatusModel();
            if (HasActivePlacement)
            {
                model.PlacementActive = true;
                model.CanCancel = true;
                model.Title = "Placing " + (_activePlacement.Label ?? "Item");
                model.Guidance = "Left-click to place instantly into the scenario draft. Right-click or Escape cancels.";
                model.Detail = "This uses Sheltered ghost previews and placement rules before committing the final edit.";
                return model;
            }

            switch (tool)
            {
                case ScenarioAuthoringTool.Shelter:
                    model.Title = "Structure Tools";
                    model.Guidance = "Room, ladder, and light tools use vanilla ghost placement previews, then commit instantly into the draft.";
                    model.Detail = "Use these to expand the shelter layout instead of only decorating it.";
                    break;

                case ScenarioAuthoringTool.Wiring:
                    model.Title = "Wall & Wiring";
                    model.Guidance = "Select a shelter room tile, then pick a wall or wiring sprite to apply it immediately.";
                    model.Detail = "These edits are stored as bunker room changes in the scenario XML.";
                    break;

                default:
                    model.Title = "Object Placement";
                    model.Guidance = "Pick a workbench, shelter system, or furniture prefab to start vanilla-style placement.";
                    model.Detail = "Placed objects are spawned live now and stored back into BunkerEdits/ObjectPlacements.";
                    break;
            }

            return model;
        }

        public List<PaletteSectionModel> GetPaletteSections(ScenarioAuthoringState state, ScenarioEditorSession session)
        {
            List<PaletteSectionModel> sections = new List<PaletteSectionModel>();
            ScenarioAuthoringTool tool = state != null ? state.ActiveTool : ScenarioAuthoringTool.Select;
            switch (tool)
            {
                case ScenarioAuthoringTool.Shelter:
                    sections.Add(BuildStructureSection());
                    break;

                case ScenarioAuthoringTool.Wiring:
                    AppendRoomVisualSections(sections, state != null ? state.SelectedTarget : null);
                    break;

                default:
                    AppendObjectSections(sections);
                    break;
            }

            return sections;
        }

        public bool TryHandleAction(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (string.IsNullOrEmpty(actionId))
                return false;

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildPlacementCancel, StringComparison.Ordinal))
            {
                handled = true;
                return CancelActivePlacement("Placement cancelled.", out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildStructureRoom, StringComparison.Ordinal))
            {
                handled = true;
                return StartRoomPlacement(out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildStructureLadder, StringComparison.Ordinal))
            {
                handled = true;
                return StartLadderPlacement(out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildStructureLight, StringComparison.Ordinal))
            {
                handled = true;
                return StartRoomLightPlacement(out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildObjectPlacePrefix, StringComparison.Ordinal))
            {
                handled = true;
                string payload = DecodeActionToken(actionId.Substring(ScenarioAuthoringActionIds.ActionBuildObjectPlacePrefix.Length));
                ObjectManager.ObjectType objectType;
                int level;
                if (!TryParseObjectPayload(payload, out objectType, out level))
                {
                    message = "The selected object placement could not be decoded.";
                    return false;
                }

                return StartObjectPlacement(objectType, level, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildWallApplyPrefix, StringComparison.Ordinal))
            {
                handled = true;
                int wallIndex;
                if (!int.TryParse(actionId.Substring(ScenarioAuthoringActionIds.ActionBuildWallApplyPrefix.Length), out wallIndex))
                {
                    message = "The selected wall sprite could not be decoded.";
                    return false;
                }

                return ApplyWall(state != null ? state.SelectedTarget : null, wallIndex, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildWireApplyPrefix, StringComparison.Ordinal))
            {
                handled = true;
                int wireIndex;
                if (!int.TryParse(actionId.Substring(ScenarioAuthoringActionIds.ActionBuildWireApplyPrefix.Length), out wireIndex))
                {
                    message = "The selected wiring sprite could not be decoded.";
                    return false;
                }

                return ApplyWire(state != null ? state.SelectedTarget : null, wireIndex, out message);
            }

            return false;
        }

        public bool Update(ScenarioAuthoringState state, ScenarioEditorSession session, out string message)
        {
            message = null;
            if (!HasActivePlacement)
                return false;

            if (state == null || session == null || ScenarioAuthoringRuntimeGuards.IsPlaytesting())
            {
                return CancelActivePlacement("Placement tool reset because authoring is no longer in live-edit mode.", out message);
            }

            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            if (UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return CancelActivePlacement("Placement cancelled.", out message);

            if (_activePlacement.Ghost == null)
            {
                _activePlacement = null;
                message = "The active placement preview was lost and has been reset.";
                return true;
            }

            if (!inputCapture.PointerOverAuthoringUi)
            {
                Vector3 worldPoint;
                if (TryGetMouseWorldPoint(out worldPoint))
                    UpdateGhostPosition(worldPoint);

                if (UnityEngine.Input.GetMouseButtonUp(1))
                    return CancelActivePlacement("Placement cancelled.", out message);

                if (UnityEngine.Input.GetMouseButtonUp(0))
                    return TryCompletePlacement(session, out message);
            }

            return false;
        }

        public static string BuildObjectActionId(ObjectManager.ObjectType objectType, int level)
        {
            string payload = objectType + "|" + level;
            return ScenarioAuthoringActionIds.ActionBuildObjectPlacePrefix + EncodeActionToken(payload);
        }

        private static string BuildRoomIdentity(int gridX, int gridY)
        {
            return "room:" + gridX + ":" + gridY;
        }

        private static string BuildLadderIdentity(int gridX, int gridY)
        {
            return "ladder:" + gridX + ":" + gridY;
        }

        private static string BuildLightIdentity(int gridX, int gridY)
        {
            return "light:" + gridX + ":" + gridY;
        }

        private bool StartObjectPlacement(ObjectManager.ObjectType objectType, int level, out string message)
        {
            message = null;
            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
            {
                message = "ObjectManager is not ready; object placement is unavailable.";
                return false;
            }

            GameObject prefab = manager.GetPrefab(objectType, level);
            Obj_Base prefabComponent = prefab != null ? prefab.GetComponent<Obj_Base>() : null;
            if (prefab == null || prefabComponent == null)
            {
                message = "No compatible prefab is available for " + objectType + ".";
                return false;
            }

            ActivePlacementSession session = CreateGhostSession(ObjectManager.ObjectType.CraftingGhost, PlacementSessionKind.Object, BuildObjectLabel(prefabComponent, objectType), out message);
            if (session == null)
                return false;

            Obj_CraftingGhost ghost = session.Ghost as Obj_CraftingGhost;
            if (ghost == null)
            {
                CancelActivePlacement(null);
                message = "The crafting ghost prefab was not available for object placement.";
                return false;
            }

            ghost.ImitateObject(objectType, level);
            ghost.SetIgnoresObjects(false);
            session.ObjectType = objectType;
            session.Level = level;
            session.DefinitionReference = objectType.ToString();
            session.PlaceableOnSurface = prefabComponent.PlacableOnSurface;
            BoxCollider2D collider = prefabComponent.GetComponent<BoxCollider2D>();
            session.ColliderWidth = collider != null ? collider.size.x : 0f;
            _activePlacement = session;
            message = "Placing " + session.Label + ". Left-click to place, right-click or Escape to cancel.";
            return true;
        }

        private bool StartRoomPlacement(out string message)
        {
            ActivePlacementSession session = CreateGhostSession(ObjectManager.ObjectType.RoomGhost, PlacementSessionKind.Room, "Room Tile", out message);
            if (session == null)
                return false;

            session.DefinitionReference = ScenarioPlacementDefinitions.Room;
            _activePlacement = session;
            message = "Placing a room tile. Left-click to place, right-click or Escape to cancel.";
            return true;
        }

        private bool StartLadderPlacement(out string message)
        {
            ActivePlacementSession session = CreateGhostSession(ObjectManager.ObjectType.LadderGhost, PlacementSessionKind.Ladder, "Ladder", out message);
            if (session == null)
                return false;

            session.DefinitionReference = ScenarioPlacementDefinitions.Ladder;
            _activePlacement = session;
            message = "Placing a ladder. Left-click to place, right-click or Escape to cancel.";
            return true;
        }

        private bool StartRoomLightPlacement(out string message)
        {
            ActivePlacementSession session = CreateGhostSession(ObjectManager.ObjectType.RoomLightGhost, PlacementSessionKind.RoomLight, "Room Light", out message);
            if (session == null)
                return false;

            session.DefinitionReference = ScenarioPlacementDefinitions.RoomLight;
            _activePlacement = session;
            message = "Placing a room light. Left-click to place, right-click or Escape to cancel.";
            return true;
        }

        private ActivePlacementSession CreateGhostSession(
            ObjectManager.ObjectType ghostType,
            PlacementSessionKind kind,
            string label,
            out string message)
        {
            message = null;
            CancelActivePlacement(null);

            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
            {
                message = "ObjectManager is not ready; placement preview is unavailable.";
                return null;
            }

            Obj_Base ghostBase = manager.SpawnObject(ghostType, Vector2.zero);
            Obj_GhostBase ghost = ghostBase as Obj_GhostBase;
            if (ghost == null)
            {
                if (ghostBase != null)
                    manager.RemoveObject(ghostBase);
                message = "The required ghost prefab was not available for placement.";
                return null;
            }

            ghost.SetUpGhost(null, null);
            ghost.transform.position = Vector3.zero;
            return new ActivePlacementSession
            {
                Kind = kind,
                Ghost = ghost,
                Label = label,
                Level = 1
            };
        }

        private bool TryCompletePlacement(ScenarioEditorSession session, out string message)
        {
            message = null;
            if (!HasActivePlacement || _activePlacement.Ghost == null)
            {
                message = "No active placement preview is available.";
                return false;
            }

            Obj_GhostBase ghost = _activePlacement.Ghost;
            if (!ghost.OnTryPlacement())
            {
                message = "That placement is blocked by the current shelter layout or collisions.";
                return true;
            }

            switch (_activePlacement.Kind)
            {
                case PlacementSessionKind.Object:
                    return CompleteObjectPlacement(session, ghost as Obj_CraftingGhost, out message);

                case PlacementSessionKind.Room:
                    return CompleteRoomPlacement(session, ghost, out message);

                case PlacementSessionKind.Ladder:
                    return CompleteLadderPlacement(session, ghost, out message);

                case PlacementSessionKind.RoomLight:
                    return CompleteRoomLightPlacement(session, ghost, out message);

                default:
                    return CancelActivePlacement("Unknown placement session cancelled.", out message);
            }
        }

        private bool CompleteObjectPlacement(ScenarioEditorSession session, Obj_CraftingGhost ghost, out string message)
        {
            message = null;
            if (session == null || ghost == null)
            {
                message = "Object placement could not be completed because the ghost preview was unavailable.";
                return CancelActivePlacement(null);
            }

            Vector3 position = ghost.transform.position;
            ghost.OnPlacementFinished();
            ObjectManager.Instance.RemoveObject(ghost);
            Obj_Base spawned = ObjectManager.Instance.SpawnObject(_activePlacement.ObjectType, _activePlacement.Level, new Vector2(position.x, position.y));
            _activePlacement = null;
            if (spawned == null)
            {
                message = "The final object could not be spawned after placement.";
                return true;
            }

            ScenarioBunkerDraftService.UpsertPlacement(session, ScenarioBunkerDraftService.CreatePlacement(spawned));
            message = "Placed " + ScenarioBunkerDraftService.SafeObjectName(spawned) + " and recorded it in the scenario draft.";
            return true;
        }

        private bool CompleteRoomPlacement(ScenarioEditorSession session, Obj_GhostBase ghost, out string message)
        {
            message = null;
            if (session == null || ghost == null)
            {
                message = "Room placement could not be completed because the ghost preview was unavailable.";
                return CancelActivePlacement(null);
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            int gridX;
            int gridY;
            if (grid == null || !grid.WorldCoordsToCellCoords(ghost.transform.position, out gridX, out gridY))
            {
                return CancelActivePlacement("Room placement could not resolve a shelter cell.", out message);
            }

            ghost.OnPlacementFinished();
            bool applied = CraftingManager.FinishCraft_Room(null, null, ghost);
            _activePlacement = null;
            if (!applied)
            {
                message = "The room could not be committed after the preview confirmed placement.";
                return true;
            }

            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            string definitionReference = cell != null && cell.type == ShelterRoomGrid.CellType.RoomTop
                ? ScenarioPlacementDefinitions.RoomTop
                : ScenarioPlacementDefinitions.Room;
            ObjectPlacement placement = ScenarioBunkerDraftService.CreatePlacement(
                definitionReference,
                ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY),
                Vector3.zero,
                Property(ScenarioPlacementDefinitions.PropertyGridX, gridX.ToString()),
                Property(ScenarioPlacementDefinitions.PropertyGridY, gridY.ToString()),
                Property(ScenarioPlacementDefinitions.PropertyAuthoringIdentity, BuildRoomIdentity(gridX, gridY)));
            ScenarioBunkerDraftService.UpsertPlacement(session, placement);
            message = "Placed a room tile at " + gridX + "," + gridY + " and stored it in the draft.";
            return true;
        }

        private bool CompleteLadderPlacement(ScenarioEditorSession session, Obj_GhostBase ghost, out string message)
        {
            message = null;
            if (session == null || ghost == null)
            {
                message = "Ladder placement could not be completed because the ghost preview was unavailable.";
                return CancelActivePlacement(null);
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            int gridX;
            int gridY;
            if (grid == null || !grid.WorldCoordsToCellCoords(ghost.transform.position, out gridX, out gridY))
            {
                return CancelActivePlacement("Ladder placement could not resolve a shelter cell.", out message);
            }

            float horizontalPos = ComputeHorizontalPosition(grid, ghost.transform.position, gridX);
            Vector3 ladderPosition = ghost.transform.position;
            ghost.OnPlacementFinished();
            bool applied = CraftingManager.FinishCraft_Ladder(null, null, ghost);
            _activePlacement = null;
            if (!applied)
            {
                message = "The ladder could not be committed after the preview confirmed placement.";
                return true;
            }

            ObjectPlacement placement = ScenarioBunkerDraftService.CreatePlacement(
                ScenarioPlacementDefinitions.Ladder,
                ladderPosition,
                Vector3.zero,
                Property(ScenarioPlacementDefinitions.PropertyGridX, gridX.ToString()),
                Property(ScenarioPlacementDefinitions.PropertyGridY, gridY.ToString()),
                Property(ScenarioPlacementDefinitions.PropertyHorizontalPos, horizontalPos.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)),
                Property(ScenarioPlacementDefinitions.PropertyAuthoringIdentity, BuildLadderIdentity(gridX, gridY)));
            ScenarioBunkerDraftService.UpsertPlacement(session, placement);
            message = "Placed a ladder for room " + gridX + "," + gridY + " and stored it in the draft.";
            return true;
        }

        private bool CompleteRoomLightPlacement(ScenarioEditorSession session, Obj_GhostBase ghost, out string message)
        {
            message = null;
            if (session == null || ghost == null)
            {
                message = "Room light placement could not be completed because the ghost preview was unavailable.";
                return CancelActivePlacement(null);
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            int gridX;
            int gridY;
            if (grid == null || !grid.WorldCoordsToCellCoords(ghost.transform.position, out gridX, out gridY))
            {
                return CancelActivePlacement("Room light placement could not resolve a shelter cell.", out message);
            }

            ghost.OnPlacementFinished();
            bool applied = CraftingManager.FinishCraft_Light(null, null, ghost);
            _activePlacement = null;
            if (!applied)
            {
                message = "The room light could not be committed after the preview confirmed placement.";
                return true;
            }

            ObjectPlacement placement = ScenarioBunkerDraftService.CreatePlacement(
                ScenarioPlacementDefinitions.RoomLight,
                ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY),
                Vector3.zero,
                Property(ScenarioPlacementDefinitions.PropertyGridX, gridX.ToString()),
                Property(ScenarioPlacementDefinitions.PropertyGridY, gridY.ToString()),
                Property(ScenarioPlacementDefinitions.PropertyAuthoringIdentity, BuildLightIdentity(gridX, gridY)));
            ScenarioBunkerDraftService.UpsertPlacement(session, placement);
            message = "Placed a room light at " + gridX + "," + gridY + " and stored it in the draft.";
            return true;
        }

        private bool ApplyWall(ScenarioAuthoringTarget target, int wallIndex, out string message)
        {
            message = null;
            ShelterRoom room;
            int gridX;
            int gridY;
            if (!TryResolveRoomTarget(target, out room, out gridX, out gridY))
            {
                message = "Select a shelter room tile before applying a wall sprite.";
                return false;
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (room.wallSprites == null || wallIndex < 0 || wallIndex >= room.wallSprites.Count)
            {
                message = "The selected wall sprite index is not valid for this room.";
                return false;
            }

            if (grid == null || !grid.SetWall(gridX, gridY, wallIndex))
            {
                message = "The selected wall sprite could not be applied to " + gridX + "," + gridY + ".";
                return false;
            }

            ScenarioEditorSession editorSession = ScenarioEditorController.Instance.CurrentSession;
            if (editorSession != null)
            {
                ScenarioBunkerDraftService.UpsertRoomEdit(editorSession, gridX, gridY, delegate(RoomEdit edit)
                {
                    edit.WallSpriteIndex = wallIndex;
                });
            }

            message = "Applied wall sprite " + wallIndex + " to room " + gridX + "," + gridY + ".";
            return true;
        }

        private bool ApplyWire(ScenarioAuthoringTarget target, int wireIndex, out string message)
        {
            message = null;
            ShelterRoom room;
            int gridX;
            int gridY;
            if (!TryResolveRoomTarget(target, out room, out gridX, out gridY))
            {
                message = "Select a shelter room tile before applying a wiring sprite.";
                return false;
            }

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            List<Sprite> wireSprites = grid != null && WiresSpritesField != null ? WiresSpritesField.GetValue(grid) as List<Sprite> : null;
            if (wireSprites == null || wireIndex < 0 || wireIndex >= wireSprites.Count)
            {
                message = "The selected wiring sprite index is not valid for this shelter.";
                return false;
            }

            if (grid == null || !grid.SetWiring(gridX, gridY, wireSprites[wireIndex]))
            {
                message = "The selected wiring sprite could not be applied to " + gridX + "," + gridY + ".";
                return false;
            }

            ScenarioEditorSession editorSession = ScenarioEditorController.Instance.CurrentSession;
            if (editorSession != null)
            {
                ScenarioBunkerDraftService.UpsertRoomEdit(editorSession, gridX, gridY, delegate(RoomEdit edit)
                {
                    edit.WireSpriteIndex = wireIndex;
                });
            }

            message = "Applied wiring sprite " + wireIndex + " to room " + gridX + "," + gridY + ".";
            return true;
        }

        private void UpdateGhostPosition(Vector3 worldPoint)
        {
            if (!HasActivePlacement || _activePlacement.Ghost == null)
                return;

            Vector3 position = worldPoint;
            switch (_activePlacement.Kind)
            {
                case PlacementSessionKind.Object:
                    position = ResolveObjectPlacementPosition(worldPoint);
                    break;

                case PlacementSessionKind.Room:
                case PlacementSessionKind.RoomLight:
                    position = ResolveGridPlacementPosition(worldPoint);
                    break;

                case PlacementSessionKind.Ladder:
                    position = ResolveLadderPlacementPosition(worldPoint);
                    break;
            }

            position.z = 0f;
            _activePlacement.Ghost.transform.position = position;
        }

        private Vector3 ResolveObjectPlacementPosition(Vector3 worldPoint)
        {
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null)
                return worldPoint;

            float width = Mathf.Max(0f, _activePlacement != null ? _activePlacement.ColliderWidth : 0f);
            float minX = width * 0.5f;
            float maxX = (grid.grid_width * grid.grid_cell_width) - width;
            if (maxX < minX)
                maxX = minX;

            float minY;
            float maxY;
            if (_activePlacement != null && _activePlacement.PlaceableOnSurface)
            {
                minY = -1f * grid.grid_cell_height;
                maxY = 0f;
            }
            else
            {
                minY = -1f * ((grid.grid_height - 1) * grid.grid_cell_height);
                maxY = -1f * grid.grid_cell_height;
            }

            return new Vector3(
                Mathf.Clamp(worldPoint.x, minX, maxX),
                Mathf.Clamp(worldPoint.y, minY, maxY),
                0f);
        }

        private static Vector3 ResolveGridPlacementPosition(Vector3 worldPoint)
        {
            int gridX;
            int gridY;
            if (ScenarioGridSnapService.TryGetCell(worldPoint, out gridX, out gridY))
                return ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY);

            return worldPoint;
        }

        private static Vector3 ResolveLadderPlacementPosition(Vector3 worldPoint)
        {
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            int gridX;
            int gridY;
            if (grid == null || !ScenarioGridSnapService.TryGetCell(worldPoint, out gridX, out gridY))
                return worldPoint;

            Vector3 snapped = ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY);
            float cellLeft = gridX * grid.grid_cell_width;
            float cellRight = cellLeft + grid.grid_cell_width;
            snapped.x = Mathf.Clamp(worldPoint.x, cellLeft + 0.05f, cellRight - 0.05f);
            return snapped;
        }

        private bool CancelActivePlacement(string fallbackMessage)
        {
            string ignored;
            return CancelActivePlacement(fallbackMessage, out ignored);
        }

        private bool CancelActivePlacement(string fallbackMessage, out string message)
        {
            message = fallbackMessage;
            if (_activePlacement == null)
                return !string.IsNullOrEmpty(message);

            Obj_GhostBase ghost = _activePlacement.Ghost;
            _activePlacement = null;
            if (ghost != null)
                ObjectManager.Instance.RemoveObject(ghost);
            return true;
        }

        private static void AppendObjectSections(List<PaletteSectionModel> sections)
        {
            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
            {
                sections.Add(new PaletteSectionModel
                {
                    Id = "objects_unavailable",
                    Title = "Objects",
                    EmptyMessage = "ObjectManager is not ready, so the object palette is unavailable.",
                    Entries = new List<PaletteEntryModel>()
                });
                return;
            }

            Dictionary<string, List<PaletteEntryModel>> grouped = new Dictionary<string, List<PaletteEntryModel>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ObjectSectionOrder.Length; i++)
                grouped[ObjectSectionOrder[i]] = new List<PaletteEntryModel>();

            int maxValue = (int)ObjectManager.ObjectType.Max;
            for (int raw = 0; raw < maxValue; raw++)
            {
                ObjectManager.ObjectType objectType = (ObjectManager.ObjectType)raw;
                if (!IsEligiblePaletteObject(objectType, manager))
                    continue;

                GameObject prefab = manager.GetPrefab(objectType, 1);
                Obj_Base component = prefab != null ? prefab.GetComponent<Obj_Base>() : null;
                if (prefab == null || component == null)
                    continue;

                string sectionTitle = ResolveObjectSectionTitle(objectType);
                grouped[sectionTitle].Add(new PaletteEntryModel
                {
                    ActionId = BuildObjectActionId(objectType, component.objectLevel > 0 ? component.objectLevel : 1),
                    Label = BuildObjectLabel(component, objectType),
                    Hint = "Uses Sheltered's crafting ghost preview, then places the final object instantly into the scenario draft.",
                    Source = objectType.ToString(),
                    Badge = "OBJ",
                    Preview = ResolvePreviewSprite(prefab),
                    Enabled = true,
                    Active = Instance._activePlacement != null
                        && Instance._activePlacement.Kind == PlacementSessionKind.Object
                        && Instance._activePlacement.ObjectType == objectType
                });
            }

            for (int i = 0; i < ObjectSectionOrder.Length; i++)
            {
                string title = ObjectSectionOrder[i];
                List<PaletteEntryModel> entries;
                if (!grouped.TryGetValue(title, out entries))
                    entries = new List<PaletteEntryModel>();

                entries.Sort(ComparePaletteEntries);
                sections.Add(new PaletteSectionModel
                {
                    Id = "objects_" + title.Replace(" ", "_").ToLowerInvariant(),
                    Title = title,
                    EmptyMessage = "No compatible prefabs are currently loaded for this category.",
                    Entries = entries
                });
            }
        }

        private static PaletteSectionModel BuildStructureSection()
        {
            List<PaletteEntryModel> entries = new List<PaletteEntryModel>();
            entries.Add(new PaletteEntryModel
            {
                ActionId = ScenarioAuthoringActionIds.ActionBuildStructureRoom,
                Label = "Room Tile",
                Hint = "Uses the vanilla room ghost preview, then commits the room instantly into the scenario draft.",
                Source = ScenarioPlacementDefinitions.Room,
                Badge = "RM",
                Preview = ResolveGhostPreview(ObjectManager.ObjectType.RoomGhost),
                Enabled = true,
                Active = Instance._activePlacement != null && Instance._activePlacement.Kind == PlacementSessionKind.Room
            });
            entries.Add(new PaletteEntryModel
            {
                ActionId = ScenarioAuthoringActionIds.ActionBuildStructureLadder,
                Label = "Ladder",
                Hint = "Uses the vanilla ladder ghost preview, then commits the ladder instantly into the scenario draft.",
                Source = ScenarioPlacementDefinitions.Ladder,
                Badge = "LD",
                Preview = ResolveGhostPreview(ObjectManager.ObjectType.LadderGhost),
                Enabled = true,
                Active = Instance._activePlacement != null && Instance._activePlacement.Kind == PlacementSessionKind.Ladder
            });
            entries.Add(new PaletteEntryModel
            {
                ActionId = ScenarioAuthoringActionIds.ActionBuildStructureLight,
                Label = "Room Light",
                Hint = "Uses the vanilla room-light ghost preview, then commits the light instantly into the scenario draft.",
                Source = ScenarioPlacementDefinitions.RoomLight,
                Badge = "LG",
                Preview = ResolveGhostPreview(ObjectManager.ObjectType.RoomLightGhost),
                Enabled = true,
                Active = Instance._activePlacement != null && Instance._activePlacement.Kind == PlacementSessionKind.RoomLight
            });

            entries.Sort(ComparePaletteEntries);
            return new PaletteSectionModel
            {
                Id = "structure_tools",
                Title = "Structure Tools",
                EmptyMessage = "Structure placement tools are unavailable right now.",
                Entries = entries
            };
        }

        private static void AppendRoomVisualSections(List<PaletteSectionModel> sections, ScenarioAuthoringTarget target)
        {
            ShelterRoom room;
            int gridX;
            int gridY;
            if (!TryResolveRoomTarget(target, out room, out gridX, out gridY))
            {
                sections.Add(new PaletteSectionModel
                {
                    Id = "walls_selection",
                    Title = "Walls & Wiring",
                    EmptyMessage = "Select a shelter room tile to browse wall and wiring sprites.",
                    Entries = new List<PaletteEntryModel>()
                });
                return;
            }

            int activeWallIndex = room.GetWallSprite();
            List<PaletteEntryModel> wallEntries = new List<PaletteEntryModel>();
            for (int i = 0; room.wallSprites != null && i < room.wallSprites.Count; i++)
            {
                wallEntries.Add(new PaletteEntryModel
                {
                    ActionId = ScenarioAuthoringActionIds.ActionBuildWallApplyPrefix + i,
                    Label = "Wall " + (i + 1),
                    Hint = "Apply wall sprite " + i + " to room " + gridX + "," + gridY + ".",
                    Source = "Room " + gridX + "," + gridY,
                    Badge = activeWallIndex == i ? "LIVE" : "WALL",
                    Preview = room.wallSprites[i],
                    Enabled = true,
                    Active = activeWallIndex == i
                });
            }

            wallEntries.Sort(ComparePaletteEntries);
            sections.Add(new PaletteSectionModel
            {
                Id = "wall_palette",
                Title = "Wall Sprites",
                EmptyMessage = "No wall sprites are available for the selected room.",
                Entries = wallEntries
            });

            List<PaletteEntryModel> wireEntries = new List<PaletteEntryModel>();
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            List<Sprite> wireSprites = grid != null && WiresSpritesField != null ? WiresSpritesField.GetValue(grid) as List<Sprite> : null;
            Sprite activeWire = room.GetWires();
            for (int i = 0; wireSprites != null && i < wireSprites.Count; i++)
            {
                wireEntries.Add(new PaletteEntryModel
                {
                    ActionId = ScenarioAuthoringActionIds.ActionBuildWireApplyPrefix + i,
                    Label = "Wire " + (i + 1),
                    Hint = "Apply wiring sprite " + i + " to room " + gridX + "," + gridY + ".",
                    Source = "Room " + gridX + "," + gridY,
                    Badge = activeWire == wireSprites[i] ? "LIVE" : "WIRE",
                    Preview = wireSprites[i],
                    Enabled = true,
                    Active = activeWire == wireSprites[i]
                });
            }

            wireEntries.Sort(ComparePaletteEntries);
            sections.Add(new PaletteSectionModel
            {
                Id = "wire_palette",
                Title = "Wiring Sprites",
                EmptyMessage = "No wiring sprites are available for the selected shelter.",
                Entries = wireEntries
            });
        }

        private static bool TryResolveRoomTarget(ScenarioAuthoringTarget target, out ShelterRoom room, out int gridX, out int gridY)
        {
            room = null;
            gridX = -1;
            gridY = -1;
            if (target == null || !target.GridX.HasValue || !target.GridY.HasValue)
                return false;

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null)
                return false;

            gridX = target.GridX.Value;
            gridY = target.GridY.Value;
            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            if (cell == null || cell.prefab == null)
                return false;

            room = cell.prefab.GetComponent<ShelterRoom>();
            return room != null;
        }

        private static bool IsEligiblePaletteObject(ObjectManager.ObjectType objectType, ObjectManager manager)
        {
            if (manager == null || !manager.HasPrefab(objectType))
                return false;

            switch (objectType)
            {
                case ObjectManager.ObjectType.Undefined:
                case ObjectManager.ObjectType.CraftingGhost:
                case ObjectManager.ObjectType.CatatonicGhost:
                case ObjectManager.ObjectType.RoomGhost:
                case ObjectManager.ObjectType.LadderGhost:
                case ObjectManager.ObjectType.RoomPaintGhost:
                case ObjectManager.ObjectType.RoomLightGhost:
                case ObjectManager.ObjectType.BurntGhost:
                case ObjectManager.ObjectType.UnconsciousGhost:
                case ObjectManager.ObjectType.RoomLight:
                case ObjectManager.ObjectType.Corpse:
                case ObjectManager.ObjectType.Worm:
                case ObjectManager.ObjectType.Goldfish:
                case ObjectManager.ObjectType.Horse:
                case ObjectManager.ObjectType.SnakeTank:
                case ObjectManager.ObjectType.CamperVan:
                    return false;
                default:
                    return true;
            }
        }

        private static string ResolveObjectSectionTitle(ObjectManager.ObjectType objectType)
        {
            string name = objectType.ToString().ToLowerInvariant();
            if (ContainsAny(name, "bench", "laboratory", "lab", "ammopress", "stove", "incinerator", "radio", "clipboard", "map", "computer"))
                return "Workbenches & Stations";
            if (ContainsAny(name, "generator", "filter", "door", "solar", "condenser", "recycling", "cryo", "rocket", "fabricator", "cctv", "switch"))
                return "Shelter Systems";
            if (ContainsAny(name, "storage", "pantry", "tank", "locker", "freezer", "wardrobe", "medicine", "itembin", "foodbowl", "planter"))
                return "Storage & Utility";
            return "Furniture & Misc";
        }

        private static string BuildObjectLabel(Obj_Base prefabComponent, ObjectManager.ObjectType objectType)
        {
            string localized = null;
            try
            {
                localized = prefabComponent != null ? prefabComponent.GetLocalizedObjectName() : null;
            }
            catch
            {
            }

            if (!string.IsNullOrEmpty(localized))
                return localized;

            return FormatObjectType(objectType.ToString());
        }

        private static string FormatObjectType(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "Object";

            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && current != '_' && char.IsUpper(current) && value[i - 1] != '_')
                    builder.Append(' ');
                builder.Append(current == '_' ? ' ' : current);
            }

            return builder.ToString();
        }

        private static Sprite ResolvePreviewSprite(GameObject prefab)
        {
            SpriteRenderer[] renderers = prefab != null ? prefab.GetComponentsInChildren<SpriteRenderer>(true) : null;
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].sprite != null)
                    return renderers[i].sprite;
            }

            return null;
        }

        private static Sprite ResolveGhostPreview(ObjectManager.ObjectType ghostType)
        {
            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
                return null;

            return ResolvePreviewSprite(manager.GetPrefab(ghostType, 1));
        }

        private static bool TryGetMouseWorldPoint(out Vector3 worldPoint)
        {
            worldPoint = Vector3.zero;
            Camera camera = Camera.main;
            if (camera == null)
            {
                Camera[] cameras = Camera.allCameras;
                if (cameras == null || cameras.Length == 0)
                    return false;
                camera = cameras[0];
            }

            Vector3 mouse = UnityEngine.Input.mousePosition;
            mouse.z = camera.orthographic ? Mathf.Abs(camera.transform.position.z) : camera.nearClipPlane;
            worldPoint = camera.ScreenToWorldPoint(mouse);
            return true;
        }

        private static float ComputeHorizontalPosition(ShelterRoomGrid grid, Vector3 worldPosition, int gridX)
        {
            if (grid == null)
                return 0.5f;

            float cellLeft = gridX * grid.grid_cell_width;
            float cellRight = cellLeft + grid.grid_cell_width;
            if (Mathf.Approximately(cellRight, cellLeft))
                return 0.5f;

            return Mathf.Clamp01((worldPosition.x - cellLeft) / (cellRight - cellLeft));
        }

        private static ScenarioProperty Property(string key, string value)
        {
            return new ScenarioProperty
            {
                Key = key,
                Value = value
            };
        }

        private static bool TryParseObjectPayload(string payload, out ObjectManager.ObjectType objectType, out int level)
        {
            objectType = ObjectManager.ObjectType.Undefined;
            level = 1;
            if (string.IsNullOrEmpty(payload))
                return false;

            string[] parts = payload.Split('|');
            if (parts.Length != 2 || !Enum.IsDefined(typeof(ObjectManager.ObjectType), parts[0]) || !int.TryParse(parts[1], out level))
                return false;

            objectType = (ObjectManager.ObjectType)Enum.Parse(typeof(ObjectManager.ObjectType), parts[0], true);
            return objectType != ObjectManager.ObjectType.Undefined && objectType != ObjectManager.ObjectType.Max;
        }

        private static string EncodeActionToken(string token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(token ?? string.Empty);
            return Convert.ToBase64String(bytes);
        }

        private static string DecodeActionToken(string encoded)
        {
            if (string.IsNullOrEmpty(encoded))
                return null;

            try
            {
                byte[] bytes = Convert.FromBase64String(encoded);
                return Encoding.UTF8.GetString(bytes);
            }
            catch
            {
                return null;
            }
        }

        private static int ComparePaletteEntries(PaletteEntryModel left, PaletteEntryModel right)
        {
            if (ReferenceEquals(left, right))
                return 0;
            if (left == null)
                return 1;
            if (right == null)
                return -1;

            return string.Compare(left.Label ?? string.Empty, right.Label ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ContainsAny(string value, params string[] parts)
        {
            if (string.IsNullOrEmpty(value) || parts == null)
                return false;

            for (int i = 0; i < parts.Length; i++)
            {
                if (!string.IsNullOrEmpty(parts[i]) && value.IndexOf(parts[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }
    }
}
