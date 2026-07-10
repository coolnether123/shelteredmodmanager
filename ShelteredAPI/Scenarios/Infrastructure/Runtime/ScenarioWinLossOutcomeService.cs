using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class ScenarioWinLossOutcomeService : IScenarioWinLossOutcomeService
    {
        private readonly IScenarioQuestInstanceResolver _questInstanceResolver;
        private readonly IScenarioWinLossConditionAdapter _conditionAdapter;
        private readonly ScenarioConditionEvaluatorRegistry _conditionEvaluator;
        private readonly IVanillaScenarioRuntime _vanillaRuntime;
        private readonly ScenarioRuntimeExecutionLog _executionLog;
        private ScenarioDefinition _definition;
        private ScenarioRuntimeBinding _binding;
        private string _lastBlockedReason;

        public ScenarioWinLossOutcomeService(
            IScenarioQuestInstanceResolver questInstanceResolver,
            IScenarioWinLossConditionAdapter conditionAdapter,
            ScenarioConditionEvaluatorRegistry conditionEvaluator,
            IVanillaScenarioRuntime vanillaRuntime,
            ScenarioRuntimeExecutionLog executionLog)
        {
            _questInstanceResolver = questInstanceResolver;
            _conditionAdapter = conditionAdapter;
            _conditionEvaluator = conditionEvaluator;
            _vanillaRuntime = vanillaRuntime;
            _executionLog = executionLog;
        }

        public void Initialize(ScenarioDefinition definition, ScenarioRuntimeBinding binding)
        {
            _definition = definition;
            _binding = binding;
            _lastBlockedReason = null;
        }

        public void Tick(ScenarioRuntimeState state)
        {
            if (_definition == null || _definition.WinLossConditions == null || _binding == null || state == null)
                return;

            if (!string.IsNullOrEmpty(state.ScenarioOutcome))
                return;

            ConditionDef condition;
            string reason;
            if (TryFindSatisfied(_definition.WinLossConditions.LossConditions, state, out condition, out reason))
            {
                ResolveSatisfiedOutcome(state, false, condition);
                return;
            }

            if (TryFindSatisfied(_definition.WinLossConditions.WinConditions, state, out condition, out reason))
                ResolveSatisfiedOutcome(state, true, condition);
            else if (!string.IsNullOrEmpty(reason))
                LogBlocked(reason);
        }

        private void ResolveSatisfiedOutcome(ScenarioRuntimeState state, bool success, ConditionDef condition)
        {
            QuestInstance instance;
            string reason;
            if (!_questInstanceResolver.TryResolve(_binding, out instance, out reason))
            {
                LogBlocked(reason);
                ReturnAuthoringPlaytestToEditor();
                return;
            }

            Resolve(instance, state, success, condition);
        }

        private bool TryFindSatisfied(List<ConditionDef> conditions, ScenarioRuntimeState state, out ConditionDef satisfied, out string reason)
        {
            satisfied = null;
            reason = null;
            for (int i = 0; conditions != null && i < conditions.Count; i++)
            {
                ConditionDef condition = conditions[i];
                if (condition == null)
                    continue;

                ScenarioConditionRef conditionRef;
                string adapterReason;
                if (!_conditionAdapter.TryCreateConditionRef(_definition, _binding, condition, out conditionRef, out adapterReason))
                {
                    reason = adapterReason;
                    continue;
                }

                string evaluatorReason;
                if (_conditionEvaluator.AreConditionsSatisfied(_definition, new ScenarioConditionRef[] { conditionRef }, state, out evaluatorReason))
                {
                    satisfied = condition;
                    return true;
                }

                reason = evaluatorReason;
            }

            return false;
        }

        private void Resolve(QuestInstance instance, ScenarioRuntimeState state, bool success, ConditionDef condition)
        {
            if (instance == null || state == null)
                return;

            string reason;
            if (!_vanillaRuntime.TryFinishQuest(instance, success, out reason))
            {
                LogBlocked(reason);
                return;
            }

            state.ScenarioOutcome = success ? "Win" : "Loss";
            state.ScenarioOutcomeConditionId = condition != null ? condition.Id : null;
            UpdateScoreSnapshotOutcome(state, success);
            if (_executionLog != null)
            {
                _executionLog.Record(
                    condition != null ? condition.Id : "scenario-outcome",
                    success ? "Victory" : "Defeat",
                    "Scenario outcome",
                    ScenarioRuntimeExecutionLogOutcome.Fired,
                    condition != null ? condition.Type : null,
                    success ? "Vanilla scenario completed successfully." : "Vanilla scenario failed.");
            }
            MMLog.WriteInfo("[ScenarioWinLoss] Resolved scenario QuestInstance " + instance.id.ToString()
                + " as " + state.ScenarioOutcome
                + " via condition '" + (state.ScenarioOutcomeConditionId ?? string.Empty) + "'.");
            ReturnAuthoringPlaytestToEditor();
        }

        private static void ReturnAuthoringPlaytestToEditor()
        {
            try
            {
                ScenarioEditorController editor = ScenarioEditorController.Instance;
                if (editor != null && editor.CurrentSession != null)
                    editor.EndPlaytest();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ScenarioWinLoss] Outcome resolved, but authoring return could not be completed: " + ex.Message);
            }
        }

        private static void UpdateScoreSnapshotOutcome(ScenarioRuntimeState state, bool success)
        {
            if (state == null || state.ScoreSnapshot == null)
                return;

            state.ScoreSnapshot.CompletionState = success ? ScenarioScoreCompletionState.Won : ScenarioScoreCompletionState.Lost;
            state.ScoreSnapshot.Outcome = state.ScenarioOutcome;
            state.ScoreSnapshot.OutcomeConditionId = state.ScenarioOutcomeConditionId;
            try
            {
                state.ScoreSnapshot.Day = GameTime.Day;
                state.ScoreSnapshot.Hour = GameTime.Hour;
                state.ScoreSnapshot.Minute = GameTime.Minute;
            }
            catch
            {
            }
        }

        private void LogBlocked(string reason)
        {
            if (string.IsNullOrEmpty(reason)
                || string.Equals(_lastBlockedReason, reason, StringComparison.OrdinalIgnoreCase))
                return;

            _lastBlockedReason = reason;
            MMLog.WriteInfo("[ScenarioWinLoss] Waiting: " + reason);
        }
    }
}
