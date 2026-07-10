using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using ModAPI.Core;
using ModAPI.Scenarios;
using UnityEngine;
using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
namespace ShelteredAPI.Scenarios.Application.Authoring{
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
            public string TargetCell;
            public string Footprint;
            public bool? CanPlace;
            public string ValidationReason;
        }

        internal sealed class PlacementValidationResult
        {
            public int? GridX;
            public int? GridY;
            public bool CanPlace;
            public string Reason;
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
            public PlacementValidationResult Validation;
            public ScenarioPlacementFeelVisualService.GhostPreviewHandle GhostVisual;
            public bool SuppressPrimaryClickUntilClear;
            public Obj_Base CloneSourceObject;
        }

        private static readonly string[] ObjectSectionOrder = new[]
        {
            "Workbenches & Stations",
            "Shelter Systems",
            "Storage & Utility",
            "Furniture & Misc"
        };

        private static readonly FieldInfo ObjectManagerObjectsField = typeof(ObjectManager).GetField("objects", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo ObjectManagerSpawnedObjectIdField = typeof(ObjectManager).GetField("m_spawnedObjectId", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CraftingGhostImitatedTypeField = typeof(Obj_CraftingGhost).GetField("m_imitatedType", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CraftingGhostImitatedLevelField = typeof(Obj_CraftingGhost).GetField("m_imitatedLevel", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo CraftingGhostPlaceableOnSurfaceField = typeof(Obj_CraftingGhost).GetField("placableOnSurface", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly StructurePlacementService _structurePlacementService;
        private readonly ObjectPlacementService _objectPlacementService;
        private readonly WallWiringEditService _wallWiringEditService;
        private readonly PlacementPaletteService _placementPaletteService;
        private readonly RoomVisualPaletteService _roomVisualPaletteService;
        private readonly PlacementGhostSessionService _placementGhostSessionService;
        private readonly ScenarioBuildDeletionAuthoringService _deletionService;
        private readonly ScenarioPlacementFeelVisualService _placementFeelVisualService = new ScenarioPlacementFeelVisualService();
        private ActivePlacementSession _activePlacement;

        public static ScenarioBuildPlacementAuthoringService Instance
        {
            get { return ScenarioCompositionRoot.Resolve<ScenarioBuildPlacementAuthoringService>(); }
        }

        public bool HasActivePlacement
        {
            get { return _activePlacement != null && _activePlacement.Ghost != null; }
        }

        internal ScenarioBuildPlacementAuthoringService(
            StructurePlacementService structurePlacementService,
            ObjectPlacementService objectPlacementService,
            WallWiringEditService wallWiringEditService,
            PlacementPaletteService placementPaletteService,
            RoomVisualPaletteService roomVisualPaletteService,
            PlacementGhostSessionService placementGhostSessionService,
            ScenarioBuildDeletionAuthoringService deletionService)
        {
            _structurePlacementService = structurePlacementService;
            _objectPlacementService = objectPlacementService;
            _wallWiringEditService = wallWiringEditService;
            _placementPaletteService = placementPaletteService;
            _roomVisualPaletteService = roomVisualPaletteService;
            _placementGhostSessionService = placementGhostSessionService;
            _deletionService = deletionService;
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
                model.Title = "Placing: " + (_activePlacement.Label ?? "Item");
                model.Guidance = "Left-click place - Right-click/Esc cancel";
                PlacementValidationResult validation = EvaluateActivePlacement();
                _activePlacement.Validation = validation;
                if (validation != null)
                {
                    model.TargetCell = validation.GridX.HasValue && validation.GridY.HasValue
                        ? validation.GridX.Value + "," + validation.GridY.Value
                        : "<none>";
                    model.Footprint = BuildActiveFootprint();
                    model.CanPlace = validation.CanPlace;
                    model.ValidationReason = validation.Reason;
                }
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
                    model.Detail = "Placed objects are spawned live now and stored in the draft placement list.";
                    break;
            }

            return model;
        }

        private string BuildActiveFootprint()
        {
            if (_activePlacement == null)
                return "1 x 1 (1 cell)";

            int width = 1;
            int height = _activePlacement.Kind == PlacementSessionKind.Ladder ? 2 : 1;
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (_activePlacement.Kind == PlacementSessionKind.Object
                && grid != null
                && grid.grid_cell_width > 0.001f
                && _activePlacement.ColliderWidth > 0.001f)
            {
                width = Math.Max(1, Mathf.CeilToInt(_activePlacement.ColliderWidth / grid.grid_cell_width));
            }

            int cells = width * height;
            return width + " x " + height + " (" + cells + (cells == 1 ? " cell)" : " cells)");
        }

        public List<PaletteSectionModel> GetPaletteSections(ScenarioAuthoringState state, ScenarioEditorSession session)
        {
            ScenarioAuthoringTool tool = state != null ? state.ActiveTool : ScenarioAuthoringTool.Select;
            switch (tool)
            {
                case ScenarioAuthoringTool.Shelter:
                    return new List<PaletteSectionModel> { BuildStructureSection() };

                case ScenarioAuthoringTool.Wiring:
                    return BuildRoomVisualSections(state != null ? state.SelectedTarget : null);

                default:
                    return BuildObjectSections();
            }
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

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildPlacementCommitGridPrefix, StringComparison.Ordinal))
            {
                handled = true;
                return CommitPlacementAtGridCell(
                    actionId.Substring(ScenarioAuthoringActionIds.ActionBuildPlacementCommitGridPrefix.Length),
                    out message);
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

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildDeleteObject, StringComparison.Ordinal))
            {
                handled = true;
                return _deletionService.DeleteObject(state != null ? state.SelectedTarget : null, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildDeleteRoom, StringComparison.Ordinal))
            {
                handled = true;
                return _deletionService.DeleteRoom(state != null ? state.SelectedTarget : null, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildDeleteLadder, StringComparison.Ordinal))
            {
                handled = true;
                return _deletionService.DeleteLadder(state != null ? state.SelectedTarget : null, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildDeleteLight, StringComparison.Ordinal))
            {
                handled = true;
                return _deletionService.DeleteLight(state != null ? state.SelectedTarget : null, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildResetWall, StringComparison.Ordinal))
            {
                handled = true;
                return _deletionService.ResetWall(state != null ? state.SelectedTarget : null, out message);
            }

            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionBuildResetWire, StringComparison.Ordinal))
            {
                handled = true;
                return _deletionService.ResetWire(state != null ? state.SelectedTarget : null, out message);
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
                string token = actionId.Substring(ScenarioAuthoringActionIds.ActionBuildWallApplyPrefix.Length);
                int wallIndex;
                if (int.TryParse(token, out wallIndex))
                    return ApplyWall(state != null ? state.SelectedTarget : null, wallIndex, null, out message);

                string runtimeSpriteKey = DecodeActionToken(token);
                if (string.IsNullOrEmpty(runtimeSpriteKey))
                {
                    message = "The selected wall sprite could not be decoded.";
                    return false;
                }

                return ApplyWall(state != null ? state.SelectedTarget : null, -1, runtimeSpriteKey, out message);
            }

            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildWireApplyPrefix, StringComparison.Ordinal))
            {
                handled = true;
                string token = actionId.Substring(ScenarioAuthoringActionIds.ActionBuildWireApplyPrefix.Length);
                int wireIndex;
                if (int.TryParse(token, out wireIndex))
                    return ApplyWire(state != null ? state.SelectedTarget : null, wireIndex, null, out message);

                string runtimeSpriteKey = DecodeActionToken(token);
                if (string.IsNullOrEmpty(runtimeSpriteKey))
                {
                    message = "The selected wiring sprite could not be decoded.";
                    return false;
                }

                return ApplyWire(state != null ? state.SelectedTarget : null, -1, runtimeSpriteKey, out message);
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
            if ((inputCapture == null || !inputCapture.KeyboardShortcutHandled)
                && UnityEngine.Input.GetKeyDown(KeyCode.Escape))
                return CancelActivePlacement("Placement cancelled.", out message);

            if (_activePlacement.Ghost == null)
            {
                _activePlacement = null;
                _placementGhostSessionService.Clear();
                message = "The active placement preview was lost and has been reset.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Active placement preview was lost and reset.");
                return true;
            }

            bool suppressWorldInput = inputCapture != null && inputCapture.ShouldSuppressWorldInputNow();
            bool primaryClickConsumed = ConsumePlacementStartPrimaryClick();
            Vector3 worldPoint;
            if (TryGetMouseWorldPoint(out worldPoint))
                UpdateGhostPosition(worldPoint);

            if (!suppressWorldInput)
            {
                if (UnityEngine.Input.GetMouseButtonUp(1))
                    return CancelActivePlacement("Placement cancelled.", out message);

                if (UnityEngine.Input.GetMouseButtonUp(0) && !primaryClickConsumed && !IsEditorCameraDragPanning())
                    return TryCompletePlacement(session, out message);
            }

            _activePlacement.Validation = EvaluateActivePlacement();
            ApplyActiveGhostVisual();
            if (primaryClickConsumed && UnityEngine.Input.GetMouseButtonUp(0))
                return true;

            return false;
        }

        public static string BuildObjectActionId(ObjectManager.ObjectType objectType, int level)
        {
            string payload = objectType + "|" + level;
            return ScenarioAuthoringActionIds.ActionBuildObjectPlacePrefix + EncodeActionToken(payload);
        }

        internal static bool TryResolvePlaceableObject(
            ObjectManager manager,
            ObjectManager.ObjectType requestedType,
            int requestedLevel,
            string requestedLabel,
            out ObjectManager.ObjectType resolvedType,
            out int resolvedLevel,
            out GameObject prefab,
            out Obj_Base prefabComponent)
        {
            if (TryResolvePlaceablePrefab(manager, requestedType, requestedLevel, out resolvedType, out resolvedLevel, out prefab, out prefabComponent))
                return true;

            if (manager == null || string.IsNullOrEmpty(requestedLabel))
                return false;

            string normalizedLabel = NormalizeObjectLabel(requestedLabel);
            int maxValue = (int)ObjectManager.ObjectType.Max;
            for (int raw = 0; raw < maxValue; raw++)
            {
                ObjectManager.ObjectType candidateType = (ObjectManager.ObjectType)raw;
                if (!IsEligiblePaletteObject(candidateType, manager))
                    continue;

                ObjectManager.ObjectType candidateResolvedType;
                int candidateResolvedLevel;
                GameObject candidatePrefab;
                Obj_Base candidateComponent;
                if (!TryResolvePlaceablePrefab(manager, candidateType, 1, out candidateResolvedType, out candidateResolvedLevel, out candidatePrefab, out candidateComponent))
                    continue;

                string candidateLabel = NormalizeObjectLabel(BuildObjectLabel(candidateComponent, candidateType));
                string candidateTypeLabel = NormalizeObjectLabel(FormatObjectType(candidateType.ToString()));
                if (string.Equals(normalizedLabel, candidateLabel, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(normalizedLabel, candidateTypeLabel, StringComparison.OrdinalIgnoreCase))
                {
                    resolvedType = candidateResolvedType;
                    resolvedLevel = candidateResolvedLevel;
                    prefab = candidatePrefab;
                    prefabComponent = candidateComponent;
                    return true;
                }
            }

            return false;
        }

        public bool CancelForPlaytest(out string message)
        {
            return CancelActivePlacement("Placement cancelled before playtest started.", out message);
        }

        public bool CancelForToolSwitch(out string message)
        {
            return CancelActivePlacement("Placement cancelled because the active tool changed.", out message);
        }

        internal bool StartObjectClonePlacement(Obj_Base source, out string message)
        {
            message = null;
            if (source == null)
            {
                message = "No compatible prefab is available for the copied object.";
                return true;
            }

            ObjectManager.ObjectType objectType = source.GetObjectType();
            if (objectType == ObjectManager.ObjectType.Undefined || objectType == ObjectManager.ObjectType.Max)
            {
                message = "No compatible prefab is available for " + ScenarioBunkerDraftService.SafeObjectName(source) + ".";
                return true;
            }

            int level = source.objectLevel > 0 ? source.objectLevel : 1;
            ActivePlacementSession session = CreateGhostSession(ObjectManager.ObjectType.CraftingGhost, PlacementSessionKind.Object, ScenarioBunkerDraftService.SafeObjectName(source), out message);
            if (session == null)
                return true;

            Obj_CraftingGhost ghost = session.Ghost as Obj_CraftingGhost;
            if (ghost == null || !ConfigureGhostFromSource(ghost, source))
            {
                CancelActivePlacement(null);
                message = "Object placement could not start because the copied object has no placeable visual preview.";
                return true;
            }

            session.ObjectType = objectType;
            session.Level = level;
            session.PlaceableOnSurface = source.PlacableOnSurface;
            session.ColliderWidth = ResolveColliderWidth(source.gameObject);
            session.DefinitionReference = objectType.ToString();
            session.CloneSourceObject = source;
            _activePlacement = session;
            _placementGhostSessionService.Start(session.Label, objectType.ToString(), session.Ghost);
            ApplyActiveGhostVisual();
            LogPlacementInfo("Clone placement session started: " + session.Label + " (" + objectType + ").");
            message = "Placing copied " + session.Label + ". Left-click to place, right-click or Escape to cancel.";
            return true;
        }

        public bool CanDeleteObject(ScenarioAuthoringTarget target, out string reason)
        {
            reason = "Build deletion service is not ready.";
            return _deletionService != null && _deletionService.CanDeleteObject(target, out reason);
        }

        public bool CanDeleteRoom(ScenarioAuthoringTarget target, out string reason)
        {
            reason = "Build deletion service is not ready.";
            return _deletionService != null && _deletionService.CanDeleteRoom(target, out reason);
        }

        public bool CanDeleteLadder(ScenarioAuthoringTarget target, out string reason)
        {
            reason = "Build deletion service is not ready.";
            return _deletionService != null && _deletionService.CanDeleteLadder(target, out reason);
        }

        public bool CanDeleteLight(ScenarioAuthoringTarget target, out string reason)
        {
            reason = "Build deletion service is not ready.";
            return _deletionService != null && _deletionService.CanDeleteLight(target, out reason);
        }

        public bool CanResetWall(ScenarioAuthoringTarget target, out string reason)
        {
            reason = "Build deletion service is not ready.";
            return _deletionService != null && _deletionService.CanResetWall(target, out reason);
        }

        public bool CanResetWire(ScenarioAuthoringTarget target, out string reason)
        {
            reason = "Build deletion service is not ready.";
            return _deletionService != null && _deletionService.CanResetWire(target, out reason);
        }

        private static bool TryParseObjectAction(string actionId, out ObjectManager.ObjectType objectType, out int level)
        {
            objectType = ObjectManager.ObjectType.Undefined;
            level = 1;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(ScenarioAuthoringActionIds.ActionBuildObjectPlacePrefix, StringComparison.Ordinal))
                return false;

            return TryParseObjectPayload(DecodeActionToken(actionId.Substring(ScenarioAuthoringActionIds.ActionBuildObjectPlacePrefix.Length)), out objectType, out level);
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
            ShelterRoomGrid grid;
            if (!CanStartPlacement(out grid, out message))
                return false;

            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
            {
                message = "ObjectManager is not ready; object placement is unavailable.";
                return false;
            }

            ObjectManager.ObjectType resolvedObjectType;
            int resolvedLevel;
            GameObject prefab;
            Obj_Base prefabComponent;
            if (!TryResolvePlaceableObject(manager, objectType, level, null, out resolvedObjectType, out resolvedLevel, out prefab, out prefabComponent))
            {
                message = "No compatible prefab is available for " + objectType + ".";
                return false;
            }

            if (prefab == null || prefabComponent == null)
            {
                message = "No compatible prefab is available for " + objectType + ".";
                return false;
            }

            ActivePlacementSession session = CreateGhostSession(ObjectManager.ObjectType.CraftingGhost, PlacementSessionKind.Object, BuildObjectLabel(prefabComponent, resolvedObjectType), out message);
            if (session == null)
                return false;

            Obj_CraftingGhost ghost = session.Ghost as Obj_CraftingGhost;
            if (ghost == null)
            {
                CancelActivePlacement(null);
                message = "The crafting ghost prefab was not available for object placement.";
                return false;
            }

            ghost.ImitateObject(resolvedObjectType, resolvedLevel);
            ghost.SetIgnoresObjects(ResolveIgnoreMovementCollision(prefabComponent));
            session.ObjectType = resolvedObjectType;
            session.Level = resolvedLevel;
            session.DefinitionReference = resolvedObjectType.ToString();
            session.PlaceableOnSurface = prefabComponent.PlacableOnSurface;
            BoxCollider2D collider = prefabComponent.GetComponent<BoxCollider2D>();
            session.ColliderWidth = collider != null ? collider.size.x : 0f;
            _activePlacement = session;
            _placementGhostSessionService.Start(session.Label, resolvedObjectType.ToString(), session.Ghost);
            ApplyActiveGhostVisual();
            message = "Placing " + session.Label + ". Left-click to place, right-click or Escape to cancel.";
            LogPlacementInfo("Placement session started: " + session.Label + " (" + resolvedObjectType + ").");
            return true;
        }

        private bool StartRoomPlacement(out string message)
        {
            ShelterRoomGrid grid;
            if (!CanStartPlacement(out grid, out message))
                return false;

            ActivePlacementSession session = CreateGhostSession(ObjectManager.ObjectType.RoomGhost, PlacementSessionKind.Room, "Room Tile", out message);
            if (session == null)
                return false;

            session.DefinitionReference = ScenarioPlacementDefinitions.Room;
            _activePlacement = session;
            _placementGhostSessionService.Start(session.Label, session.DefinitionReference, session.Ghost);
            ApplyActiveGhostVisual();
            message = "Placing a room tile. Left-click to place, right-click or Escape to cancel.";
            LogPlacementInfo("Placement session started: Room Tile.");
            return true;
        }

        private bool StartLadderPlacement(out string message)
        {
            ShelterRoomGrid grid;
            if (!CanStartPlacement(out grid, out message))
                return false;

            ActivePlacementSession session = CreateGhostSession(ObjectManager.ObjectType.LadderGhost, PlacementSessionKind.Ladder, "Ladder", out message);
            if (session == null)
                return false;

            session.DefinitionReference = ScenarioPlacementDefinitions.Ladder;
            _activePlacement = session;
            _placementGhostSessionService.Start(session.Label, session.DefinitionReference, session.Ghost);
            ApplyActiveGhostVisual();
            message = "Placing a ladder. Left-click to place, right-click or Escape to cancel.";
            LogPlacementInfo("Placement session started: Ladder.");
            return true;
        }

        private bool StartRoomLightPlacement(out string message)
        {
            ShelterRoomGrid grid;
            if (!CanStartPlacement(out grid, out message))
                return false;

            ActivePlacementSession session = CreateGhostSession(ObjectManager.ObjectType.RoomLightGhost, PlacementSessionKind.RoomLight, "Room Light", out message);
            if (session == null)
                return false;

            session.DefinitionReference = ScenarioPlacementDefinitions.RoomLight;
            _activePlacement = session;
            _placementGhostSessionService.Start(session.Label, session.DefinitionReference, session.Ghost);
            ApplyActiveGhostVisual();
            message = "Placing a room light. Left-click to place, right-click or Escape to cancel.";
            LogPlacementInfo("Placement session started: Room Light.");
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

            Obj_Base ghostBase;
            try
            {
                ghostBase = manager.SpawnObject(ghostType, Vector2.zero);
            }
            catch (Exception ex)
            {
                message = "The required ghost prefab could not be spawned for placement.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Failed to spawn ghost " + ghostType + ": " + ex.Message);
                return null;
            }

            Obj_GhostBase ghost = ghostBase as Obj_GhostBase;
            if (ghost == null)
            {
                if (ghostBase != null)
                    manager.RemoveObject(ghostBase);
                message = "The required ghost prefab was not available for placement.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Ghost spawn returned no usable Obj_GhostBase for " + ghostType + ".");
                return null;
            }

            try
            {
                ghost.SetUpGhost(null, null);
            }
            catch (Exception ex)
            {
                RemoveGhostSafely(ghost);
                message = "The placement preview could not be initialized.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Failed to initialize ghost " + ghostType + ": " + ex.Message);
                return null;
            }

            ghost.transform.position = Vector3.zero;
            return new ActivePlacementSession
            {
                Kind = kind,
                Ghost = ghost,
                Label = label,
                Level = 1,
                GhostVisual = _placementFeelVisualService.CreateGhostPreview(ghost),
                SuppressPrimaryClickUntilClear = true
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
            _activePlacement.Validation = EvaluateActivePlacement();
            bool canPlace;
            try
            {
                canPlace = ghost.OnTryPlacement();
            }
            catch (Exception ex)
            {
                message = "The placement preview failed its placement check: " + ex.Message;
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement blocked by OnTryPlacement exception: " + ex.Message);
                return true;
            }

            if (!canPlace)
            {
                PlacementValidationResult validation = _activePlacement.Validation;
                message = validation != null && !string.IsNullOrEmpty(validation.Reason)
                    ? validation.Reason
                    : "That placement is blocked by the current shelter layout or collisions.";
                LogPlacementInfo("Placement blocked by OnTryPlacement for " + SafeActivePlacementLabel() + ".");
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

            if (!_objectPlacementService.CanRecordPlacement(out message))
                return CancelActivePlacement(message, out message);

            ObjectManager manager = ObjectManager.Instance;
            if (manager == null)
                return CancelActivePlacement("ObjectManager is not ready; object placement was cancelled before committing.", out message);

            Vector3 position = ghost.transform.position;
            ObjectManager.ObjectType objectType = _activePlacement.ObjectType;
            int level = _activePlacement.Level;
            string label = _activePlacement.Label;
            Obj_Base cloneSource = _activePlacement.CloneSourceObject;
            Obj_Base spawned;
            try
            {
                RestoreActiveGhostVisual();
                ghost.OnPlacementFinished();
                RemoveGhostSafely(ghost);
                spawned = _activePlacement.CloneSourceObject != null
                    ? SpawnCloneObject(manager, _activePlacement.CloneSourceObject, objectType, position)
                    : manager.SpawnObject(objectType, level, new Vector2(position.x, position.y));
            }
            catch (Exception ex)
            {
                _activePlacement = null;
                _placementGhostSessionService.Clear();
                RemoveGhostSafely(ghost);
                message = "Object placement failed while committing the final object: " + ex.Message;
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement commit failed for " + label + ": " + ex.Message);
                return true;
            }

            _activePlacement = null;
            _placementGhostSessionService.Clear();
            if (spawned == null)
            {
                message = "The final object could not be spawned after placement.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement commit failed; final object spawn returned null for " + label + ".");
                return true;
            }

            LogPlacementInfo("Placement committed to world: " + ScenarioBunkerDraftService.SafeObjectName(spawned) + ".");
            _placementFeelVisualService.PlaySettle(spawned.gameObject);
            RecordBunkerUndo(session, "Place object " + ScenarioBunkerDraftService.SafeObjectName(spawned));
            if (!_objectPlacementService.UpsertPlacement(_objectPlacementService.CapturePlacement(spawned)))
            {
                message = "Placed " + ScenarioBunkerDraftService.SafeObjectName(spawned) + ", but the scenario draft became unavailable before the placement could be recorded.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement could not record to draft: " + ScenarioBunkerDraftService.SafeObjectName(spawned) + ".");
                return true;
            }

            message = "Placed " + ScenarioBunkerDraftService.SafeObjectName(spawned) + " and recorded it in the scenario draft.";
            LogPlacementInfo("Placement recorded to draft: " + ScenarioBunkerDraftService.SafeObjectName(spawned) + ".");
            RestartPlacementForRepeat(PlacementSessionKind.Object, objectType, level, cloneSource, ref message);
            return true;
        }

        // Semantic route for headless authoring: arm a placement through the
        // asset browser, then commit it against an explicit shelter-grid cell.
        // This preserves the normal ghost validation and draft-recording path
        // without depending on a foreground window or native mouse input.
        private bool CommitPlacementAtGridCell(string payload, out string message)
        {
            message = null;
            if (!HasActivePlacement || _activePlacement == null || _activePlacement.Ghost == null)
            {
                message = "No active placement preview is available for grid commit.";
                return false;
            }

            string[] coordinates = (payload ?? string.Empty).Split('.');
            int gridX;
            int gridY;
            if (coordinates.Length != 2
                || !int.TryParse(coordinates[0], out gridX)
                || !int.TryParse(coordinates[1], out gridY))
            {
                message = "Grid placement commit requires integer coordinates: build.place.commit.grid.<x>.<y>.";
                return false;
            }

            ShelterRoomGrid grid;
            string gridReason;
            if (!TryGetReadyShelterGrid(out grid, out gridReason)
                || gridX < 0 || gridY < 0 || gridX >= grid.grid_width || gridY >= grid.grid_height)
            {
                message = string.IsNullOrEmpty(gridReason) ? "Grid placement target is outside the shelter grid." : gridReason;
                return false;
            }

            UpdateGhostPosition(ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY));
            return TryCompletePlacement(ScenarioEditorController.Instance.CurrentSession, out message);
        }

        private bool CompleteRoomPlacement(ScenarioEditorSession session, Obj_GhostBase ghost, out string message)
        {
            message = null;
            if (session == null || ghost == null)
            {
                message = "Room placement could not be completed because the ghost preview was unavailable.";
                return CancelActivePlacement(null);
            }

            ShelterRoomGrid grid;
            int gridX;
            int gridY;
            if (!TryResolveActiveGridCell(ghost.transform.position, out grid, out gridX, out gridY, out message))
            {
                return CancelActivePlacement(message ?? "Room placement could not resolve a shelter cell.", out message);
            }

            if (!_objectPlacementService.CanRecordPlacement(out message))
                return CancelActivePlacement(message, out message);

            string label = _activePlacement.Label;
            bool applied;
            try
            {
                RestoreActiveGhostVisual();
                ghost.OnPlacementFinished();
                applied = CraftingManager.FinishCraft_Room(null, null, ghost);
            }
            catch (Exception ex)
            {
                _activePlacement = null;
                _placementGhostSessionService.Clear();
                RemoveGhostSafely(ghost);
                message = "The room placement failed while committing: " + ex.Message;
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement commit failed for " + label + ": " + ex.Message);
                return true;
            }

            _activePlacement = null;
            _placementGhostSessionService.Clear();
            if (!applied)
            {
                RemoveGhostSafely(ghost);
                message = "The room could not be committed after the preview confirmed placement.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement commit failed: " + message);
                return true;
            }

            LogPlacementInfo("Placement committed to world: " + label + " at " + gridX + "," + gridY + ".");
            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            if (cell != null && cell.prefab != null)
                _placementFeelVisualService.PlaySettle(cell.prefab);
            string definitionReference = cell != null && cell.type == ShelterRoomGrid.CellType.RoomTop
                ? ScenarioPlacementDefinitions.RoomTop
                : ScenarioPlacementDefinitions.Room;
            RecordBunkerUndo(session, "Place room tile at " + gridX + "," + gridY);
            if (!_objectPlacementService.UpsertPlacement(_structurePlacementService.CreateRoomPlacement(
                    gridX,
                    gridY,
                    ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY),
                    BuildRoomIdentity(gridX, gridY),
                    definitionReference)))
            {
                message = "Placed a room tile at " + gridX + "," + gridY + ", but the scenario draft became unavailable before it could be recorded.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement could not record to draft: Room Tile at " + gridX + "," + gridY + ".");
                return true;
            }

            message = "Placed a room tile at " + gridX + "," + gridY + " and stored it in the draft.";
            LogPlacementInfo("Placement recorded to draft: " + definitionReference + " at " + gridX + "," + gridY + ".");
            RestartPlacementForRepeat(PlacementSessionKind.Room, ObjectManager.ObjectType.RoomGhost, 1, null, ref message);
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

            ShelterRoomGrid grid;
            int gridX;
            int gridY;
            if (!TryResolveActiveGridCell(ghost.transform.position, out grid, out gridX, out gridY, out message))
            {
                return CancelActivePlacement(message ?? "Ladder placement could not resolve a shelter cell.", out message);
            }

            float horizontalPos = ComputeHorizontalPosition(grid, ghost.transform.position, gridX);
            Vector3 ladderPosition = ghost.transform.position;
            if (!_objectPlacementService.CanRecordPlacement(out message))
                return CancelActivePlacement(message, out message);

            string label = _activePlacement.Label;
            bool applied;
            try
            {
                RestoreActiveGhostVisual();
                ghost.OnPlacementFinished();
                applied = CraftingManager.FinishCraft_Ladder(null, null, ghost);
            }
            catch (Exception ex)
            {
                _activePlacement = null;
                _placementGhostSessionService.Clear();
                RemoveGhostSafely(ghost);
                message = "The ladder placement failed while committing: " + ex.Message;
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement commit failed for " + label + ": " + ex.Message);
                return true;
            }

            _activePlacement = null;
            _placementGhostSessionService.Clear();
            if (!applied)
            {
                RemoveGhostSafely(ghost);
                message = "The ladder could not be committed after the preview confirmed placement.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement commit failed: " + message);
                return true;
            }

            LogPlacementInfo("Placement committed to world: " + label + " at " + gridX + "," + gridY + ".");
            GameObject ladderObject = ResolveLadderObject(grid, gridX, gridY);
            if (ladderObject != null)
                _placementFeelVisualService.PlaySettle(ladderObject);
            RecordBunkerUndo(session, "Place ladder at " + gridX + "," + gridY);
            if (!_objectPlacementService.UpsertPlacement(_structurePlacementService.CreateLadderPlacement(
                    gridX,
                    gridY,
                    ladderPosition,
                    BuildLadderIdentity(gridX, gridY),
                    horizontalPos)))
            {
                message = "Placed a ladder for room " + gridX + "," + gridY + ", but the scenario draft became unavailable before it could be recorded.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement could not record to draft: Ladder at " + gridX + "," + gridY + ".");
                return true;
            }

            message = "Placed a ladder for room " + gridX + "," + gridY + " and stored it in the draft.";
            LogPlacementInfo("Placement recorded to draft: Ladder at " + gridX + "," + gridY + ".");
            RestartPlacementForRepeat(PlacementSessionKind.Ladder, ObjectManager.ObjectType.LadderGhost, 1, null, ref message);
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

            ShelterRoomGrid grid;
            int gridX;
            int gridY;
            if (!TryResolveActiveGridCell(ghost.transform.position, out grid, out gridX, out gridY, out message))
            {
                return CancelActivePlacement(message ?? "Room light placement could not resolve a shelter cell.", out message);
            }

            if (!_objectPlacementService.CanRecordPlacement(out message))
                return CancelActivePlacement(message, out message);

            string label = _activePlacement.Label;
            bool applied;
            try
            {
                RestoreActiveGhostVisual();
                ghost.OnPlacementFinished();
                applied = CraftingManager.FinishCraft_Light(null, null, ghost);
            }
            catch (Exception ex)
            {
                _activePlacement = null;
                _placementGhostSessionService.Clear();
                RemoveGhostSafely(ghost);
                message = "The room light placement failed while committing: " + ex.Message;
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement commit failed for " + label + ": " + ex.Message);
                return true;
            }

            _activePlacement = null;
            _placementGhostSessionService.Clear();
            if (!applied)
            {
                RemoveGhostSafely(ghost);
                message = "The room light could not be committed after the preview confirmed placement.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement commit failed: " + message);
                return true;
            }

            LogPlacementInfo("Placement committed to world: " + label + " at " + gridX + "," + gridY + ".");
            ShelterRoomGrid.GridCell cell = grid.GetCell(gridX, gridY);
            if (cell != null && (UnityEngine.Object)cell.lightObject != (UnityEngine.Object)null)
                _placementFeelVisualService.PlaySettle(cell.lightObject.gameObject);
            RecordBunkerUndo(session, "Place room light at " + gridX + "," + gridY);
            if (!_objectPlacementService.UpsertPlacement(_structurePlacementService.CreateRoomLightPlacement(
                    gridX,
                    gridY,
                    ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY),
                    BuildLightIdentity(gridX, gridY))))
            {
                message = "Placed a room light at " + gridX + "," + gridY + ", but the scenario draft became unavailable before it could be recorded.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement could not record to draft: Room Light at " + gridX + "," + gridY + ".");
                return true;
            }

            message = "Placed a room light at " + gridX + "," + gridY + " and stored it in the draft.";
            LogPlacementInfo("Placement recorded to draft: Room Light at " + gridX + "," + gridY + ".");
            RestartPlacementForRepeat(PlacementSessionKind.RoomLight, ObjectManager.ObjectType.RoomLightGhost, 1, null, ref message);
            return true;
        }

        private bool ApplyWall(ScenarioAuthoringTarget target, int wallIndex, string runtimeSpriteKey, out string message)
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
            RoomVisualPaletteService.Entry entry = _roomVisualPaletteService.ResolveWallEntry(room, wallIndex, runtimeSpriteKey);
            if (entry == null || entry.Sprite == null)
            {
                message = "The selected wall sprite is not available for this room.";
                return false;
            }

            if (!_wallWiringEditService.CanRecordEdit(out message))
                return false;

            int appliedIndex = _roomVisualPaletteService.EnsureSprite(room.wallSprites, entry.Sprite);
            if (grid == null || appliedIndex < 0 || !grid.SetWall(gridX, gridY, appliedIndex))
            {
                message = "The selected wall sprite could not be applied to " + gridX + "," + gridY + ".";
                return false;
            }

            int serializedIndex = entry.NativeIndex >= 0 ? appliedIndex : -1;
            RecordBunkerUndo(ScenarioEditorController.Instance.CurrentSession, "Apply wall at " + gridX + "," + gridY);
            if (!_wallWiringEditService.ApplyWall(gridX, gridY, serializedIndex, entry.RuntimeSpriteKey))
            {
                message = "Applied wall sprite " + appliedIndex + " to room " + gridX + "," + gridY + ", but the scenario draft became unavailable before it could be recorded.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Wall edit could not record to draft at " + gridX + "," + gridY + ".");
                return true;
            }

            message = "Applied wall sprite " + appliedIndex + " to room " + gridX + "," + gridY + ".";
            LogPlacementInfo("Wall edit recorded to draft at " + gridX + "," + gridY + " sprite=" + appliedIndex + ".");
            return true;
        }

        private bool ApplyWire(ScenarioAuthoringTarget target, int wireIndex, string runtimeSpriteKey, out string message)
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
            List<Sprite> wireSprites = _roomVisualPaletteService.GetWireSprites(grid);
            RoomVisualPaletteService.Entry entry = _roomVisualPaletteService.ResolveWireEntry(wireSprites, wireIndex, runtimeSpriteKey);
            if (wireSprites == null || entry == null || entry.Sprite == null)
            {
                message = "The selected wiring sprite is not available for this shelter.";
                return false;
            }

            if (!_wallWiringEditService.CanRecordEdit(out message))
                return false;

            int appliedIndex = _roomVisualPaletteService.EnsureSprite(wireSprites, entry.Sprite);
            if (grid == null || appliedIndex < 0 || !grid.SetWiring(gridX, gridY, wireSprites[appliedIndex]))
            {
                message = "The selected wiring sprite could not be applied to " + gridX + "," + gridY + ".";
                return false;
            }

            int serializedIndex = entry.NativeIndex >= 0 ? appliedIndex : -1;
            RecordBunkerUndo(ScenarioEditorController.Instance.CurrentSession, "Apply wiring at " + gridX + "," + gridY);
            if (!_wallWiringEditService.ApplyWire(gridX, gridY, serializedIndex, entry.RuntimeSpriteKey))
            {
                message = "Applied wiring sprite " + appliedIndex + " to room " + gridX + "," + gridY + ", but the scenario draft became unavailable before it could be recorded.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Wiring edit could not record to draft at " + gridX + "," + gridY + ".");
                return true;
            }

            message = "Applied wiring sprite " + appliedIndex + " to room " + gridX + "," + gridY + ".";
            LogPlacementInfo("Wiring edit recorded to draft at " + gridX + "," + gridY + " sprite=" + appliedIndex + ".");
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
            _activePlacement.Validation = EvaluateActivePlacement();
            ApplyActiveGhostVisual();
        }

        private bool ConsumePlacementStartPrimaryClick()
        {
            if (_activePlacement == null || !_activePlacement.SuppressPrimaryClickUntilClear)
                return false;

            if (UnityEngine.Input.GetMouseButton(0) || UnityEngine.Input.GetMouseButtonUp(0))
                return true;

            _activePlacement.SuppressPrimaryClickUntilClear = false;
            return false;
        }

        private void ApplyActiveGhostVisual()
        {
            if (_activePlacement == null || _activePlacement.GhostVisual == null)
                return;

            _activePlacement.GhostVisual.Apply();
        }

        private void RestartPlacementForRepeat(
            PlacementSessionKind kind,
            ObjectManager.ObjectType objectType,
            int level,
            Obj_Base cloneSource,
            ref string message)
        {
            string restartMessage;
            switch (kind)
            {
                case PlacementSessionKind.Object:
                    if (cloneSource != null)
                        StartObjectClonePlacement(cloneSource, out restartMessage);
                    else
                        StartObjectPlacement(objectType, level, out restartMessage);
                    break;

                case PlacementSessionKind.Room:
                    StartRoomPlacement(out restartMessage);
                    break;

                case PlacementSessionKind.Ladder:
                    StartLadderPlacement(out restartMessage);
                    break;

                case PlacementSessionKind.RoomLight:
                    StartRoomLightPlacement(out restartMessage);
                    break;

                default:
                    restartMessage = "Unsupported repeat placement kind.";
                    break;
            }

            if (!HasActivePlacement && !string.IsNullOrEmpty(restartMessage))
                message = (message ?? string.Empty) + " Repeat placement stopped: " + restartMessage;
        }

        private static bool IsEditorCameraDragPanning()
        {
            try
            {
                ScenarioAuthoringEditorCameraService camera = ScenarioCompositionRoot.Resolve<ScenarioAuthoringEditorCameraService>();
                return camera != null && camera.ShouldSuppressSelectionClickThisFrame();
            }
            catch
            {
                return false;
            }
        }

        private void RestoreActiveGhostVisual()
        {
            if (_activePlacement == null || _activePlacement.GhostVisual == null)
                return;

            _activePlacement.GhostVisual.Restore();
            _activePlacement.GhostVisual = null;
        }

        private static bool ConfigureGhostFromSource(Obj_CraftingGhost ghost, Obj_Base source)
        {
            if (ghost == null || source == null)
                return false;

            SpriteRenderer sourceRenderer = ResolvePreviewRenderer(source.gameObject);
            SpriteRenderer ghostRenderer = ResolvePreviewRenderer(ghost.gameObject);
            BoxCollider2D sourceCollider = source.GetComponent<BoxCollider2D>();
            BoxCollider2D ghostCollider = ghost.GetComponent<BoxCollider2D>();
            if (sourceRenderer == null || sourceRenderer.sprite == null || ghostRenderer == null || sourceCollider == null || ghostCollider == null)
                return false;

            ghostRenderer.sprite = sourceRenderer.sprite;
            ghostRenderer.transform.localPosition = sourceRenderer.transform.localPosition;
            ghostRenderer.transform.localRotation = sourceRenderer.transform.localRotation;
            ghostRenderer.transform.localScale = sourceRenderer.transform.localScale;
            ghostCollider.size = sourceCollider.size;
            ghostCollider.offset = sourceCollider.offset;
            ghost.constructionSprites = source.constructionSprites;
            ghost.craftAnimation = source.craftAnimation;

            SetFieldValue(CraftingGhostImitatedTypeField, ghost, source.GetObjectType());
            SetFieldValue(CraftingGhostImitatedLevelField, ghost, source.objectLevel > 0 ? source.objectLevel : 1);
            SetFieldValue(CraftingGhostPlaceableOnSurfaceField, ghost, source.PlacableOnSurface);
            ghost.SetIgnoresObjects(ResolveIgnoreMovementCollision(source));
            return true;
        }

        private static SpriteRenderer ResolvePreviewRenderer(GameObject gameObject)
        {
            SpriteRenderer[] renderers = gameObject != null ? gameObject.GetComponentsInChildren<SpriteRenderer>(true) : null;
            for (int i = 0; renderers != null && i < renderers.Length; i++)
                if (renderers[i] != null && renderers[i].sprite != null)
                    return renderers[i];

            return null;
        }

        private static float ResolveColliderWidth(GameObject gameObject)
        {
            BoxCollider2D collider = gameObject != null ? gameObject.GetComponent<BoxCollider2D>() : null;
            return collider != null ? collider.size.x : 0f;
        }

        private static Obj_Base SpawnCloneObject(ObjectManager manager, Obj_Base source, ObjectManager.ObjectType objectType, Vector3 position)
        {
            if (manager == null || source == null)
                return null;

            GameObject clone = UnityEngine.Object.Instantiate(source.gameObject, new Vector3(position.x, position.y, 0f), Quaternion.identity) as GameObject;
            Obj_Base spawned = clone != null ? clone.GetComponent<Obj_Base>() : null;
            if (spawned == null)
            {
                if (clone != null)
                    UnityEngine.Object.Destroy(clone);
                return null;
            }

            if (manager.spawned_objects != null)
                clone.transform.parent = manager.spawned_objects.transform;
            spawned.SetObjectId(NextSpawnedObjectId(manager), false);
            spawned.movable = source.movable;
            RegisterSpawnedObject(manager, objectType, spawned);
            return spawned;
        }

        private static int NextSpawnedObjectId(ObjectManager manager)
        {
            if (manager == null || ObjectManagerSpawnedObjectIdField == null)
                return 0;

            int next = 0;
            try
            {
                object value = ObjectManagerSpawnedObjectIdField.GetValue(manager);
                if (value != null && value.GetType() == typeof(int))
                    next = (int)value;
                ObjectManagerSpawnedObjectIdField.SetValue(manager, next + 1);
            }
            catch
            {
                return 0;
            }

            return next;
        }

        private static void RegisterSpawnedObject(ObjectManager manager, ObjectManager.ObjectType objectType, Obj_Base spawned)
        {
            if (manager == null || spawned == null || ObjectManagerObjectsField == null)
                return;

            try
            {
                Dictionary<ObjectManager.ObjectType, List<Obj_Base>> objects = ObjectManagerObjectsField.GetValue(manager) as Dictionary<ObjectManager.ObjectType, List<Obj_Base>>;
                if (objects == null)
                    return;

                List<Obj_Base> typedObjects;
                if (!objects.TryGetValue(objectType, out typedObjects) || typedObjects == null)
                {
                    typedObjects = new List<Obj_Base>();
                    objects[objectType] = typedObjects;
                }

                if (!typedObjects.Contains(spawned))
                    typedObjects.Add(spawned);
            }
            catch
            {
            }
        }

        private static void SetFieldValue(FieldInfo field, object target, object value)
        {
            if (field == null || target == null)
                return;

            try
            {
                field.SetValue(target, value);
            }
            catch
            {
            }
        }

        private PlacementValidationResult EvaluateActivePlacement()
        {
            if (!HasActivePlacement || _activePlacement.Ghost == null)
                return null;

            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            int gridX = -1;
            int gridY = -1;
            bool hasCell = TryResolveActiveGridCell(_activePlacement.Ghost.transform.position, out grid, out gridX, out gridY);
            ShelterRoomGrid.GridCell cell = hasCell ? grid.GetCell(gridX, gridY) : null;
            bool ghostAllowsPlacement = _activePlacement.Ghost.IsPlacable();
            PlacementValidationResult result = new PlacementValidationResult
            {
                GridX = hasCell ? (int?)gridX : null,
                GridY = hasCell ? (int?)gridY : null,
                CanPlace = ghostAllowsPlacement,
                Reason = ghostAllowsPlacement ? "Valid target." : "That placement is blocked by the current shelter layout or collisions."
            };

            switch (_activePlacement.Kind)
            {
                case PlacementSessionKind.Room:
                    PopulateRoomValidation(result, cell, ghostAllowsPlacement);
                    break;
                case PlacementSessionKind.Ladder:
                    PopulateLadderValidation(result, grid, gridX, gridY, hasCell, cell, ghostAllowsPlacement);
                    break;
                case PlacementSessionKind.RoomLight:
                    PopulateLightValidation(result, cell, ghostAllowsPlacement);
                    break;
                case PlacementSessionKind.Object:
                    PopulateObjectValidation(result, cell, ghostAllowsPlacement);
                    break;
            }

            return result;
        }

        private static void PopulateRoomValidation(PlacementValidationResult result, ShelterRoomGrid.GridCell cell, bool ghostAllowsPlacement)
        {
            if (cell == null)
            {
                result.CanPlace = false;
                result.Reason = "Target is outside the shelter grid.";
                return;
            }

            if (cell.type != ShelterRoomGrid.CellType.Dirt)
            {
                result.CanPlace = false;
                result.Reason = "Rooms must sit on dirt next to an existing room.";
                return;
            }

            if (!IsRoomGridCellValid(cell))
            {
                result.CanPlace = false;
                result.Reason = "Rooms must sit on dirt next to an existing room.";
                return;
            }

            result.CanPlace = ghostAllowsPlacement;
            result.Reason = ghostAllowsPlacement ? "Room can be placed here." : "Rooms must sit on dirt next to an existing room.";
        }

        private static void PopulateLadderValidation(PlacementValidationResult result, ShelterRoomGrid grid, int gridX, int gridY, bool hasCell, ShelterRoomGrid.GridCell cell, bool ghostAllowsPlacement)
        {
            if (!hasCell || cell == null)
            {
                result.CanPlace = false;
                result.Reason = "Target is outside the shelter grid.";
                return;
            }

            if (!IsRoomOrTop(cell))
            {
                result.CanPlace = false;
                result.Reason = "Ladder needs clear cells above and below.";
                return;
            }

            ShelterRoomGrid.GridCell below = grid != null ? grid.GetCell(gridX, gridY + 1) : null;
            if (below == null || (below.type != ShelterRoomGrid.CellType.Room && below.type != ShelterRoomGrid.CellType.InProgress))
            {
                result.CanPlace = false;
                result.Reason = "Ladder needs clear cells above and below.";
                return;
            }

            result.CanPlace = ghostAllowsPlacement;
            result.Reason = ghostAllowsPlacement ? "Ladder can be placed here." : "Ladder needs clear cells above and below.";
        }

        private static void PopulateLightValidation(PlacementValidationResult result, ShelterRoomGrid.GridCell cell, bool ghostAllowsPlacement)
        {
            if (cell == null)
            {
                result.CanPlace = false;
                result.Reason = "Target is outside the shelter grid.";
                return;
            }

            if (!IsRoomOrTop(cell))
            {
                result.CanPlace = false;
                result.Reason = "Room lights can only be placed in Room or RoomTop cells.";
                return;
            }

            if ((UnityEngine.Object)cell.lightObject != (UnityEngine.Object)null)
            {
                result.CanPlace = false;
                result.Reason = "A light already exists in this room.";
                return;
            }

            result.CanPlace = ghostAllowsPlacement;
            result.Reason = ghostAllowsPlacement ? "Room light can be placed here." : "A light already exists in this room.";
        }

        private void PopulateObjectValidation(PlacementValidationResult result, ShelterRoomGrid.GridCell cell, bool ghostAllowsPlacement)
        {
            if (cell == null)
            {
                result.CanPlace = false;
                result.Reason = "Target is outside the shelter grid.";
                return;
            }

            if (_activePlacement != null && _activePlacement.PlaceableOnSurface && cell.type == ShelterRoomGrid.CellType.Surface)
            {
                result.CanPlace = ghostAllowsPlacement;
                result.Reason = ghostAllowsPlacement ? "Surface object can be placed here." : "Surface object placement is blocked by a collision.";
                return;
            }

            if (!IsRoomOrTop(cell))
            {
                result.CanPlace = false;
                result.Reason = _activePlacement != null && _activePlacement.PlaceableOnSurface
                    ? "Objects must target a room cell or valid surface."
                    : "This object must be placed in a Room or RoomTop cell.";
                return;
            }

            result.CanPlace = ghostAllowsPlacement;
            result.Reason = ghostAllowsPlacement ? "Object can be placed here." : "Object placement is blocked by another object, door, wall, ladder, or ghost.";
        }

        private static bool HasNeighborRoom(ShelterRoomGrid.GridCell cell)
        {
            if (cell == null || cell.neighbours == null)
                return false;

            for (int i = 0; i < cell.neighbours.Length; i++)
            {
                if (i != 1 && IsRoomOrTop(cell.neighbours[i]))
                    return true;
            }

            return false;
        }

        // This is intentionally the single structural rule used by both the
        // interactive ghost commit and external candidate discovery.  The
        // latter reaches it through the harness' reflection seam because the
        // authoring service remains internal to the product assembly.
        public static bool IsRoomGridCellValid(ShelterRoomGrid.GridCell cell)
        {
            return cell != null
                && cell.type == ShelterRoomGrid.CellType.Dirt
                && HasNeighborRoom(cell);
        }

        private static bool IsRoomOrTop(ShelterRoomGrid.GridCell cell)
        {
            return cell != null
                && (cell.type == ShelterRoomGrid.CellType.Room || cell.type == ShelterRoomGrid.CellType.RoomTop);
        }

        private static GameObject ResolveLadderObject(ShelterRoomGrid grid, int gridX, int gridY)
        {
            ShelterRoomGrid.GridCell cell = grid != null ? grid.GetCell(gridX, gridY) : null;
            List<ShelterLadder> ladders = cell != null ? cell.ladders : null;
            if (ladders == null || ladders.Count <= 0)
                return null;

            ShelterLadder ladder = ladders[ladders.Count - 1];
            if ((UnityEngine.Object)ladder == (UnityEngine.Object)null)
                return null;

            return ladder.gameObject;
        }

        private Vector3 ResolveObjectPlacementPosition(Vector3 worldPoint)
        {
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            if (grid == null)
                return worldPoint;

            Vector3 origin = grid.transform.position;
            float width = Mathf.Max(0f, _activePlacement != null ? _activePlacement.ColliderWidth : 0f);
            float halfWidth = width * 0.5f;
            float minX = origin.x + halfWidth;
            float maxX = origin.x + (grid.grid_width * grid.grid_cell_width) - halfWidth;
            if (maxX < minX)
                maxX = minX;

            float minY;
            float maxY;
            if (_activePlacement != null && _activePlacement.PlaceableOnSurface)
            {
                minY = origin.y - grid.grid_cell_height;
                maxY = origin.y;
            }
            else
            {
                minY = origin.y - ((grid.grid_height - 1) * grid.grid_cell_height);
                maxY = origin.y - grid.grid_cell_height;
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

        private static bool TryResolveActiveGridCell(Vector3 worldPosition, out ShelterRoomGrid grid, out int gridX, out int gridY)
        {
            string ignored;
            return TryResolveActiveGridCell(worldPosition, out grid, out gridX, out gridY, out ignored);
        }

        private static bool TryResolveActiveGridCell(Vector3 worldPosition, out ShelterRoomGrid grid, out int gridX, out int gridY, out string message)
        {
            gridX = -1;
            gridY = -1;
            if (!TryGetReadyShelterGrid(out grid, out message))
                return false;

            if (ScenarioGridSnapService.TryGetCell(worldPosition, out gridX, out gridY))
                return true;

            if (grid.WorldCoordsToCellCoords(worldPosition, out gridX, out gridY))
                return true;

            message = "Target is outside the shelter grid.";
            return false;
        }

        private static Vector3 ResolveLadderPlacementPosition(Vector3 worldPoint)
        {
            ShelterRoomGrid grid = ShelterRoomGrid.Instance;
            int gridX;
            int gridY;
            if (grid == null || !ScenarioGridSnapService.TryGetCell(worldPoint, out gridX, out gridY))
                return worldPoint;

            Vector3 snapped = ScenarioGridSnapService.GetCellCenterWorldPosition(gridX, gridY);
            float cellLeft = grid.transform.position.x + (gridX * grid.grid_cell_width);
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
            string label = _activePlacement.Label;
            RestoreActiveGhostVisual();
            _activePlacement = null;
            _placementGhostSessionService.Clear();
            RemoveGhostSafely(ghost);
            if (!string.IsNullOrEmpty(message))
                LogPlacementInfo("Placement cancelled: " + (label ?? "unknown") + ". " + message);
            return true;
        }

        private bool CanStartPlacement(out ShelterRoomGrid grid, out string message)
        {
            grid = null;
            if (ScenarioAuthoringRuntimeGuards.IsPlaytesting())
            {
                message = "End playtest before starting a new placement.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement blocked while playtest is running.");
                return false;
            }

            if (!_objectPlacementService.CanRecordPlacement(out message))
            {
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement blocked: " + (message ?? "draft unavailable"));
                return false;
            }

            if (!TryGetReadyShelterGrid(out grid, out message))
            {
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement blocked: " + (message ?? "shelter grid unavailable"));
                return false;
            }

            if (ObjectManager.Instance == null)
            {
                message = "ObjectManager is not ready; placement is unavailable.";
                MMLog.WriteWarning("[ScenarioBuildPlacement] Placement blocked: " + message);
                return false;
            }

            return true;
        }

        private static bool TryGetReadyShelterGrid(out ShelterRoomGrid grid, out string message)
        {
            grid = ShelterRoomGrid.Instance;
            if (grid == null)
            {
                message = "ShelterRoomGrid is not ready; placement is unavailable.";
                return false;
            }

            if (!grid.isInitialized)
            {
                message = "ShelterRoomGrid is not initialized yet; placement is unavailable.";
                return false;
            }

            message = null;
            return true;
        }

        private void RemoveGhostSafely(Obj_GhostBase ghost)
        {
            if (ghost == null)
                return;

            ObjectManager manager = ObjectManager.Instance;
            if (manager != null)
            {
                try
                {
                    manager.RemoveObject(ghost);
                    return;
                }
                catch (Exception ex)
                {
                    MMLog.WriteWarning("[ScenarioBuildPlacement] ObjectManager failed to remove placement ghost: " + ex.Message);
                }
            }

            try
            {
                UnityEngine.Object.Destroy(ghost.gameObject);
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioBuildPlacement] Failed to clean up placement ghost: " + ex.Message);
            }
        }

        private string SafeActivePlacementLabel()
        {
            return _activePlacement != null && !string.IsNullOrEmpty(_activePlacement.Label)
                ? _activePlacement.Label
                : "unknown placement";
        }

        private static void LogPlacementInfo(string message)
        {
            if (!string.IsNullOrEmpty(message))
                MMLog.WriteInfo("[ScenarioBuildPlacement] " + message);
        }

        private List<PaletteSectionModel> BuildObjectSections()
        {
            List<PaletteSectionModel> sections = new List<PaletteSectionModel>();
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
                return sections;
            }

            Dictionary<string, List<PlacementPaletteService.PaletteEntry>> grouped = new Dictionary<string, List<PlacementPaletteService.PaletteEntry>>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < ObjectSectionOrder.Length; i++)
                grouped[ObjectSectionOrder[i]] = new List<PlacementPaletteService.PaletteEntry>();

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
                grouped[sectionTitle].Add(_placementPaletteService.CreateEntry(
                    sectionTitle,
                    BuildObjectActionId(objectType, component.objectLevel > 0 ? component.objectLevel : 1),
                    BuildObjectLabel(component, objectType),
                    "Uses Sheltered's crafting ghost preview, then places the final object instantly into the scenario draft."));
            }

            for (int i = 0; i < ObjectSectionOrder.Length; i++)
            {
                string title = ObjectSectionOrder[i];
                List<PlacementPaletteService.PaletteEntry> entries;
                if (!grouped.TryGetValue(title, out entries))
                    entries = new List<PlacementPaletteService.PaletteEntry>();

                sections.Add(new PaletteSectionModel
                {
                    Id = "objects_" + title.Replace(" ", "_").ToLowerInvariant(),
                    Title = title,
                    EmptyMessage = "No compatible prefabs are currently loaded for this category.",
                    Entries = BuildObjectEntries(entries, manager)
                });
            }

            return sections;
        }

        private PaletteSectionModel BuildStructureSection()
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
                Active = _activePlacement != null && _activePlacement.Kind == PlacementSessionKind.Room
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
                Active = _activePlacement != null && _activePlacement.Kind == PlacementSessionKind.Ladder
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
                Active = _activePlacement != null && _activePlacement.Kind == PlacementSessionKind.RoomLight
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

        private List<PaletteSectionModel> BuildRoomVisualSections(ScenarioAuthoringTarget target)
        {
            List<PaletteSectionModel> sections = new List<PaletteSectionModel>();
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
                return sections;
            }

            int activeWallIndex = room.GetWallSprite();
            Sprite activeWall = activeWallIndex >= 0 && room.wallSprites != null && activeWallIndex < room.wallSprites.Count ? room.wallSprites[activeWallIndex] : null;
            string activeWallKey = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(activeWall);
            List<RoomVisualPaletteService.Entry> wallPalette = _roomVisualPaletteService.BuildWallPalette(room);
            List<PaletteEntryModel> wallEntries = new List<PaletteEntryModel>();
            for (int i = 0; wallPalette != null && i < wallPalette.Count; i++)
            {
                RoomVisualPaletteService.Entry entry = wallPalette[i];
                bool active = entry != null && string.Equals(activeWallKey, entry.RuntimeSpriteKey, StringComparison.OrdinalIgnoreCase);
                int displayIndex = entry != null && entry.NativeIndex >= 0 ? entry.NativeIndex : i;
                wallEntries.Add(new PaletteEntryModel
                {
                    ActionId = ScenarioAuthoringActionIds.ActionBuildWallApplyPrefix + EncodeActionToken(entry != null ? entry.RuntimeSpriteKey : null),
                    Label = "Wall " + (displayIndex + 1),
                    Hint = "Apply wall sprite " + displayIndex + " to room " + gridX + "," + gridY + ".",
                    Source = entry != null && !string.IsNullOrEmpty(entry.SourceLabel) ? entry.SourceLabel : ("Room " + gridX + "," + gridY),
                    Badge = active ? "LIVE" : "WALL",
                    Preview = entry != null ? entry.Sprite : null,
                    Enabled = true,
                    Active = active
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
            List<Sprite> wireSprites = _roomVisualPaletteService.GetWireSprites(grid);
            Sprite activeWire = room.GetWires();
            string activeWireKey = ScenarioSpriteReferenceLibrary.CreateRuntimeSpriteKey(activeWire);
            List<RoomVisualPaletteService.Entry> wirePalette = _roomVisualPaletteService.BuildWirePalette(wireSprites);
            for (int i = 0; wirePalette != null && i < wirePalette.Count; i++)
            {
                RoomVisualPaletteService.Entry entry = wirePalette[i];
                bool active = entry != null && string.Equals(activeWireKey, entry.RuntimeSpriteKey, StringComparison.OrdinalIgnoreCase);
                int displayIndex = entry != null && entry.NativeIndex >= 0 ? entry.NativeIndex : i;
                wireEntries.Add(new PaletteEntryModel
                {
                    ActionId = ScenarioAuthoringActionIds.ActionBuildWireApplyPrefix + EncodeActionToken(entry != null ? entry.RuntimeSpriteKey : null),
                    Label = "Wire " + (displayIndex + 1),
                    Hint = "Apply wiring sprite " + displayIndex + " to room " + gridX + "," + gridY + ".",
                    Source = entry != null && !string.IsNullOrEmpty(entry.SourceLabel) ? entry.SourceLabel : ("Room " + gridX + "," + gridY),
                    Badge = active ? "LIVE" : "WIRE",
                    Preview = entry != null ? entry.Sprite : null,
                    Enabled = true,
                    Active = active
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
            return sections;
        }

        private List<PaletteEntryModel> BuildObjectEntries(IEnumerable<PlacementPaletteService.PaletteEntry> entries, ObjectManager manager)
        {
            List<PaletteEntryModel> models = new List<PaletteEntryModel>();
            if (entries == null || manager == null)
                return models;

            foreach (PlacementPaletteService.PaletteEntry entry in entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.ActionId))
                    continue;

                ObjectManager.ObjectType objectType;
                int level;
                if (!TryParseObjectAction(entry.ActionId, out objectType, out level))
                    continue;

                GameObject prefab = manager.GetPrefab(objectType, level);
                models.Add(new PaletteEntryModel
                {
                    ActionId = entry.ActionId,
                    Label = entry.Label,
                    Hint = entry.Hint,
                    Source = objectType.ToString(),
                    Badge = "OBJ",
                    Preview = ResolvePreviewSprite(prefab),
                    Enabled = true,
                    Active = _activePlacement != null
                        && _activePlacement.Kind == PlacementSessionKind.Object
                        && _activePlacement.ObjectType == objectType
                });
            }

            models.Sort(ComparePaletteEntries);
            return models;
        }

        private static bool ResolveIgnoreMovementCollision(Obj_Base prefabComponent)
        {
            return prefabComponent != null && prefabComponent.IgnoreMovementCollision;
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

            float cellLeft = grid.transform.position.x + (gridX * grid.grid_cell_width);
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

        private static bool TryResolvePlaceablePrefab(
            ObjectManager manager,
            ObjectManager.ObjectType objectType,
            int requestedLevel,
            out ObjectManager.ObjectType resolvedType,
            out int resolvedLevel,
            out GameObject prefab,
            out Obj_Base prefabComponent)
        {
            resolvedType = objectType;
            resolvedLevel = requestedLevel > 0 ? requestedLevel : 1;
            prefab = null;
            prefabComponent = null;

            if (manager == null || !IsEligiblePaletteObject(objectType, manager))
                return false;

            prefab = manager.GetPrefab(objectType, resolvedLevel);
            prefabComponent = prefab != null ? prefab.GetComponent<Obj_Base>() : null;
            if (prefab == null || prefabComponent == null)
                return false;

            resolvedLevel = prefabComponent.objectLevel > 0 ? prefabComponent.objectLevel : resolvedLevel;
            return true;
        }

        private static string NormalizeObjectLabel(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Trim();
        }

        private static string EncodeActionToken(string token)
        {
            return ScenarioAuthoringActionCodec.EncodeToken(token);
        }

        private static string DecodeActionToken(string encoded)
        {
            return ScenarioAuthoringActionCodec.DecodeToken(encoded);
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

        private static void RecordBunkerUndo(ScenarioEditorSession session, string description)
        {
            if (session == null || session.WorkingDefinition == null)
                return;

            ScenarioAuthoringHistoryService.Instance.RecordBunkerChange(session.WorkingDefinition, description);
        }
    }
}
