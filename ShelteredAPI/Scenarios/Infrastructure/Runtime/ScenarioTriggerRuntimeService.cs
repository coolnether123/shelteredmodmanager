using System;
using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Infrastructure.Runtime{
    internal sealed class ScenarioTriggerRuntimeService : IScenarioTriggerRuntimeService, IScenarioEffectHandler, IScenarioConditionEvaluator
    {
        private readonly ScenarioRuntimeStateService _stateService;

        public ScenarioTriggerRuntimeService(ScenarioRuntimeStateService stateService)
        {
            _stateService = stateService;
        }

        public bool CanHandle(ScenarioEffectKind kind)
        {
            return kind == ScenarioEffectKind.FireTrigger;
        }

        public bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message)
        {
            string triggerId = effect != null ? (effect.TriggerId ?? effect.TargetId) : null;
            string source = effect != null ? ("effect:" + (effect.Id ?? effect.TargetId ?? triggerId ?? string.Empty)) : "effect";
            return Fire(state, triggerId, source, out message);
        }

        public bool CanEvaluate(ScenarioConditionKind kind)
        {
            return kind == ScenarioConditionKind.CustomTrigger;
        }

        public bool IsSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason)
        {
            reason = null;
            string triggerId = condition != null ? condition.TargetId : null;
            if (string.IsNullOrEmpty(triggerId))
            {
                reason = "Custom trigger condition is missing trigger id.";
                return false;
            }

            if (HasFired(state, triggerId))
                return true;

            reason = "Trigger has not fired: " + triggerId;
            return false;
        }

        public bool Fire(string triggerId, string source, out string message)
        {
            ScenarioRuntimeState state = _stateService != null ? _stateService.State : null;
            return Fire(state, triggerId, source, out message);
        }

        public bool Fire(ScenarioRuntimeState state, string triggerId, string source, out string message)
        {
            message = null;
            if (state == null)
            {
                message = "Scenario runtime state is not ready.";
                return false;
            }

            if (string.IsNullOrEmpty(triggerId))
            {
                message = "Trigger id is missing.";
                return false;
            }

            ScenarioFiredTriggerRecord record = Find(state, triggerId);
            if (record == null)
            {
                record = new ScenarioFiredTriggerRecord();
                record.TriggerId = triggerId;
                state.FiredTriggers.Add(record);
            }

            record.Source = string.IsNullOrEmpty(source) ? "runtime" : source;
            record.FiredDay = GameTime.Day;
            record.FiredHour = GameTime.Hour;
            record.FiredMinute = GameTime.Minute;
            record.FireCount = Math.Max(0, record.FireCount) + 1;
            return true;
        }

        public bool HasFired(ScenarioRuntimeState state, string triggerId)
        {
            return Find(state, triggerId) != null;
        }

        private static ScenarioFiredTriggerRecord Find(ScenarioRuntimeState state, string triggerId)
        {
            if (state == null || state.FiredTriggers == null || string.IsNullOrEmpty(triggerId))
                return null;

            for (int i = 0; i < state.FiredTriggers.Count; i++)
            {
                ScenarioFiredTriggerRecord record = state.FiredTriggers[i];
                if (record != null && string.Equals(record.TriggerId, triggerId, StringComparison.OrdinalIgnoreCase))
                    return record;
            }

            return null;
        }
    }
}
