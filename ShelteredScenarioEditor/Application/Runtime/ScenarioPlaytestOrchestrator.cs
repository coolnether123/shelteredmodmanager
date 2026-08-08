using System;
using ModAPI.Core;
using ShelteredAPI.Scenarios.Public;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Infrastructure.Unity;

namespace ShelteredScenarioEditor.Application.Runtime
{
    internal sealed class ScenarioPlaytestOrchestrator : IScenarioPlaytestOrchestrator
    {
        private readonly IScenarioPauseService _pauseService;
        private readonly ScenarioAuthorTestChecklistService _testChecklistService;
        private readonly IScenarioPlaytestUiService _vanillaUiService;
        private readonly ScenarioPreviewSessionHost _previewSession;

        public ScenarioPlaytestOrchestrator(
            IScenarioPauseService pauseService,
            ScenarioAuthorTestChecklistService testChecklistService,
            IScenarioPlaytestUiService vanillaUiService,
            ScenarioPreviewSessionHost previewSession)
        {
            _pauseService = pauseService;
            _testChecklistService = testChecklistService;
            _vanillaUiService = vanillaUiService;
            _previewSession = previewSession;
        }

        public ScenarioEditorPlaytestResult BeginPlaytest(ScenarioEditorSession session, string scenarioFilePath)
        {
            if (session == null)
                throw new InvalidOperationException("No scenario editor session is active.");
            if (session.WorkingDefinition == null)
                return ScenarioEditorPlaytestResult.Failed("Playtest could not start because the active draft has no working definition.");
            if (session.PlaytestState == ScenarioPlaytestState.Playtesting)
                return ScenarioEditorPlaytestResult.Failed("Playtest is already running.");

            string cutsceneBlockingReason;
            if (!ScenarioPlaytestRestartCutsceneGuard.TryClearBlockingIntroCutscene(
                session.WorkingDefinition.Id,
                out cutsceneBlockingReason))
            {
                return ScenarioEditorPlaytestResult.Failed(
                    "World is not ready for scenario preview. " + cutsceneBlockingReason);
            }

            session.ResetStoppedPlaytestWorld();
            ScenarioEditorPlaytestResult result = ScenarioEditorPlaytestResult.FromPreview(
                _previewSession.StartOrRefresh(session.WorkingDefinition, scenarioFilePath));
            if (!result.Started)
            {
                MMLog.WriteWarning("[ScenarioPlaytestOrchestrator] Preview did not start for scenario '"
                    + session.WorkingDefinition.Id + "'.");
                return result;
            }

            session.MarkAppliedToCurrentWorld();
            session.PlaytestState = ScenarioPlaytestState.Playtesting;
            if (_testChecklistService != null)
                _testChecklistService.MarkPlaytestStarted(session);
            _pauseService.ReleasePause("Scenario authoring released simulation.");
            if (_vanillaUiService != null)
                _vanillaUiService.RestoreForPlaytest();

            MMLog.WriteInfo("[ScenarioPlaytestOrchestrator] Playtest started for scenario '"
                + session.WorkingDefinition.Id + "'. Messages=" + result.Messages.Length
                + ", appliedDraftRevision=" + session.AppliedDraftRevision + ".");
            return result;
        }

        public void EndPlaytest(ScenarioEditorSession session)
        {
            _pauseService.EnsurePaused("Scenario authoring active.");
            _previewSession.Close();
            if (session != null)
            {
                session.PlaytestState = ScenarioPlaytestState.Paused;
                session.ResetStoppedPlaytestWorld();
            }

            MMLog.WriteInfo("[ScenarioPlaytestOrchestrator] Playtest ended; authoring pause restored.");
        }
    }
}
