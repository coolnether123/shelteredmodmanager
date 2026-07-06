using System.Collections.Generic;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Selection;
namespace ShelteredAPI.Scenarios.Presentation.Authoring.Shell{
    internal sealed class StatusBarViewModelBuilder
    {
        private readonly ScenarioSelectionScopeService _selectionScopeService;

        public StatusBarViewModelBuilder(ScenarioSelectionScopeService selectionScopeService)
        {
            _selectionScopeService = selectionScopeService;
        }

        public string[] BuildEntries(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession authoringSession,
            string stageLabel)
        {
            List<string> entries = new List<string>();
            ScenarioBuildPlacementAuthoringService.StatusModel placementStatus =
                ScenarioBuildPlacementAuthoringService.Instance.GetStatusModel(state, editorSession);
            entries.Add("Workspace: " + (string.IsNullOrEmpty(stageLabel) ? "Workshop" : stageLabel));
            entries.Add(BuildModeEntry(placementStatus));
            ScenarioTargetScope scope = _selectionScopeService.ResolveSelectionScope(state);
            if (scope != ScenarioTargetScope.Unknown)
                entries.Add("Layer: " + ScenarioTargetClassifier.FormatScopeLabel(scope));
            if (state != null && state.HoveredTarget != null)
                entries.Add("Hover: " + state.HoveredTarget.DisplayName);
            entries.Add("Grid: " + (state != null && state.Settings != null && state.Settings.GetBool("visuals.show_grid", true) ? "On (32px)" : "Off"));
            if (placementStatus != null && placementStatus.PlacementActive)
                entries.Add("Left-click place - Right-click/Esc cancel");
            if (!string.IsNullOrEmpty(state != null ? state.StatusMessage : null))
                entries.Add(FormatStatusMessage(state.StatusMessage));
            return entries.ToArray();
        }

        private static string BuildModeEntry(ScenarioBuildPlacementAuthoringService.StatusModel placementStatus)
        {
            if (placementStatus != null && placementStatus.PlacementActive)
                return placementStatus.Title;

            return "Mode: Select";
        }

        private static string FormatStatusMessage(string statusMessage)
        {
            if (statusMessage == "Scenario authoring shell is active. Use playtest to make live shelter changes, then capture them back into the draft.")
                return "Workshop ready. Playtest live changes, then capture updates into the draft.";

            return statusMessage;
        }
    }
}
