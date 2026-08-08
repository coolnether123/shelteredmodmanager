using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Application.Authoring;

namespace ShelteredScenarioEditor.Application.Runtime
{
    internal interface IScenarioPlaytestOrchestrator
    {
        ScenarioEditorPlaytestResult BeginPlaytest(ScenarioEditorSession session, string scenarioFilePath);
        void EndPlaytest(ScenarioEditorSession session);
    }

    internal sealed class ScenarioEditorPlaytestResult
    {
        private readonly string[] _messages;

        private ScenarioEditorPlaytestResult(bool started, int bunkerChanges, string[] messages)
        {
            Started = started;
            BunkerChanges = bunkerChanges;
            _messages = messages ?? new string[0];
        }

        public bool Started { get; private set; }
        public int BunkerChanges { get; private set; }
        public string[] Messages { get { return (string[])_messages.Clone(); } }

        public static ScenarioEditorPlaytestResult FromPreview(ScenarioPreviewResult preview)
        {
            return preview == null
                ? Failed("Scenario preview returned no result.")
                : new ScenarioEditorPlaytestResult(preview.Started, preview.BunkerChanges, preview.Messages);
        }

        public static ScenarioEditorPlaytestResult Failed(string message)
        {
            return new ScenarioEditorPlaytestResult(false, 0, new[] { message ?? "Scenario preview did not start." });
        }
    }

    internal interface IScenarioEditorService
    {
        ScenarioEditorSession CurrentSession { get; }
        ScenarioEditorSession EnterEditMode(ScenarioBaseGameMode baseMode);
        ScenarioEditorSession LoadEditMode(string scenarioFilePath);
        ScenarioValidationResult CommitChanges(string scenarioFilePath);
        ScenarioEditorPlaytestResult BeginPlaytest();
        void EndPlaytest();
        void ConvertToNormalSave();
        void RequestRestart();
        void CloseEditor(bool resumeGame);
        void MaintainAuthoringPause();
    }

    internal interface IScenarioPauseService
    {
        bool OwnsPause { get; }
        bool EnsurePaused(string reason);
        void ReleasePause(string reason);
        bool ShouldSuppressPauseMenu();
        bool IsPauseMenuPanel(BasePanel panel);
    }

    internal interface IScenarioPlaytestUiService
    {
        void RestoreForPlaytest();
    }
}
