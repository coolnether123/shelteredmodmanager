using System;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioRuntimeExecutionJournal
    {
        private readonly ScenarioRuntimeStateService _stateService;

        public ScenarioRuntimeExecutionJournal(ScenarioRuntimeStateService stateService)
        {
            _stateService = stateService;
        }

        public ScenarioRuntimeState State
        {
            get { return _stateService.State; }
        }

        public bool HasExecuted(string actionKey)
        {
            if (string.IsNullOrEmpty(actionKey) || State == null || State.ExecutedActions == null)
                return false;

            for (int i = 0; i < State.ExecutedActions.Count; i++)
            {
                ScenarioExecutedActionRecord record = State.ExecutedActions[i];
                if (record != null
                    && string.Equals(record.ActionKey, actionKey, StringComparison.OrdinalIgnoreCase)
                    && record.Status == ScenarioExecutedActionStatus.Succeeded)
                {
                    return true;
                }
            }
            return false;
        }

        public bool HasRecord(string actionKey, ScenarioExecutedActionStatus status)
        {
            if (string.IsNullOrEmpty(actionKey) || State == null || State.ExecutedActions == null)
                return false;

            for (int i = 0; i < State.ExecutedActions.Count; i++)
            {
                ScenarioExecutedActionRecord record = State.ExecutedActions[i];
                if (record != null
                    && string.Equals(record.ActionKey, actionKey, StringComparison.OrdinalIgnoreCase)
                    && record.Status == status)
                {
                    return true;
                }
            }
            return false;
        }

        public void Record(ScenarioScheduledActionDefinition action, ScenarioExecutedActionStatus status, string message)
        {
            if (State == null || action == null)
                return;

            State.ExecutedActions.Add(new ScenarioExecutedActionRecord
            {
                ScenarioId = State.ScenarioId,
                ScenarioVersion = State.ScenarioVersion,
                RuntimeBindingId = State.RuntimeBindingId,
                ActionKey = action.Id,
                ActionType = action.ActionType,
                FiredDay = GameTime.Day,
                FiredHour = GameTime.Hour,
                FiredMinute = GameTime.Minute,
                Status = status,
                Message = message
            });
        }

        public void UpdateLastProcessedTime()
        {
            if (State == null)
                return;
            State.LastProcessedDay = GameTime.Day;
            State.LastProcessedHour = GameTime.Hour;
            State.LastProcessedMinute = GameTime.Minute;
        }
    }
}
