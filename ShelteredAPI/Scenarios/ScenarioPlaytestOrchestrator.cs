using System;
using ModAPI.Core;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioPlaytestOrchestrator : IScenarioPlaytestOrchestrator
    {
        private readonly IScenarioApplier _applier;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;
        private readonly IScenarioPauseService _pauseService;

        public ScenarioPlaytestOrchestrator(
            IScenarioApplier applier,
            IScenarioRuntimeBindingService runtimeBindingService,
            IScenarioPauseService pauseService)
        {
            _applier = applier;
            _runtimeBindingService = runtimeBindingService;
            _pauseService = pauseService;
        }

        public ScenarioApplyResult BeginPlaytest(ScenarioEditorSession session, string scenarioFilePath)
        {
            if (session == null)
                throw new InvalidOperationException("No scenario editor session is active.");

            if (session.PlaytestState == ScenarioPlaytestState.Playtesting)
            {
                ScenarioApplyResult alreadyRunning = new ScenarioApplyResult();
                alreadyRunning.AddMessage("Playtest is already running.");
                return alreadyRunning;
            }

            string blockingReason;
            if (!ScenarioWorldReady.Evaluate(out blockingReason))
            {
                ScenarioApplyResult notReady = new ScenarioApplyResult();
                notReady.AddMessage("World is not ready for scenario apply; playtest did not start. " + blockingReason);
                return notReady;
            }

            bool reusedLiveWorld = session.HasAppliedToCurrentWorld;
            ScenarioApplyResult result;
            if (!reusedLiveWorld)
            {
                result = _applier.ApplyAll(session.WorkingDefinition, scenarioFilePath);
                session.HasAppliedToCurrentWorld = true;
            }
            else
            {
                result = new ScenarioApplyResult();
                result.AddMessage("Playtest resumed without reapplying scenario changes; the current live shelter already matches the authoring draft.");
            }

            EnsureRuntimeBinding(session);
            session.PlaytestState = ScenarioPlaytestState.Playtesting;
            _pauseService.ReleasePause("Scenario authoring released simulation.");
            MMLog.WriteInfo("[ScenarioPlaytestOrchestrator] Playtest started for scenario '" + session.WorkingDefinition.Id
                + "'. Messages=" + result.Messages.Length + ", reusedLiveWorld=" + reusedLiveWorld + ".");
            return result;
        }

        public void EndPlaytest(ScenarioEditorSession session)
        {
            _pauseService.EnsurePaused("Scenario authoring active.");
            if (session != null)
                session.PlaytestState = ScenarioPlaytestState.Paused;
            MMLog.WriteInfo("[ScenarioPlaytestOrchestrator] Playtest ended; authoring pause restored.");
        }

        private void EnsureRuntimeBinding(ScenarioEditorSession session)
        {
            if (session == null || session.WorkingDefinition == null)
                return;

            _runtimeBindingService.SetBinding(new ScenarioRuntimeBinding
            {
                ScenarioId = session.WorkingDefinition.Id,
                VersionApplied = session.WorkingDefinition.Version,
                IsActive = true,
                IsConvertedToNormalSave = false,
                DayCreated = GameTime.Day,
                LastEditorSaveTick = Environment.TickCount
            });
        }
    }
}
