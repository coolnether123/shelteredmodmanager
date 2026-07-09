using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Timeline;
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
        private readonly ScenarioRuntimeExecutionLog _executionLog;
        private ScenarioDefinition _definition;
        private List<ScenarioScheduledActionDefinition> _actions = new List<ScenarioScheduledActionDefinition>();
        private readonly HashSet<string> _executingActions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _onceConsumptionLogged = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public ScenarioScheduleRuntimeCoordinator(
            ScenarioRuntimeStateService stateService,
            ScenarioRuntimeExecutionJournal journal,
            ScenarioConditionEvaluatorRegistry conditions,
            ScenarioEffectDispatcher effects,
            IScenarioWinLossOutcomeService winLossOutcomeService,
            IScenarioScheduledActionProvider[] providers,
            ScenarioRuntimeExecutionLog executionLog)
        {
            _stateService = stateService;
            _journal = journal;
            _conditions = conditions;
            _effects = effects;
            _winLossOutcomeService = winLossOutcomeService;
            _providers = providers ?? new IScenarioScheduledActionProvider[0];
            _executionLog = executionLog;
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
            _executingActions.Clear();
            _onceConsumptionLogged.Clear();
            for (int i = 0; i < _actions.Count; i++)
                Record(_actions[i], ScenarioRuntimeExecutionLogOutcome.Scheduled, null, "Awaiting its authored schedule.");
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

                if (_executingActions.Contains(action.Id))
                    continue;

                if (!IsRepeatable(action)
                    && (_journal.HasExecuted(action.Id) || _journal.HasRecord(action.Id, ScenarioExecutedActionStatus.Skipped)))
                {
                    if (_onceConsumptionLogged.Add(action.Id))
                        Record(action, ScenarioRuntimeExecutionLogOutcome.OnceAlreadyConsumed, null, "Once-only action was already consumed.");
                    continue;
                }

                string reason;
                ScenarioSchedulePolicyDecision scheduleDecision = EvaluateSchedule(action, out reason);
                if (scheduleDecision == ScenarioSchedulePolicyDecision.NotDue)
                    continue;
                if (scheduleDecision == ScenarioSchedulePolicyDecision.Skipped)
                {
                    _journal.Record(action, ScenarioExecutedActionStatus.Skipped, reason);
                    Record(action, ScenarioRuntimeExecutionLogOutcome.OnceAlreadyConsumed, null, reason);
                    continue;
                }

                if (!_conditions.IsGateSatisfied(_definition, action.GateId, _journal.State, out reason)
                    || !_conditions.AreConditionsSatisfied(_definition, action.ConditionRefs, _journal.State, out reason))
                {
                    if (!_journal.HasRecord(action.Id, ScenarioExecutedActionStatus.Blocked))
                    {
                        _journal.Record(action, ScenarioExecutedActionStatus.Blocked, reason);
                        Record(action, ScenarioRuntimeExecutionLogOutcome.SkippedConditionFalse, reason, null);
                    }
                    continue;
                }

                ExecuteAction(action, false);
            }

            if (_winLossOutcomeService != null)
                _winLossOutcomeService.Tick(_journal.State);

            _journal.UpdateLastProcessedTime();
        }

        internal bool TryFireNow(string actionId, out string message)
        {
            message = null;
            ScenarioScheduledActionDefinition action = FindAction(actionId);
            if (action == null)
            {
                message = "Scheduled action is not active in this playtest.";
                return false;
            }
            if (_executingActions.Contains(action.Id))
            {
                message = "Scheduled action is already being applied.";
                return false;
            }
            if (!IsRepeatable(action) && _journal.HasExecuted(action.Id))
            {
                message = "This once-only action has already been consumed.";
                Record(action, ScenarioRuntimeExecutionLogOutcome.OnceAlreadyConsumed, null, message);
                return false;
            }
            return ExecuteAction(action, true, out message);
        }

        internal bool TryGetMinutesUntilNextAuthoredEvent(int maximumMinutes, out int minutes)
        {
            minutes = 0;
            long now = ScenarioSchedulePolicyEvaluator.ToGameMinutes(GameTime.Day, GameTime.Hour, GameTime.Minute);
            long best = long.MaxValue;
            for (int i = 0; i < _actions.Count; i++)
            {
                ScenarioScheduledActionDefinition action = _actions[i];
                if (action == null || action.DueTime == null || (!IsRepeatable(action) && _journal.HasExecuted(action.Id)))
                    continue;
                long due = ScenarioSchedulePolicyEvaluator.ToGameMinutes(action.DueTime.Day, action.DueTime.Hour, action.DueTime.Minute);
                if (due >= now && due - now < best)
                    best = due - now;
            }
            if (best == long.MaxValue || best > maximumMinutes)
                return false;
            minutes = (int)best;
            return true;
        }

        private void ExecuteAction(ScenarioScheduledActionDefinition action, bool manuallyFired)
        {
            string ignored;
            ExecuteAction(action, manuallyFired, out ignored);
        }

        private bool ExecuteAction(ScenarioScheduledActionDefinition action, bool manuallyFired, out string finalMessage)
        {
            finalMessage = null;
            if (action == null || string.IsNullOrEmpty(action.Id))
                return false;

            _executingActions.Add(action.Id);
            string message = null;
            bool ok = true;
            try
            {
                if (action.Effects == null || action.Effects.Count == 0)
                {
                    _journal.Record(action, ScenarioExecutedActionStatus.Failed, "No effects were defined.");
                    Record(action, ScenarioRuntimeExecutionLogOutcome.FailedWithError, null, "No effects were defined.");
                    finalMessage = "No effects were defined.";
                    return false;
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
                Record(action, ok ? (manuallyFired ? ScenarioRuntimeExecutionLogOutcome.ManuallyFired : ScenarioRuntimeExecutionLogOutcome.Fired) : ScenarioRuntimeExecutionLogOutcome.FailedWithError, null, message);
                if (!ok)
                    MMLog.WriteWarning("[ScenarioScheduleRuntime] Scheduled action failed: " + action.Id + " " + (message ?? string.Empty));
                finalMessage = message ?? (ok ? "Action applied." : "Action failed.");
                return ok;
            }
            finally
            {
                _executingActions.Remove(action.Id);
            }
        }

        private ScenarioScheduledActionDefinition FindAction(string actionId)
        {
            for (int i = 0; i < _actions.Count; i++)
                if (_actions[i] != null && string.Equals(_actions[i].Id, actionId, StringComparison.OrdinalIgnoreCase))
                    return _actions[i];
            return null;
        }

        private void Record(ScenarioScheduledActionDefinition action, ScenarioRuntimeExecutionLogOutcome outcome, string conditionSummary, string detail)
        {
            if (_executionLog == null || action == null)
                return;
            _executionLog.Record(action.Id, ScenarioTimelineCreatorText.ScheduledActionName(null, action), string.IsNullOrEmpty(action.ActionType) ? "Scheduled action" : action.ActionType, outcome, conditionSummary, detail);
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
