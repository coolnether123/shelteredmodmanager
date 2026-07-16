using System;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Application.Runtime{
    internal sealed class ScenarioPlaytestOrchestrator : IScenarioPlaytestOrchestrator
    {
        private readonly IScenarioApplier _applier;
        private readonly IScenarioRuntimeBindingService _runtimeBindingService;
        private readonly IScenarioPauseService _pauseService;
        private readonly ScenarioAuthorTestChecklistService _testChecklistService;
        private readonly IVanillaScenarioRuntime _vanillaRuntime;
        private readonly IScenarioPlaytestUiService _vanillaUiService;

        public ScenarioPlaytestOrchestrator(
            IScenarioApplier applier,
            IScenarioRuntimeBindingService runtimeBindingService,
            IScenarioPauseService pauseService,
            ScenarioAuthorTestChecklistService testChecklistService,
            IVanillaScenarioRuntime vanillaRuntime,
            IScenarioPlaytestUiService vanillaUiService)
        {
            _applier = applier;
            _runtimeBindingService = runtimeBindingService;
            _pauseService = pauseService;
            _testChecklistService = testChecklistService;
            _vanillaRuntime = vanillaRuntime;
            _vanillaUiService = vanillaUiService;
        }

        public ScenarioApplyResult BeginPlaytest(ScenarioEditorSession session, string scenarioFilePath)
        {
            if (session == null)
                throw new InvalidOperationException("No scenario editor session is active.");

            if (session.WorkingDefinition == null)
            {
                ScenarioApplyResult missingDefinition = new ScenarioApplyResult();
                missingDefinition.AddMessage("Playtest could not start because the active draft has no working definition.");
                MMLog.WriteWarning("[ScenarioPlaytestOrchestrator] Playtest blocked: active draft has no working definition.");
                return missingDefinition;
            }

            if (session.PlaytestState == ScenarioPlaytestState.Playtesting)
            {
                ScenarioApplyResult alreadyRunning = new ScenarioApplyResult();
                alreadyRunning.AddMessage("Playtest is already running.");
                return alreadyRunning;
            }

            string blockingReason;
            if (!ScenarioWorldReady.Evaluate(out blockingReason))
            {
                if (string.Equals(blockingReason, "A cutscene is still active.", StringComparison.Ordinal))
                {
                    string cutsceneBlockingReason;
                    if (!ScenarioPlaytestRestartCutsceneGuard.TryClearBlockingIntroCutscene(
                        session.WorkingDefinition.Id, out cutsceneBlockingReason))
                    {
                        MMLog.WriteWarning("[ScenarioPlaytestOrchestrator] Playtest blocked: " + cutsceneBlockingReason);
                        ScenarioApplyResult cutsceneBlocked = new ScenarioApplyResult();
                        cutsceneBlocked.AddMessage("World is not ready for scenario apply; playtest did not start. " + cutsceneBlockingReason);
                        return cutsceneBlocked;
                    }

                    if (!ScenarioWorldReady.Evaluate(out blockingReason))
                    {
                        ScenarioApplyResult notReadyAfterCutsceneClear = new ScenarioApplyResult();
                        notReadyAfterCutsceneClear.AddMessage("World is not ready for scenario apply; playtest did not start. " + blockingReason);
                        MMLog.WriteWarning("[ScenarioPlaytestOrchestrator] Playtest blocked: " + blockingReason);
                        return notReadyAfterCutsceneClear;
                    }
                }
                else
                {
                    ScenarioApplyResult notReady = new ScenarioApplyResult();
                    notReady.AddMessage("World is not ready for scenario apply; playtest did not start. " + blockingReason);
                    MMLog.WriteWarning("[ScenarioPlaytestOrchestrator] Playtest blocked: " + blockingReason);
                    return notReady;
                }
            }
            // A Begin request which reaches this point is not an active playtest
            // (the active case returned above).  It is therefore the normal
            // fresh-start path and must apply the current draft, irrespective of
            // any latch/revision residue from an earlier stopped world.
            //
            // In particular, do not interpret a revision mismatch as a reason to
            // refuse a paused start: that mismatch is exactly why this new world
            // needs an apply.  EndPlaytest clears this state in the usual path;
            // the reset here also recovers safely from a stale session snapshot.
            session.ResetStoppedPlaytestWorld();
            InvalidateCompletionCarrier();
            ScenarioApplyResult result;

            // The scheduled runtime resolves authored end conditions through the
            // vanilla scenario QuestInstance.  A playtest is not entered through
            // the normal pending-scenario launch path, so create and bind that
            // carrier here before applying the scheduled runtime.
            string spawnReason;
            if (!EnsureScenarioQuestInstance(session, out spawnReason))
            {
                ScenarioApplyResult spawnBlocked = new ScenarioApplyResult();
                spawnBlocked.AddMessage("Playtest could not start because its scenario completion carrier could not be created. " + spawnReason);
                MMLog.WriteWarning("[ScenarioPlaytestOrchestrator] Playtest blocked: " + spawnReason);
                return spawnBlocked;
            }

            try
            {
                string seedMessage;
                ScenarioSeedPolicy.TryApplyForScenario(session.WorkingDefinition, "playtest", out seedMessage);
                result = _applier.ApplyAll(session.WorkingDefinition, scenarioFilePath);
                if (!string.IsNullOrEmpty(seedMessage))
                    result.AddMessage(seedMessage);
            }
            catch (Exception ex)
            {
                result = new ScenarioApplyResult();
                result.AddMessage("Playtest apply failed: " + ex.Message);
                MMLog.WriteWarning("[ScenarioPlaytestOrchestrator] Playtest apply failed: " + ex);
                return result;
            }

            if (result == null)
            {
                result = new ScenarioApplyResult();
                result.AddMessage("Playtest apply returned no result.");
                MMLog.WriteWarning("[ScenarioPlaytestOrchestrator] Playtest apply returned null for scenario '"
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
            MMLog.WriteInfo("[ScenarioPlaytestOrchestrator] Playtest started for scenario '" + session.WorkingDefinition.Id
                + "'. Messages=" + result.Messages.Length + ", appliedDraftRevision=" + session.AppliedDraftRevision + ".");
            return result;
        }

        public void EndPlaytest(ScenarioEditorSession session)
        {
            ModRandomBridge.SetScenarioFixedSeedActive(false);
            _pauseService.EnsurePaused("Scenario authoring active.");
            if (session != null)
            {
                session.PlaytestState = ScenarioPlaytestState.Paused;
                session.ResetStoppedPlaytestWorld();
            }

            // A quest carrier belongs to the just-stopped live world.  Leaving
            // its id on the active binding makes a subsequent apply reuse it.
            // Keep the binding active: it identifies the still-open authoring
            // session, while only the carrier is world-scoped.
            InvalidateCompletionCarrier();
            MMLog.WriteInfo("[ScenarioPlaytestOrchestrator] Playtest ended; authoring pause restored.");
        }

        private void InvalidateCompletionCarrier()
        {
            ScenarioRuntimeBinding binding = _runtimeBindingService.CurrentBinding;
            if (binding == null)
                return;

            binding.ScenarioQuestInstanceId = null;
            _runtimeBindingService.SetBinding(binding);
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
                RunId = Guid.NewGuid().ToString("N"),
                LastEditorSaveTick = Environment.TickCount
            });
        }

        private bool EnsureScenarioQuestInstance(ScenarioEditorSession session, out string reason)
        {
            reason = null;
            if (_vanillaRuntime == null)
            {
                reason = "Vanilla scenario runtime is unavailable.";
                return false;
            }

            ScenarioRuntimeBinding existingBinding = _runtimeBindingService.CurrentBinding;
            if (existingBinding != null
                && existingBinding.IsActive
                && existingBinding.ScenarioQuestInstanceId.HasValue)
            {
                QuestInstance existingInstance;
                string existingReason;
                if (_vanillaRuntime.TryGetQuestInstance(existingBinding.ScenarioQuestInstanceId.Value, out existingInstance, out existingReason)
                    && existingInstance != null
                    && existingInstance.definition != null
                    && existingInstance.definition.IsScenario()
                    && string.Equals(existingInstance.definition.id, session.WorkingDefinition.Id, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                // Instance ids are local to a loaded world.  A persisted id is
                // only a hint; discard it before creating this world's carrier.
                existingBinding.ScenarioQuestInstanceId = null;
                _runtimeBindingService.SetBinding(existingBinding);
                MMLog.WriteInfo("[ScenarioPlaytestOrchestrator] Discarded unavailable playtest Scenario QuestInstance "
                    + "for '" + session.WorkingDefinition.Id + "'. " + (existingReason ?? "Carrier did not match the active draft."));
            }

            EnsureRuntimeBinding(session);
            ScenarioDef scenarioDef;
            try
            {
                scenarioDef = ScenarioDefinitionService.BuildPlayableScenarioDef(session.WorkingDefinition);
            }
            catch (Exception ex)
            {
                reason = "ScenarioDef build failed: " + ex.Message;
                return false;
            }

            QuestInstance instance;
            if (!_vanillaRuntime.TrySpawnScenario(scenarioDef, out instance, out reason) || instance == null)
            {
                if (string.IsNullOrEmpty(reason))
                    reason = "QuestManager did not return a Scenario QuestInstance.";
                return false;
            }

            ScenarioRuntimeBinding binding = _runtimeBindingService.CurrentBinding;
            if (binding == null)
            {
                reason = "Scenario binding disappeared while the completion carrier was being created.";
                return false;
            }

            binding.ScenarioQuestInstanceId = instance.id;
            _runtimeBindingService.SetBinding(binding);
            MMLog.WriteInfo("[ScenarioPlaytestOrchestrator] Bound playtest Scenario QuestInstance "
                + instance.id.ToString() + " for '" + session.WorkingDefinition.Id + "'.");
            return true;
        }
    }
}
