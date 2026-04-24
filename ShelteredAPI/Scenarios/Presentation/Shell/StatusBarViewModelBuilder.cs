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
            if (editorSession != null)
                entries.Add("Playtest: " + editorSession.PlaytestState);
            if (!string.IsNullOrEmpty(state != null ? state.StatusMessage : null))
                entries.Add(state.StatusMessage);
            return entries.ToArray();
        }
    }
}
