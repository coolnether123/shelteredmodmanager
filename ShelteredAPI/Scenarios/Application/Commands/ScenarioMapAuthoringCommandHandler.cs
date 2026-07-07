using System;
using System.Globalization;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Map;
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
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectWorldPrefix, StringComparison.Ordinal))
                return SelectWorldPosition(state, actionId, out message);
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringClickWorldPrefix, StringComparison.Ordinal))
                return ClickWorldPosition(state, actionId, out message);
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectLocationPrefix, StringComparison.Ordinal))
                return SelectAuthoredLocation(state, actionId, out message);

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
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectWorldPrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringClickWorldPrefix, StringComparison.Ordinal)
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectLocationPrefix, StringComparison.Ordinal);
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
