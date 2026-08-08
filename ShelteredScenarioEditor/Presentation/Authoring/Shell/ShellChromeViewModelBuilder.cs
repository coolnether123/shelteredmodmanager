using ModAPI.Scenarios;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredScenarioEditor.Presentation.Authoring.Shell{
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
            viewModel.Title = "SHELTERED / SCENARIO WORKSHOP";
            viewModel.Subtitle = definition != null ? Safe(definition.DisplayName) : "No active scenario";
            viewModel.TimeLabel = null;
        }

        private static string Safe(string value)
        {
            return string.IsNullOrEmpty(value) ? "<unnamed>" : value;
        }
    }
}
