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
            entries.Add("Workspace: " + (string.IsNullOrEmpty(stageLabel) ? "Workshop" : stageLabel));
            entries.Add("Layer: " + ScenarioTargetClassifier.FormatScopeLabel(_selectionScopeService.ResolveSelectionScope(state)));
            entries.Add("Tool: " + (state != null ? ScenarioAuthoringWorkflowLabels.GetToolLabel(state.ActiveTool) : "Unknown"));
            entries.Add("Grid: " + (state != null && state.Settings != null && state.Settings.GetBool("visuals.show_grid", true) ? "ON (32px)" : "OFF"));
            if (!string.IsNullOrEmpty(state != null ? state.StatusMessage : null))
                entries.Add(state.StatusMessage);
            return entries.ToArray();
        }
    }
}
