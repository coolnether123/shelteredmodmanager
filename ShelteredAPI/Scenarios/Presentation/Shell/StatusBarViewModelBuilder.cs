using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
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
            entries.Add("Stage: " + (string.IsNullOrEmpty(stageLabel) ? "Shell" : stageLabel));
            entries.Add("Scope: " + ScenarioTargetClassifier.FormatScopeLabel(_selectionScopeService.ResolveActiveScope(state)));
            entries.Add("Tool: " + (state != null ? state.ActiveTool.ToString() : "Unknown"));
            entries.Add("Grid: " + (state != null && state.Settings != null && state.Settings.GetBool("visuals.show_grid", true) ? "ON (32px)" : "OFF"));
            if (!string.IsNullOrEmpty(state != null ? state.StatusMessage : null))
                entries.Add(state.StatusMessage);
            return entries.ToArray();
        }
    }
}
