using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ShellChromeViewModelBuilder
    {
        public void ApplyShellChrome(
            ScenarioAuthoringShellViewModel viewModel,
            ScenarioAuthoringState state,
            ScenarioEditorSession editorSession,
            ScenarioAuthoringSession authoringSession)
        {
            if (viewModel == null)
                return;

            ScenarioDefinition definition = editorSession != null ? editorSession.WorkingDefinition : null;
            viewModel.Title = "Sheltered Scenario Editor";
            viewModel.Subtitle = definition != null ? Safe(definition.DisplayName) : "No active scenario";
            viewModel.DraftLabel = FormatDraftDisplay(state != null ? state.ActiveDraftId : null);
            viewModel.ModeLabel = BuildEditorModeLabel(editorSession, state);
            viewModel.TimeLabel = DateTime.Now.ToString("HH:mm");
        }

        private static string FormatDraftDisplay(string draftId)
        {
            if (string.IsNullOrEmpty(draftId))
                return "Untitled";

            const string prefix = "smm.authoring.";
            if (draftId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && draftId.Length > prefix.Length)
            {
                string tail = draftId.Substring(prefix.Length);
                int dot = tail.IndexOf('.');
                return dot > 0 ? "Draft " + tail.Substring(0, dot) : "Draft " + tail;
            }

            return draftId.Length > 32 ? draftId.Substring(0, 29) + "..." : draftId;
        }

        private static string BuildEditorModeLabel(ScenarioEditorSession editorSession, ScenarioAuthoringState state)
        {
            if (editorSession != null && editorSession.PlaytestState == ScenarioPlaytestState.Playtesting)
                return "Playtesting";

            if (ScenarioAuthoringRuntimeGuards.IsPlaytesting())
                return "Playtesting";

            if (state != null && state.MinimalMode)
                return "Minimal Editing";

            return "Editing Draft";
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<unnamed>" : value;
        }
    }
}
