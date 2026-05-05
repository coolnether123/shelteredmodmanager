using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Runtime;
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
        private ScenarioDefinition _definition;
        private ScenarioRuntimeBinding _binding;
        private string _lastBlockedReason;

        public ScenarioWinLossOutcomeService(
            IScenarioQuestInstanceResolver questInstanceResolver,
            IScenarioWinLossConditionAdapter conditionAdapter,
            ScenarioConditionEvaluatorRegistry conditionEvaluator,
            IVanillaScenarioRuntime vanillaRuntime)
        {
            _questInstanceResolver = questInstanceResolver;
            _conditionAdapter = conditionAdapter;
            _conditionEvaluator = conditionEvaluator;
            _vanillaRuntime = vanillaRuntime;
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

            QuestInstance instance;
            string reason;
            if (!_questInstanceResolver.TryResolve(_binding, out instance, out reason))
            {
                LogBlocked(reason);
                return;
            }

            ConditionDef condition;
            if (TryFindSatisfied(_definition.WinLossConditions.LossConditions, state, out condition, out reason))
            {
                Resolve(instance, state, false, condition);
                return;
            }

            if (TryFindSatisfied(_definition.WinLossConditions.WinConditions, state, out condition, out reason))
                Resolve(instance, state, true, condition);
            else if (!string.IsNullOrEmpty(reason))
                LogBlocked(reason);
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
            MMLog.WriteInfo("[ScenarioWinLoss] Resolved scenario QuestInstance " + instance.id.ToString()
                + " as " + state.ScenarioOutcome
                + " via condition '" + (state.ScenarioOutcomeConditionId ?? string.Empty) + "'.");
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
