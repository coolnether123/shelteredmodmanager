using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
namespace ShelteredAPI.Scenarios.Application.Scheduling{
    internal sealed class ScenarioScheduleRuntimeCoordinator
    {
        private readonly ScenarioRuntimeStateService _stateService;
        private readonly ScenarioRuntimeExecutionJournal _journal;
        private readonly ScenarioConditionEvaluatorRegistry _conditions;
        private readonly ScenarioEffectDispatcher _effects;
        private readonly IScenarioWinLossOutcomeService _winLossOutcomeService;
        private readonly IScenarioScheduledActionProvider[] _providers;
        private ScenarioDefinition _definition;
        private List<ScenarioScheduledActionDefinition> _actions = new List<ScenarioScheduledActionDefinition>();

        public ScenarioScheduleRuntimeCoordinator(
            ScenarioRuntimeStateService stateService,
            ScenarioRuntimeExecutionJournal journal,
            ScenarioConditionEvaluatorRegistry conditions,
            ScenarioEffectDispatcher effects,
            IScenarioWinLossOutcomeService winLossOutcomeService,
            IScenarioScheduledActionProvider[] providers)
        {
            _stateService = stateService;
            _journal = journal;
            _conditions = conditions;
            _effects = effects;
            _winLossOutcomeService = winLossOutcomeService;
            _providers = providers ?? new IScenarioScheduledActionProvider[0];
        }

        public void Initialize(ScenarioDefinition definition, ScenarioRuntimeBinding binding)
        {
            _definition = definition;
            _stateService.EnsureHooked();
            _stateService.Bind(definition, binding);
            ScenarioWorldEventRuntimeState.Bind(definition);
            if (_winLossOutcomeService != null)
                _winLossOutcomeService.Initialize(definition, binding);
            _actions = BuildActions(definition);
        }

        public void TickOnGameTimeChanged()
        {
            if (_definition == null)
                return;

            for (int i = 0; i < _actions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = _actions[i];
                if (action == null || string.IsNullOrEmpty(action.Id))
                    continue;

                if (!IsRepeatable(action)
                    && (_journal.HasExecuted(action.Id) || _journal.HasRecord(action.Id, ScenarioExecutedActionStatus.Skipped)))
                {
                    continue;
                }

                string reason;
                ScenarioSchedulePolicyDecision scheduleDecision = EvaluateSchedule(action, out reason);
                if (scheduleDecision == ScenarioSchedulePolicyDecision.NotDue)
                    continue;
                if (scheduleDecision == ScenarioSchedulePolicyDecision.Skipped)
                {
                    _journal.Record(action, ScenarioExecutedActionStatus.Skipped, reason);
                    continue;
                }

                if (!_conditions.IsGateSatisfied(_definition, action.GateId, _journal.State, out reason)
                    || !_conditions.AreConditionsSatisfied(_definition, action.ConditionRefs, _journal.State, out reason))
                {
                    if (!_journal.HasRecord(action.Id, ScenarioExecutedActionStatus.Blocked))
                        _journal.Record(action, ScenarioExecutedActionStatus.Blocked, reason);
                    continue;
                }

                ExecuteAction(action);
            }

            if (_winLossOutcomeService != null)
                _winLossOutcomeService.Tick(_journal.State);

            _journal.UpdateLastProcessedTime();
        }

        private void ExecuteAction(ScenarioScheduledActionDefinition action)
        {
            string message = null;
            bool ok = true;
            if (action.Effects == null || action.Effects.Count == 0)
            {
                _journal.Record(action, ScenarioExecutedActionStatus.Failed, "No effects were defined.");
                return;
            }

            for (int i = 0; i < action.Effects.Count; i++)
            {
                string effectMessage;
                if (!_effects.Dispatch(_definition, action.Effects[i], _journal.State, out effectMessage))
                {
                    ok = false;
                    message = effectMessage;
                    break;
                }
                if (!string.IsNullOrEmpty(effectMessage))
                    message = string.IsNullOrEmpty(message) ? effectMessage : message + " " + effectMessage;
            }

            _journal.Record(action, ok ? ScenarioExecutedActionStatus.Succeeded : ScenarioExecutedActionStatus.Failed, message);
            if (!ok)
                MMLog.WriteWarning("[ScenarioScheduleRuntime] Scheduled action failed: " + action.Id + " " + (message ?? string.Empty));
        }

        private List<ScenarioScheduledActionDefinition> BuildActions(ScenarioDefinition definition)
        {
            List<ScenarioScheduledActionDefinition> actions = new List<ScenarioScheduledActionDefinition>();
            for (int i = 0; _providers != null && i < _providers.Length; i++)
            {
                if (_providers[i] != null)
                    _providers[i].AddActions(definition, actions);
            }
            return actions;
        }

        private static bool IsRepeatable(ScenarioScheduledActionDefinition action)
        {
            return action != null && action.Policy != null && action.Policy.Repeatable;
        }

        private ScenarioSchedulePolicyDecision EvaluateSchedule(ScenarioScheduledActionDefinition action, out string reason)
        {
            long? lastAttemptMinutes = null;
            ScenarioExecutedActionRecord lastRecord;
            if (_journal.TryGetLastExecutionAttempt(action.Id, out lastRecord))
                lastAttemptMinutes = ScenarioSchedulePolicyEvaluator.ToGameMinutes(lastRecord.FiredDay, lastRecord.FiredHour, lastRecord.FiredMinute);

            return ScenarioSchedulePolicyEvaluator.Evaluate(
                action,
                ScenarioSchedulePolicyEvaluator.ToGameMinutes(GameTime.Day, GameTime.Hour, GameTime.Minute),
                _journal.CountRecords(action.Id, ScenarioExecutedActionStatus.Succeeded),
                _journal.CountAttempts(action.Id),
                lastAttemptMinutes,
                out reason);
        }
    }
}
