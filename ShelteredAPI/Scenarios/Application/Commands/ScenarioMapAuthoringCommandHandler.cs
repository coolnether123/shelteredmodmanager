using System;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Map;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;

namespace ShelteredAPI.Scenarios.Application.Commands{
    internal sealed class ScenarioMapAuthoringCommandHandler : IScenarioCommandHandler
    {
        private readonly ScenarioMapAuthoringRuntimeService _runtimeService;
        private readonly ScenarioMapDraftService _draftService;
        private readonly ScenarioAuthoringLayoutService _layoutService;

        public ScenarioMapAuthoringCommandHandler(
            ScenarioMapAuthoringRuntimeService runtimeService,
            ScenarioMapDraftService draftService,
            ScenarioAuthoringLayoutService layoutService)
        {
            _runtimeService = runtimeService;
            _draftService = draftService;
            _layoutService = layoutService;
        }

        public bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message)
        {
            handled = false;
            message = null;
            if (state == null || string.IsNullOrEmpty(actionId))
                return false;

            if (!IsMapAction(actionId))
                return false;

            handled = true;
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringOpen, StringComparison.Ordinal))
                return OpenMapAuthoring(state, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringClose, StringComparison.Ordinal))
                return CloseMapAuthoring(state, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringCaptureSelection, StringComparison.Ordinal))
                return CaptureSelection(state, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeSelect, StringComparison.Ordinal))
                return SetMode(state, "select", out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModePlace, StringComparison.Ordinal))
                return SetMode(state, "place", out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeMove, StringComparison.Ordinal))
                return SetMode(state, "move", out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainTrees, StringComparison.Ordinal))
                return SetMode(state, "terrain:Woodland", out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainMountains, StringComparison.Ordinal))
                return SetMode(state, "terrain:Mountains", out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainClear, StringComparison.Ordinal))
                return SetMode(state, "terrain:NowhereSpecial", out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainGeneratedBlend, StringComparison.Ordinal))
                return SetMode(state, "terrain:" + ScenarioMapTerrainModes.GeneratedBlend, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushShapeCircle, StringComparison.Ordinal))
                return SetBrushShape(state, "circle", out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushShapeSquare, StringComparison.Ordinal))
                return SetBrushShape(state, "square", out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize1, StringComparison.Ordinal))
                return SetBrushSize(state, 1, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize3, StringComparison.Ordinal))
                return SetBrushSize(state, 3, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize5, StringComparison.Ordinal))
                return SetBrushSize(state, 5, out message);
            if (string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize7, StringComparison.Ordinal))
                return SetBrushSize(state, 7, out message);
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectWorldPrefix, StringComparison.Ordinal))
                return SelectWorldPosition(state, actionId, out message);
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringClickWorldPrefix, StringComparison.Ordinal))
                return ClickWorldPosition(state, actionId, out message);
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectLocationPrefix, StringComparison.Ordinal))
                return SelectAuthoredLocation(state, actionId, out message);
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapLocationDuplicatePrefix, StringComparison.Ordinal))
                return BeginDuplicateLocation(state, actionId, out message);
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapLocationEditPrefix, StringComparison.Ordinal))
                return EditLocationField(state, actionId, out message);
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapLocationTogglePrefix, StringComparison.Ordinal))
                return ToggleLocationField(state, actionId, out message);
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapLocationCycleIconPrefix, StringComparison.Ordinal))
                return CycleLocationIcon(state, actionId, out message);

            handled = false;
            return false;
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
            if (!ScenarioWorldReady.Evaluate(out blockingReason))
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
            state.StatusMessage = "Map authoring active. Select or place authored locations on the real map.";
            ScenarioVanillaInteractionRuntimeService vanillaInteraction = ScenarioCompositionRoot.Resolve<ScenarioVanillaInteractionRuntimeService>();
            if (vanillaInteraction != null)
                vanillaInteraction.BeginPanelSession(state, ScenarioVanillaInteractionRuntimeService.KindMap, "Changes sync to your scenario. Map picks update authored location data.");
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

            ScenarioVanillaInteractionRuntimeService vanillaInteraction = ScenarioCompositionRoot.Resolve<ScenarioVanillaInteractionRuntimeService>();
            if (vanillaInteraction != null && state.VanillaInteractionActive)
                vanillaInteraction.ReturnToEditor(state);
            else
                RestoreMapWorkspace(state);
            message = "Map authoring closed. Map workspace active.";
            state.StatusMessage = message;
            return true;
        }

        private bool SetMode(ScenarioAuthoringState state, string mode, out string message)
        {
            state.MapAuthoringMode = string.IsNullOrEmpty(mode) ? "select" : mode;
            message = "Map authoring mode: " + state.MapAuthoringMode + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool SetBrushShape(ScenarioAuthoringState state, string shape, out string message)
        {
            state.MapTerrainBrushShape = string.Equals(shape, "square", StringComparison.OrdinalIgnoreCase) ? "square" : "circle";
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

        private bool SelectWorldPosition(ScenarioAuthoringState state, string actionId, out string message)
        {
            message = null;
            if (!state.MapAuthoringActive)
            {
                message = "Open the real map before selecting a vanilla region.";
                return false;
            }

            string token;
            if (!ScenarioAuthoringActionCodec.TryDecodeTokenActionId(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringSelectWorldPrefix, out token))
            {
                message = "Map selection coordinates could not be decoded.";
                return false;
            }

            float worldX;
            float worldY;
            if (!TryParseWorldPosition(token, out worldX, out worldY))
            {
                message = "Map selection coordinates are invalid.";
                return false;
            }

            ScenarioMapRegionSelection selection;
            if (_runtimeService == null || !_runtimeService.TryCreateSelectionFromWorldPosition(worldX, worldY, ScenarioEditorController.Instance.CurrentSession, "action", out selection))
            {
                message = "No vanilla map region exists at " + token + ".";
                return false;
            }

            state.MapSelection = selection;
            state.MapSelectedLocationId = null;
            message = "Selected map region " + selection.DisplayName + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool ClickWorldPosition(ScenarioAuthoringState state, string actionId, out string message)
        {
            message = null;
            if (!state.MapAuthoringActive)
            {
                message = "Open the real map before authoring map locations.";
                return false;
            }

            float worldX;
            float worldY;
            if (!TryParseWorldAction(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringClickWorldPrefix, out worldX, out worldY))
            {
                message = "Map click coordinates are invalid.";
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
                ? _draftService.FindLocationAtGrid(ScenarioEditorController.Instance.CurrentSession, gridX, gridY)
                : null;
            if (authored != null)
                return SelectAuthoredLocation(state, authored, out message);

            ScenarioMapRegionSelection selection;
            if (_runtimeService != null && _runtimeService.TryCreateSelectionFromWorldPosition(worldX, worldY, ScenarioEditorController.Instance.CurrentSession, "click", out selection))
            {
                state.MapSelection = selection;
                state.MapSelectedLocationId = null;
                message = "Selected vanilla map region " + selection.DisplayName + ".";
                state.StatusMessage = message;
                return true;
            }

            message = "No authored location or vanilla region exists at grid " + gridX.ToString(CultureInfo.InvariantCulture) + "," + gridY.ToString(CultureInfo.InvariantCulture) + ".";
            return false;
        }

        private bool PlaceAtGrid(ScenarioAuthoringState state, int gridX, int gridY, float worldX, float worldY, out string message)
        {
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
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
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
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
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
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

        private bool BeginDuplicateLocation(ScenarioAuthoringState state, string actionId, out string message)
        {
            string id;
            if (!ScenarioAuthoringActionCodec.TryDecodeTokenActionId(actionId, ScenarioAuthoringActionIds.ActionMapLocationDuplicatePrefix, out id))
            {
                message = "The location to duplicate could not be decoded.";
                return false;
            }
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
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
            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
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

        private bool SelectAuthoredLocation(ScenarioAuthoringState state, string actionId, out string message)
        {
            string id;
            if (!ScenarioAuthoringActionCodec.TryDecodeTokenActionId(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringSelectLocationPrefix, out id))
            {
                message = "Map location id could not be decoded.";
                return false;
            }

            MapLocationDefinition location = _draftService != null
                ? _draftService.GetLocation(ScenarioEditorController.Instance.CurrentSession, id)
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
            state.MapSelectedLocationId = location.Id;
            message = "Selected authored map location " + location.Id + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool EditLocationField(ScenarioAuthoringState state, string actionId, out string message)
        {
            message = null;
            string field;
            string id;
            string value;
            if (!TryParseLocationFieldAction(actionId, ScenarioAuthoringActionIds.ActionMapLocationEditPrefix, out field, out id, out value))
            {
                message = "Map location edit action is invalid.";
                return false;
            }

            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            MapLocationDefinition location;
            if (!TryResolveEditableLocation(state, session, id, "Edit map location " + id + " " + field, out location, out message))
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

        private bool ToggleLocationField(ScenarioAuthoringState state, string actionId, out string message)
        {
            message = null;
            string field;
            string id;
            if (!TryParseLocationToggleAction(actionId, ScenarioAuthoringActionIds.ActionMapLocationTogglePrefix, out field, out id))
            {
                message = "Map location toggle action is invalid.";
                return false;
            }

            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            MapLocationDefinition location;
            if (!TryResolveEditableLocation(state, session, id, "Toggle map location " + id + " " + field, out location, out message))
                return false;

            if (!string.Equals(field, "searchable", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(field, "visibleAtStart", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(field, "discoveredAtStart", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(field, "hiddenUntilDiscovered", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(field, "replaceGeneratedLoot", StringComparison.OrdinalIgnoreCase))
            {
                message = "Unknown map location toggle field '" + field + "'.";
                return false;
            }

            if (string.Equals(field, "searchable", StringComparison.OrdinalIgnoreCase))
                location.Searchable = !location.Searchable;
            else if (string.Equals(field, "visibleAtStart", StringComparison.OrdinalIgnoreCase))
                location.VisibleAtStart = !location.VisibleAtStart;
            else if (string.Equals(field, "discoveredAtStart", StringComparison.OrdinalIgnoreCase))
                location.DiscoveredAtStart = !location.DiscoveredAtStart;
            else if (string.Equals(field, "hiddenUntilDiscovered", StringComparison.OrdinalIgnoreCase))
                location.HiddenUntilDiscovered = !location.HiddenUntilDiscovered;
            else if (string.Equals(field, "replaceGeneratedLoot", StringComparison.OrdinalIgnoreCase))
                location.ReplaceGeneratedLoot = !location.ReplaceGeneratedLoot;

            ScenarioAuthoringMutation.MarkDirty(session, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
            SelectAuthoredLocation(state, location, out message);
            message = "Updated map location " + id + ".";
            state.StatusMessage = message;
            return true;
        }

        private bool CycleLocationIcon(ScenarioAuthoringState state, string actionId, out string message)
        {
            message = null;
            string id;
            if (!ScenarioAuthoringActionCodec.TryDecodeTokenActionId(actionId, ScenarioAuthoringActionIds.ActionMapLocationCycleIconPrefix, out id))
            {
                message = "Map location id could not be decoded.";
                return false;
            }

            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
            MapLocationDefinition location;
            if (!TryResolveEditableLocation(state, session, id, "Change map location icon " + id, out location, out message))
                return false;

            string[] icons = ScenarioMapIconCatalog.GetKnownIconIds();
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
            state.MapSelectedLocationId = location.Id;
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

            ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
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
            state.MapSelectedLocationId = location.Id;
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

        private static bool IsMapAction(string actionId)
        {
            return string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringOpen, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringClose, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringCaptureSelection, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeSelect, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModePlace, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeMove, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainTrees, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainMountains, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainClear, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringModeTerrainGeneratedBlend, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushShapeCircle, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushShapeSquare, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize1, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize3, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize5, StringComparison.Ordinal)
                || string.Equals(actionId, ScenarioAuthoringActionIds.ActionMapAuthoringBrushSize7, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectWorldPrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringClickWorldPrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectLocationPrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapLocationEditPrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapLocationTogglePrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapLocationCycleIconPrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapLocationDuplicatePrefix, StringComparison.Ordinal);
        }

        private static string FormatTerrainMode(string terrainId)
        {
            return string.Equals(terrainId, ScenarioMapTerrainModes.GeneratedBlend, StringComparison.OrdinalIgnoreCase)
                ? "generated blend terrain"
                : terrainId + " terrain";
        }

        private static bool ValidateLocationField(string field, string value, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(field))
            {
                error = "Map location field is missing.";
                return false;
            }

            if (string.Equals(field, "iconId", StringComparison.OrdinalIgnoreCase))
            {
                if (!ScenarioMapIconCatalog.IsKnownIconId(value))
                {
                    error = "Icon id '" + value + "' is not known. Choose one of the listed map icon ids or leave it blank.";
                    return false;
                }
            }
            else if (string.Equals(field, "danger", StringComparison.OrdinalIgnoreCase))
            {
                int danger;
                if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out danger) || danger < 0)
                {
                    error = "Danger must be a non-negative whole number.";
                    return false;
                }
            }
            else if (!string.Equals(field, "displayName", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(field, "kind", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(field, "lootTableId", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(field, "encounterTableId", StringComparison.OrdinalIgnoreCase))
            {
                error = "Unknown map location field '" + field + "'.";
                return false;
            }

            return true;
        }

        private static void ApplyLocationField(MapLocationDefinition location, string field, string value)
        {
            if (string.Equals(field, "displayName", StringComparison.OrdinalIgnoreCase))
                location.DisplayName = value;
            else if (string.Equals(field, "kind", StringComparison.OrdinalIgnoreCase))
                location.Kind = value;
            else if (string.Equals(field, "iconId", StringComparison.OrdinalIgnoreCase))
                location.IconId = value;
            else if (string.Equals(field, "lootTableId", StringComparison.OrdinalIgnoreCase))
                location.LootTableId = value;
            else if (string.Equals(field, "encounterTableId", StringComparison.OrdinalIgnoreCase))
                location.EncounterTableId = value;
            else if (string.Equals(field, "danger", StringComparison.OrdinalIgnoreCase))
                location.Danger = int.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture);
        }

        private static bool TryParseLocationFieldAction(string actionId, string prefix, out string field, out string id, out string value)
        {
            field = null;
            id = null;
            value = null;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string body = actionId.Substring(prefix.Length);
            int firstDot = body.IndexOf('.');
            if (firstDot <= 0)
                return false;

            int secondDot = body.IndexOf('.', firstDot + 1);
            if (secondDot <= firstDot)
                return false;

            field = body.Substring(0, firstDot);
            id = ScenarioAuthoringActionCodec.DecodeToken(body.Substring(firstDot + 1, secondDot - firstDot - 1));
            value = ScenarioAuthoringActionCodec.DecodeToken(body.Substring(secondDot + 1));
            return !string.IsNullOrEmpty(field) && !string.IsNullOrEmpty(id) && value != null;
        }

        private static bool TryParseLocationToggleAction(string actionId, string prefix, out string field, out string id)
        {
            field = null;
            id = null;
            if (string.IsNullOrEmpty(actionId) || !actionId.StartsWith(prefix, StringComparison.Ordinal))
                return false;

            string body = actionId.Substring(prefix.Length);
            int dot = body.IndexOf('.');
            if (dot <= 0)
                return false;

            field = body.Substring(0, dot);
            id = ScenarioAuthoringActionCodec.DecodeToken(body.Substring(dot + 1));
            return !string.IsNullOrEmpty(field) && !string.IsNullOrEmpty(id);
        }

        private static bool TryParseWorldAction(string actionId, string prefix, out float worldX, out float worldY)
        {
            worldX = 0f;
            worldY = 0f;
            string token;
            return ScenarioAuthoringActionCodec.TryDecodeTokenActionId(actionId, prefix, out token)
                && TryParseWorldPosition(token, out worldX, out worldY);
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

        private static void RecordMapUndo(ScenarioEditorSession session, string description)
        {
            ScenarioAuthoringHistoryService history = ScenarioAuthoringHistoryService.Instance;
            if (history != null && session != null)
                history.RecordAuthoringChange(session.WorkingDefinition, description, ScenarioDirtySection.Map, ScenarioEditCategory.Map);
        }

        private static bool TryParseWorldPosition(string token, out float worldX, out float worldY)
        {
            worldX = 0f;
            worldY = 0f;
            if (string.IsNullOrEmpty(token))
                return false;

            string[] parts = token.Split(',');
            return parts.Length == 2
                && float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out worldX)
                && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out worldY);
        }
    }
}
