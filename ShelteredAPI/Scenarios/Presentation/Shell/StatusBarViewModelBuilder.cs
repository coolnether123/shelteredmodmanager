using System.Collections.Generic;

namespace ShelteredAPI.Scenarios
{
    internal sealed class StatusBarViewModelBuilder
    {
        public string[] BuildEntries(
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession authoringSession,
            string stageLabel)
        {
            List<string> entries = new List<string>();
            entries.Add("Stage: " + (string.IsNullOrEmpty(stageLabel) ? "Shell" : stageLabel));
            entries.Add("Tool: " + (state != null ? state.ActiveTool.ToString() : "Unknown"));
            entries.Add("Grid: " + (state != null && state.Settings != null && state.Settings.GetBool("visuals.show_grid", true) ? "ON (32px)" : "OFF"));
            if (!string.IsNullOrEmpty(state != null ? state.StatusMessage : null))
                entries.Add(state.StatusMessage);
            return entries.ToArray();
        }
    }
}
