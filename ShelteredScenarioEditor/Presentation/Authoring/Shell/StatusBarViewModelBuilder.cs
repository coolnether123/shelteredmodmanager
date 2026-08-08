using System.Collections.Generic;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Selection;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredAPI.Infrastructure;
using ShelteredScenarioEditor.Infrastructure.Resilience;
namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
    internal sealed class StatusBarViewModelBuilder
    {
        private readonly ScenarioSelectionScopeService _selectionScopeService;
        private readonly ScenarioBuildPlacementAuthoringService _buildPlacement;

        public StatusBarViewModelBuilder(
            ScenarioSelectionScopeService selectionScopeService,
            ScenarioBuildPlacementAuthoringService buildPlacement)
        {
            _selectionScopeService = selectionScopeService;
            _buildPlacement = buildPlacement;
        }

        public string[] BuildEntries(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession authoringSession,
            string stageLabel)
        {
            List<string> entries = new List<string>();
            ScenarioBuildPlacementAuthoringService.StatusModel placementStatus =
                _buildPlacement.GetStatusModel(state, editorSession);
            bool worldMode = IsWorldMode(state);
            entries.Add("Workspace: " + (string.IsNullOrEmpty(stageLabel) ? "Workshop" : stageLabel));
            entries.Add(BuildModeEntry(placementStatus));
            if (worldMode)
            {
                ScenarioTargetScope scope = _selectionScopeService.ResolveSelectionScope(state);
                if (scope != ScenarioTargetScope.Unknown)
                    entries.Add("Layer: " + ScenarioTargetClassifier.FormatScopeLabel(scope));
            }
            if (worldMode && state != null && state.HoveredTarget != null)
                entries.Add("Hover: " + state.HoveredTarget.DisplayName);
            if (worldMode)
                entries.Add("Grid: " + (state != null && state.Settings != null && state.Settings.GetBool("visuals.show_grid", true) ? "On (32px)" : "Off"));
            if (placementStatus != null && placementStatus.PlacementActive)
                entries.Add("Left-click place - Right-click/Esc cancel");
            string seamHealth = ScenarioEditorSeamGuard.BuildSystemHealthLine();
            if (!string.IsNullOrEmpty(seamHealth))
                entries.Add(seamHealth);
            if (ShouldShowStatusMessage(state != null ? state.StatusMessage : null))
                entries.Add(FormatStatusMessage(state.StatusMessage));
            return entries.ToArray();
        }

        private static bool IsWorldMode(ScenarioAuthoringState state)
        {
            if (state == null)
                return false;

            return state.ActiveStage == ScenarioStageKind.Bunker
                || state.ActiveStage == ScenarioStageKind.BunkerBackground
                || state.ActiveStage == ScenarioStageKind.BunkerSurface
                || state.ActiveStage == ScenarioStageKind.BunkerInside;
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
            if (statusMessage == "Close blocked: the draft could not be serialized. Check the log, then try Exit Editor again.")
                return "Exit is blocked. The draft could not be serialized; check the log, then retry.";
            if (statusMessage.IndexOf("Close blocked", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return "An action is blocked. Check the reason and apply the fix.";
            if (!string.IsNullOrEmpty(statusMessage)
                && statusMessage.IndexOf("Unknown", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return statusMessage.Replace("Unknown", "current workspace");

            return statusMessage;
        }

        internal static bool ShouldShowStatusMessage(string statusMessage)
        {
            if (string.IsNullOrEmpty(statusMessage))
                return false;

            return !statusMessage.StartsWith("Disclosure toggled:", System.StringComparison.OrdinalIgnoreCase)
                && !statusMessage.StartsWith("Candidate search updated:", System.StringComparison.OrdinalIgnoreCase)
                && !statusMessage.StartsWith("Workspace subtab selected:", System.StringComparison.OrdinalIgnoreCase)
                && !statusMessage.StartsWith("Workspace selection updated:", System.StringComparison.OrdinalIgnoreCase)
                && !statusMessage.StartsWith("Workspace expansion toggled:", System.StringComparison.OrdinalIgnoreCase)
                && !statusMessage.StartsWith("Workspace search updated:", System.StringComparison.OrdinalIgnoreCase)
                && !statusMessage.StartsWith("Workspace pane changed:", System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
