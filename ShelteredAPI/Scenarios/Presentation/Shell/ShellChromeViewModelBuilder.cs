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
            viewModel.Title = "SHELTERED / SCENARIO EDITOR";
            viewModel.Subtitle = definition != null ? Safe(definition.DisplayName) : "No active scenario";
            viewModel.TimeLabel = null;
        }


        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<unnamed>" : value;
        }
    }
}
