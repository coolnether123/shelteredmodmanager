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
            if (actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectWorldPrefix, StringComparison.Ordinal))
                return SelectWorldPosition(state, actionId, out message);

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
            state.ShellVisible = false;
            state.StatusMessage = "Map authoring active. Select a region on the real map.";
            message = state.StatusMessage;
            return true;
        }

        private bool CloseMapAuthoring(ScenarioAuthoringState state, out string message)
        {
            if (_runtimeService != null)
                _runtimeService.CloseVanillaMap();

            RestoreMapWorkspace(state);
            message = "Map authoring closed. Map workspace active.";
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
            message = "Selected map region " + selection.DisplayName + ".";
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
            state.MapSelection = selection;
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
                || actionId.StartsWith(ScenarioAuthoringActionIds.ActionMapAuthoringSelectWorldPrefix, StringComparison.Ordinal);
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
