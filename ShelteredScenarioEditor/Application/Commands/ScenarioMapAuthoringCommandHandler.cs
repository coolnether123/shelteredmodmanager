using System;
using System.Globalization;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Map;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;

namespace ShelteredScenarioEditor.Application.Commands{
    internal sealed class ScenarioMapAuthoringCommandHandler : IScenarioCommandHandler
    {
        private readonly IScenarioMapAuthoringRuntime _runtimeService;
        private readonly ScenarioVanillaInteractionRuntimeService _vanillaInteraction;
        private readonly ScenarioMapDraftService _draftService;
        private readonly ScenarioAuthoringLayoutService _layoutService;
        private readonly IScenarioEditorService _editorService;
        private readonly ScenarioAuthoringHistoryService _historyService;
        private readonly ScenarioAuthoringRendererInteractionState _rendererInteraction;

        public ScenarioMapAuthoringCommandHandler(
            IScenarioMapAuthoringRuntime runtimeService,
            ScenarioVanillaInteractionRuntimeService vanillaInteraction,
            ScenarioMapDraftService draftService,
            ScenarioAuthoringLayoutService layoutService,
            IScenarioEditorService editorService,
            ScenarioAuthoringHistoryService historyService,
            ScenarioAuthoringRendererInteractionState rendererInteraction)
        {
            _runtimeService = runtimeService;
            _vanillaInteraction = vanillaInteraction;
            _draftService = draftService;
            _layoutService = layoutService;
            _editorService = editorService;
            _historyService = historyService;
            _rendererInteraction = rendererInteraction;
        }

        public bool CanHandle(ScenarioAuthoringCommand command)
        {
            return command is MapAuthoringCommand;
        }

        public ScenarioCommandDispatchResult Handle(ScenarioAuthoringState state, ScenarioAuthoringCommand command)
        {
            MapAuthoringCommand map = command as MapAuthoringCommand;
            if (state == null || map == null)
                return Result(false, "Map authoring is unavailable.");

            string message;
            bool changed;
            switch (map.Kind)
            {
                case MapAuthoringCommandKind.OpenMap: changed = OpenMapAuthoring(state, out message); break;
                case MapAuthoringCommandKind.CloseMap: changed = CloseMapAuthoring(state, out message); break;
                case MapAuthoringCommandKind.CaptureSelection: changed = CaptureSelection(state, out message); break;
                case MapAuthoringCommandKind.SetMode: changed = SetMode(state, map, out message); break;
                case MapAuthoringCommandKind.SetBrushShape: changed = SetBrushShape(state, map.BrushShape, out message); break;
                case MapAuthoringCommandKind.SetBrushSize: changed = SetBrushSize(state, map.BrushSize, out message); break;
                case MapAuthoringCommandKind.SelectWorldPosition: changed = SelectWorldPosition(state, map.WorldX, map.WorldY, out message); break;
                case MapAuthoringCommandKind.ClickWorldPosition: changed = ClickWorldPosition(state, map.WorldX, map.WorldY, out message); break;
                case MapAuthoringCommandKind.SelectLocation: changed = SelectAuthoredLocation(state, map.LocationId, out message); break;
                case MapAuthoringCommandKind.BeginDuplicateLocation: changed = BeginDuplicateLocation(state, map.LocationId, out message); break;
                case MapAuthoringCommandKind.EditLocationField: changed = EditLocationField(state, map.LocationField, map.LocationId, map.Value, out message); break;
                case MapAuthoringCommandKind.ToggleLocationField: changed = ToggleLocationField(state, map.LocationField, map.LocationId, out message); break;
                case MapAuthoringCommandKind.CycleLocationIcon: changed = CycleLocationIcon(state, map.LocationId, out message); break;
                default: return Result(false, "Map authoring command is not supported.");
            }

            return Result(changed, message);
        }

        private bool OpenMapAuthoring(ScenarioAuthoringState state, out string message)
        {
            message = null;
            if (ScenarioAuthoringRuntimeGuards.IsPlaytesting())
            {
                message = "Stop playtest before opening the map authoring surface.";
                return false;
            }

            string blockingReason;
            if (!ShelteredScenarioRuntime.IsWorldReady(out blockingReason))
            {
                message = blockingReason;
                return false;
            }

            if (_runtimeService == null || !_runtimeService.OpenVanillaMap())
            {
                message = "The vanilla map panel is not available yet.";
                return false;
            }

            if (_layoutService != null)
            {
                _layoutService.SelectStage(state, ScenarioStageKind.Map);
                _layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.Map, true);
            }
            state.ActiveStage = ScenarioStageKind.Map;
            state.ActiveShellTab = ScenarioAuthoringShellTab.Map;
            state.MapAuthoringPreviousShellVisible = state.ShellVisible;
            state.MapAuthoringActive = true;
            if (string.IsNullOrEmpty(state.MapAuthoringMode))
                state.MapAuthoringMode = "select";
            state.ShellVisible = false;
            state.StatusMessage = "Map editor open. Choose a location or paint terrain.";
            if (_vanillaInteraction != null)
                _vanillaInteraction.BeginPanelSession(state, ScenarioVanillaInteractionRuntimeService.KindMap, "Choose locations or paint terrain. Your changes are saved to this scenario.");
            message = state.StatusMessage;
            return true;
        }

        private bool CloseMapAuthoring(ScenarioAuthoringState state, out string message)
        {
            if (_runtimeService != null)
            {
                _runtimeService.CleanupMarkers();
                _runtimeService.CloseVanillaMap();
            }

            if (_vanillaInteraction != null && state.VanillaInteractionActive)
                _vanillaInteraction.ReturnToEditor(state);
            else
                RestoreMapWorkspace(state);
            message = "Map authoring closed. Map workspace active.";
            state.StatusMessage = message;
            return true;
        }

        private bool SetMode(ScenarioAuthoringState state, MapAuthoringCommand command, out string message)
        {
            if (command.Mode == MapAuthoringModeKind.Place && !state.MapAuthoringActive && !OpenMapAuthoring(state, out message))
                return false;

            string mode = "select";
            if (command.Mode == MapAuthoringModeKind.Place) mode = "place";
            else if (command.Mode == MapAuthoringModeKind.Move) mode = "move";
            else if (command.Mode == MapAuthoringModeKind.PaintTerrain) mode = "terrain:" + command.TerrainId;
            state.MapAuthoringMode = mode;
            message = "Map authoring mode: " + state.MapAuthoringMode + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool SetBrushShape(ScenarioAuthoringState state, MapTerrainBrushShape shape, out string message)
        {
            state.MapTerrainBrushShape = shape == MapTerrainBrushShape.Rectangle ? "square" : "circle";
            message = "Terrain paintbrush shape: " + state.MapTerrainBrushShape + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool SetBrushSize(ScenarioAuthoringState state, int size, out string message)
        {
            state.MapTerrainBrushSize = size == 1 || size == 5 || size == 7 ? size : 3;
            message = "Terrain paintbrush size: " + state.MapTerrainBrushSize.ToString(CultureInfo.InvariantCulture)
                + " x " + state.MapTerrainBrushSize.ToString(CultureInfo.InvariantCulture) + " cells.";
            state.StatusMessage = message;
            return true;
        }

        private bool SelectWorldPosition(ScenarioAuthoringState state, float worldX, float worldY, out string message)
        {
            message = null;
            if (!state.MapAuthoringActive)
            {
                message = "Open the real map before selecting a vanilla region.";
                return false;
            }

            ScenarioMapRegionSelection selection;
            if (_runtimeService == null || !_runtimeService.TryCreateSelectionFromWorldPosition(worldX, worldY, CurrentSession, "action", out selection))
            {
                message = "No vanilla map region exists at " + worldX.ToString(CultureInfo.InvariantCulture)
                    + "," + worldY.ToString(CultureInfo.InvariantCulture) + ".";
                return false;
            }

            state.MapSelection = selection;
            ScenarioMapWorkspaceSelection.ClearLocationSelection(state, _rendererInteraction);
            message = "Selected map region " + selection.DisplayName + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool ClickWorldPosition(ScenarioAuthoringState state, float worldX, float worldY, out string message)
        {
            message = null;
            if (!state.MapAuthoringActive)
            {
                message = "Open the real map before authoring map locations.";
                return false;
            }

            int gridX;
            int gridY;
            float centreX;
            float centreY;
            if (_runtimeService == null || !_runtimeService.TryResolveGrid(worldX, worldY, out gridX, out gridY, out centreX, out centreY))
            {
                message = "Map click is outside the authored map grid.";
                return false;
            }

            string mode = string.IsNullOrEmpty(state.MapAuthoringMode) ? "select" : state.MapAuthoringMode;
            if (string.Equals(mode, "place", StringComparison.OrdinalIgnoreCase))
                return PlaceAtGrid(state, gridX, gridY, centreX, centreY, out message);
            if (string.Equals(mode, "move", StringComparison.OrdinalIgnoreCase))
                return MoveSelectedToGrid(state, gridX, gridY, centreX, centreY, out message);
            if (mode.StartsWith("terrain:", StringComparison.OrdinalIgnoreCase))
                return PaintTerrainAtGrid(state, gridX, gridY, mode.Substring("terrain:".Length), out message);
            string duplicateSourceId;
            if (ScenarioMapLocationDuplicateService.TryReadSourceId(mode, out duplicateSourceId))
                return DuplicateSelectedToGrid(state, duplicateSourceId, gridX, gridY, centreX, centreY, out message);

            MapLocationDefinition authored = _draftService != null
                ? _draftService.FindLocationAtGrid(CurrentSession, gridX, gridY)
                : null;
            if (authored != null)
                return SelectAuthoredLocation(state, authored, out message);

            ScenarioMapRegionSelection selection;
            if (_runtimeService != null && _runtimeService.TryCreateSelectionFromWorldPosition(worldX, worldY, CurrentSession, "click", out selection))
            {
                state.MapSelection = selection;
                ScenarioMapWorkspaceSelection.ClearLocationSelection(state, _rendererInteraction);
                message = "Selected vanilla map region " + selection.DisplayName + ".";
                state.StatusMessage = message;
                return true;
            }

            message = "No authored location or vanilla region exists at grid " + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture) + ".";
            return false;
        }

        private bool PlaceAtGrid(ScenarioAuthoringState state, int gridX, int gridY, float worldX, float worldY, out string message)
        {
            ScenarioEditorSession session = CurrentSession;
            if (_draftService == null || session == null)
            {
                message = "The map draft is not available.";
                return false;
            }

            MapLocationDefinition existing = _draftService.FindLocationAtGrid(session, gridX, gridY);
            if (existing != null)
                return SelectAuthoredLocation(state, existing, out message);

            string placementReason = null;
            if (_runtimeService == null || !_runtimeService.CanAuthorLocationAtGrid(gridX, gridY, out placementReason))
            {
                message = !string.IsNullOrEmpty(placementReason)
                    ? placementReason
                    : "Map locations can only be placed on generated vanilla regions.";
                return false;
            }

            RecordMapUndo(session, "Place map location at " + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture));
            MapLocationDefinition location = _draftService.CreateLocationAtGrid(session, gridX, gridY, worldX, worldY);
            if (location == null)
            {
                message = "Could not create map location at the selected grid cell.";
                return false;
            }

            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
            SelectAuthoredLocation(state, location, out message);
            state.MapAuthoringMode = "select";
            message = "Placed authored map location " + location.Id + ".";
            state.StatusMessage = message;
            if (_runtimeService != null)
                _runtimeService.RefreshMarkers(state, session);
            return true;
        }

        private bool PaintTerrainAtGrid(ScenarioAuthoringState state, int gridX, int gridY, string terrainId, out string message)
        {
            ScenarioEditorSession session = CurrentSession;
            if (_draftService == null || session == null)
            {
                message = "The map draft is not available.";
                return false;
            }

            int brushSize = state.MapTerrainBrushSize > 0 ? state.MapTerrainBrushSize : 3;
            MapTerrainBrushShape brushShape = string.Equals(state.MapTerrainBrushShape, "square", StringComparison.OrdinalIgnoreCase)
                ? MapTerrainBrushShape.Rectangle
                : MapTerrainBrushShape.Circle;
            string previewReason = null;
            if (_runtimeService == null || !_runtimeService.CanPaintTerrainAtGrid(gridX, gridY, terrainId, out previewReason))
            {
                message = !string.IsNullOrEmpty(previewReason) ? previewReason : "That terrain cannot be painted on this map cell.";
                return false;
            }

            RecordMapUndo(session, "Paint " + terrainId + " terrain area at "
                + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture));
            MapTerrainPatchDefinition patch = _draftService.PaintTerrainArea(session, gridX, gridY, terrainId, brushShape, brushSize);
            if (patch == null)
            {
                message = "The terrain patch could not be saved to the scenario draft.";
                return false;
            }

            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
            if (!_runtimeService.PreviewTerrainDraft(session, out previewReason))
            {
                message = "Saved " + FormatTerrainMode(terrainId) + " area at grid "
                    + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture)
                    + "; live preview is unavailable: " + previewReason;
            }
            else
            {
                message = "Painted " + FormatTerrainMode(terrainId) + " with a "
                    + state.MapTerrainBrushShape + " " + brushSize.ToString(CultureInfo.InvariantCulture) + "-cell brush at grid "
                    + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture) + ".";
            }

            state.StatusMessage = message;
            return true;
        }

        private bool MoveSelectedToGrid(ScenarioAuthoringState state, int gridX, int gridY, float worldX, float worldY, out string message)
        {
            ScenarioEditorSession session = CurrentSession;
            if (_draftService == null || session == null)
            {
                message = "The map draft is not available.";
                return false;
            }

            string id = state.MapSelectedLocationId;
            if (string.IsNullOrEmpty(id) && state.MapSelection != null && state.MapSelection.Authored)
                id = state.MapSelection.LocationId;
            if (string.IsNullOrEmpty(id))
            {
                message = "Select an authored location before using Move mode.";
                return false;
            }

            MapLocationDefinition occupying = _draftService.FindLocationAtGrid(session, gridX, gridY);
            if (occupying != null && !string.Equals(occupying.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                message = "Grid " + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture) + " already has authored location " + occupying.Id + ".";
                return false;
            }

            string placementReason = null;
            if (_runtimeService == null || !_runtimeService.CanAuthorLocationAtGrid(gridX, gridY, out placementReason))
            {
                message = !string.IsNullOrEmpty(placementReason)
                    ? placementReason
                    : "Map locations can only be moved onto generated vanilla regions.";
                return false;
            }

            RecordMapUndo(session, "Move map location " + id);
            MapLocationDefinition moved;
            if (!_draftService.MoveLocation(session, id, gridX, gridY, worldX, worldY, out moved))
            {
                message = "Could not move authored location " + id + ".";
                return false;
            }

            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
            SelectAuthoredLocation(state, moved, out message);
            state.MapAuthoringMode = "select";
            message = "Moved authored map location " + moved.Id + ".";
            state.StatusMessage = message;
            if (_runtimeService != null)
                _runtimeService.RefreshMarkers(state, session);
            return true;
        }

        private bool BeginDuplicateLocation(ScenarioAuthoringState state, string id, out string message)
        {
            ScenarioEditorSession session = CurrentSession;
            MapLocationDefinition source = _draftService != null ? _draftService.GetLocation(session, id) : null;
            if (source == null || state.MapSelection == null || !state.MapSelection.Authored)
            {
                message = "Select an authored location before duplicating it.";
                return false;
            }
            state.MapAuthoringMode = ScenarioMapLocationDuplicateService.BuildMode(source.Id);
            message = "Choose a new target cell for the copy of " + source.Id + ". The source cell is not allowed.";
            state.StatusMessage = message;
            return true;
        }

        private bool DuplicateSelectedToGrid(
            ScenarioAuthoringState state,
            string sourceId,
            int gridX,
            int gridY,
            float worldX,
            float worldY,
            out string message)
        {
            ScenarioEditorSession session = CurrentSession;
            if (_draftService == null || session == null || session.WorkingDefinition == null)
            {
                message = "The map draft is not available.";
                return false;
            }
            string placementReason = null;
            if (_runtimeService == null || !_runtimeService.CanAuthorLocationAtGrid(gridX, gridY, out placementReason))
            {
                message = !string.IsNullOrEmpty(placementReason) ? placementReason : "Map locations can only be copied onto generated vanilla regions.";
                return false;
            }

            RecordMapUndo(session, "Duplicate map location " + sourceId);
            MapLocationDefinition copy;
            string error;
            if (!ScenarioMapLocationDuplicateService.TryDuplicateAtGrid(
                session.WorkingDefinition.Map,
                sourceId,
                gridX,
                gridY,
                worldX,
                worldY,
                out copy,
                out error))
            {
                message = error;
                return false;
            }

            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
            SelectAuthoredLocation(state, copy, out message);
            state.MapAuthoringMode = "select";
            message = "Placed copy " + copy.Id + " at a new cell.";
            state.StatusMessage = message;
            if (_runtimeService != null)
                _runtimeService.RefreshMarkers(state, session);
            return true;
        }

        private bool SelectAuthoredLocation(ScenarioAuthoringState state, string id, out string message)
        {
            MapLocationDefinition location = _draftService != null
                ? _draftService.GetLocation(CurrentSession, id)
                : null;
            if (location == null)
            {
                message = "Authored map location '" + id + "' was not found.";
                return false;
            }

            return SelectAuthoredLocation(state, location, out message);
        }

        private bool SelectAuthoredLocation(ScenarioAuthoringState state, MapLocationDefinition location, out string message)
        {
            ScenarioMapRegionSelection selection = BuildAuthoredSelection(location);
            state.MapSelection = selection;
            ScenarioEditorSession session = CurrentSession;
            ScenarioMapWorkspaceSelection.SelectLocation(state, session != null ? session.WorkingDefinition : null, location, _rendererInteraction);
            message = "Selected authored map location " + location.Id + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool EditLocationField(
            ScenarioAuthoringState state,
            MapLocationFieldKind field,
            string id,
            string value,
            out string message)
        {
            message = null;
            ScenarioEditorSession session = CurrentSession;
            MapLocationDefinition location;
            if (!TryResolveEditableLocation(state, session, id, "Edit map location " + id + " " + MapAuthoringCommand.FieldName(field), out location, out message))
                return false;

            if (location == null)
            {
                message = "Authored map location '" + id + "' was not found.";
                return false;
            }

            string trimmed = value != null ? value.Trim() : string.Empty;
            string validationError;
            if (!ValidateLocationField(field, trimmed, out validationError))
            {
                message = validationError;
                return false;
            }

            ApplyLocationField(location, field, trimmed);
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
            SelectAuthoredLocation(state, location, out message);
            message = "Updated map location " + id + ".";
            state.StatusMessage = message;
            if (_runtimeService != null)
                _runtimeService.RefreshMarkers(state, session);
            return true;
        }

        private bool ToggleLocationField(ScenarioAuthoringState state, MapLocationFieldKind field, string id, out string message)
        {
            message = null;
            ScenarioEditorSession session = CurrentSession;
            MapLocationDefinition location;
            if (!TryResolveEditableLocation(state, session, id, "Toggle map location " + id + " " + MapAuthoringCommand.FieldName(field), out location, out message))
                return false;

            if (field != MapLocationFieldKind.Searchable
                && field != MapLocationFieldKind.VisibleAtStart
                && field != MapLocationFieldKind.DiscoveredAtStart
                && field != MapLocationFieldKind.HiddenUntilDiscovered
                && field != MapLocationFieldKind.ReplaceGeneratedLoot)
            {
                message = "Unknown map location toggle field '" + MapAuthoringCommand.FieldName(field) + "'.";
                return false;
            }

            if (field == MapLocationFieldKind.Searchable)
                location.Searchable = !location.Searchable;
            else if (field == MapLocationFieldKind.VisibleAtStart)
                location.VisibleAtStart = !location.VisibleAtStart;
            else if (field == MapLocationFieldKind.DiscoveredAtStart)
                location.DiscoveredAtStart = !location.DiscoveredAtStart;
            else if (field == MapLocationFieldKind.HiddenUntilDiscovered)
                location.HiddenUntilDiscovered = !location.HiddenUntilDiscovered;
            else if (field == MapLocationFieldKind.ReplaceGeneratedLoot)
                location.ReplaceGeneratedLoot = !location.ReplaceGeneratedLoot;

            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
            SelectAuthoredLocation(state, location, out message);
            message = "Updated map location " + id + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool CycleLocationIcon(ScenarioAuthoringState state, string id, out string message)
        {
            message = null;
            ScenarioEditorSession session = CurrentSession;
            MapLocationDefinition location;
            if (!TryResolveEditableLocation(state, session, id, "Change map location icon " + id, out location, out message))
                return false;

            string[] icons = ShelteredScenarioAuthoring.GetKnownMapIconIds();
            int nextIndex = 0;
            for (int i = 0; i < icons.Length; i++)
            {
                if (string.Equals(icons[i], location.IconId, StringComparison.OrdinalIgnoreCase))
                {
                    nextIndex = (i + 1) % icons.Length;
                    break;
                }
            }

            location.IconId = icons.Length > 0 ? icons[nextIndex] : null;
            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
            SelectAuthoredLocation(state, location, out message);
            message = "Changed map location " + id + " icon to " + location.IconId + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool TryResolveEditableLocation(
            ScenarioAuthoringState state,
            ScenarioEditorSession session,
            string id,
            string undoDescription,
            out MapLocationDefinition location,
            out string message)
        {
            location = null;
            message = null;
            if (_draftService == null || session == null)
            {
                message = "The map draft is not available.";
                return false;
            }

            location = _draftService.GetLocation(session, id);
            if (location != null)
            {
                RecordMapUndo(session, undoDescription);
                return true;
            }

            ScenarioMapRegionSelection selection = state != null ? state.MapSelection : null;
            if (selection == null || selection.Authored || !IsSelectionLocationId(selection, id))
            {
                message = "Authored map location '" + id + "' was not found.";
                return false;
            }

            RecordMapUndo(session, undoDescription);
            bool wasExisting;
            if (!_draftService.UpsertLocationFromSelection(session, selection, out location, out wasExisting) || location == null)
            {
                message = "The selected map region could not be saved into the draft.";
                return false;
            }

            selection = selection.Copy();
            selection.Captured = true;
            selection.CapturedLocationId = location.Id;
            selection.LocationId = location.Id;
            selection.Authored = true;
            selection.SelectionKind = "Authored";
            state.MapSelection = selection;
            ScenarioMapWorkspaceSelection.SelectLocation(state, session.WorkingDefinition, location, _rendererInteraction);
            return true;
        }

        private bool IsSelectionLocationId(ScenarioMapRegionSelection selection, string id)
        {
            if (selection == null || string.IsNullOrEmpty(id) || _draftService == null)
                return false;

            if (!string.IsNullOrEmpty(selection.LocationId) && string.Equals(selection.LocationId, id, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!string.IsNullOrEmpty(selection.CapturedLocationId) && string.Equals(selection.CapturedLocationId, id, StringComparison.OrdinalIgnoreCase))
                return true;

            string selectionId = _draftService.BuildLocationId(selection);
            return string.Equals(selectionId, id, StringComparison.OrdinalIgnoreCase);
        }

        private bool CaptureSelection(ScenarioAuthoringState state, out string message)
        {
            message = null;
            ScenarioMapRegionSelection selection = state.MapSelection;
            if (selection == null)
            {
                message = "Select a vanilla map region before capturing it.";
                return false;
            }

            ScenarioEditorSession session = CurrentSession;
            RecordMapUndo(session, "Capture map region " + selection.DisplayName);
            MapLocationDefinition location;
            bool wasExisting;
            if (_draftService == null || !_draftService.UpsertLocationFromSelection(session, selection, out location, out wasExisting))
            {
                message = "The selected map region could not be captured into the draft.";
                return false;
            }

            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
            selection = selection.Copy();
            selection.Captured = true;
            selection.CapturedLocationId = location.Id;
            selection.LocationId = location.Id;
            selection.Authored = true;
            selection.SelectionKind = "Authored";
            state.MapSelection = selection;
            ScenarioMapWorkspaceSelection.SelectLocation(state, session != null ? session.WorkingDefinition : null, location, _rendererInteraction);
            message = wasExisting
                ? "Updated captured map location " + location.Id + "."
                : "Captured map location " + location.Id + ".";
            state.StatusMessage = message;
            return true;
        }

        private void RestoreMapWorkspace(ScenarioAuthoringState state)
        {
            if (state == null)
                return;

            state.MapAuthoringActive = false;
            state.ShellVisible = state.MapAuthoringPreviousShellVisible || !state.ShellVisible;
            state.MapAuthoringPreviousShellVisible = false;
            state.ActiveStage = ScenarioStageKind.Map;
            state.ActiveShellTab = ScenarioAuthoringShellTab.Map;
            if (_layoutService != null)
                _layoutService.SetWindowOpen(state, ScenarioAuthoringWindowIds.Map, true);
        }

        private ScenarioEditorSession CurrentSession
        {
            get { return _editorService != null ? _editorService.CurrentSession : null; }
        }

        private static string FormatTerrainMode(string terrainId)
        {
            return string.Equals(terrainId, ShelteredScenarioAuthoring.GeneratedBlendTerrainId, StringComparison.OrdinalIgnoreCase)
                ? "generated blend terrain"
                : terrainId + " terrain";
        }

        private static bool ValidateLocationField(MapLocationFieldKind field, string value, out string error)
        {
            error = null;
            if (field == MapLocationFieldKind.IconId)
            {
                if (!ShelteredScenarioAuthoring.IsKnownMapIconId(value))
                {
                    error = "Icon id '" + value + "' is not known. Choose one of the listed map icon ids or leave it blank.";
                    return false;
                }
            }
            else if (field == MapLocationFieldKind.Danger)
            {
                int danger;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out danger) || danger < 0)
                {
                    error = "Danger must be a non-negative whole number.";
                    return false;
                }
            }
            else if (field != MapLocationFieldKind.DisplayName
                && field != MapLocationFieldKind.Kind
                && field != MapLocationFieldKind.LootTableId
                && field != MapLocationFieldKind.EncounterTableId)
            {
                error = "Unknown map location field '" + MapAuthoringCommand.FieldName(field) + "'.";
                return false;
            }

            return true;
        }

        private static void ApplyLocationField(MapLocationDefinition location, MapLocationFieldKind field, string value)
        {
            if (field == MapLocationFieldKind.DisplayName)
                location.DisplayName = value;
            else if (field == MapLocationFieldKind.Kind)
                location.Kind = value;
            else if (field == MapLocationFieldKind.IconId)
                location.IconId = value;
            else if (field == MapLocationFieldKind.LootTableId)
                location.LootTableId = value;
            else if (field == MapLocationFieldKind.EncounterTableId)
                location.EncounterTableId = value;
            else if (field == MapLocationFieldKind.Danger)
                location.Danger = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static ScenarioMapRegionSelection BuildAuthoredSelection(MapLocationDefinition location)
        {
            ScenarioMapRegionSelection selection = new ScenarioMapRegionSelection();
            selection.SelectionId = "authored:" + location.Id;
            selection.SelectionKind = "Authored";
            selection.LocationId = location.Id;
            selection.DisplayName = !string.IsNullOrEmpty(location.DisplayName) ? location.DisplayName : location.Id;
            selection.Topography = location.Kind;
            selection.Category = location.Kind;
            selection.GridX = location.GridX;
            selection.GridY = location.GridY;
            selection.WorldX = location.X;
            selection.WorldY = location.Y;
            selection.Searchable = location.Searchable;
            selection.VisibleOnMap = location.VisibleAtStart;
            selection.Discovered = location.DiscoveredAtStart;
            selection.HiddenUntilDiscovered = location.HiddenUntilDiscovered;
            selection.IconId = location.IconId;
            selection.LootTableId = location.LootTableId;
            selection.ReplaceGeneratedLoot = location.ReplaceGeneratedLoot;
            selection.EncounterTableId = location.EncounterTableId;
            selection.Captured = true;
            selection.CapturedLocationId = location.Id;
            selection.Authored = true;
            selection.Source = "draft";
            selection.OpenGroundEncounterChance = location.Danger;
            return selection;
        }

        private void RecordMapUndo(ScenarioEditorSession session, string description)
        {
            if (_historyService != null && session != null)
                _historyService.RecordAuthoringChange(session.WorkingDefinition, description, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
        }

        private static ScenarioCommandDispatchResult Result(bool changed, string message)
        {
            return new ScenarioCommandDispatchResult
            {
                Handled = true,
                Changed = changed,
                Message = message
            };
        }
    }
}
