using System;
using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Application.Runtime{
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

        public int CountRecords(string actionKey, ScenarioExecutedActionStatus status)
        {
            int count = 0;
            if (string.IsNullOrEmpty(actionKey) || State == null || State.ExecutedActions == null)
                return count;

            for (int i = 0; i < State.ExecutedActions.Count; i++)
            {
                ScenarioExecutedActionRecord record = State.ExecutedActions[i];
                if (record != null
                    && string.Equals(record.ActionKey, actionKey, StringComparison.OrdinalIgnoreCase)
                    && record.Status == status)
                {
                    count++;
                }
            }
            return count;
        }

        public int CountAttempts(string actionKey)
        {
            int count = 0;
            if (string.IsNullOrEmpty(actionKey) || State == null || State.ExecutedActions == null)
                return count;

            for (int i = 0; i < State.ExecutedActions.Count; i++)
            {
                ScenarioExecutedActionRecord record = State.ExecutedActions[i];
                if (record != null
                    && string.Equals(record.ActionKey, actionKey, StringComparison.OrdinalIgnoreCase)
                    && IsExecutionAttempt(record.Status))
                {
                    count++;
                }
            }
            return count;
        }

        public bool TryGetLastExecutionAttempt(string actionKey, out ScenarioExecutedActionRecord lastRecord)
        {
            lastRecord = null;
            if (string.IsNullOrEmpty(actionKey) || State == null || State.ExecutedActions == null)
                return false;

            for (int i = 0; i < State.ExecutedActions.Count; i++)
            {
                ScenarioExecutedActionRecord record = State.ExecutedActions[i];
                if (record == null
                    || !string.Equals(record.ActionKey, actionKey, StringComparison.OrdinalIgnoreCase)
                    || !IsExecutionAttempt(record.Status))
                {
                    continue;
                }

                if (lastRecord == null || CompareExecutionTime(record, lastRecord) >= 0)
                    lastRecord = record;
            }

            return lastRecord != null;
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

        private static int CompareExecutionTime(ScenarioExecutedActionRecord left, ScenarioExecutedActionRecord right)
        {
            if (left == null && right == null) return 0;
            if (left == null) return -1;
            if (right == null) return 1;

            int day = left.FiredDay.CompareTo(right.FiredDay);
            if (day != 0) return day;

            int hour = left.FiredHour.CompareTo(right.FiredHour);
            if (hour != 0) return hour;

            return left.FiredMinute.CompareTo(right.FiredMinute);
        }

        private static bool IsExecutionAttempt(ScenarioExecutedActionStatus status)
        {
            return status == ScenarioExecutedActionStatus.Succeeded
                || status == ScenarioExecutedActionStatus.Failed
                || status == ScenarioExecutedActionStatus.Skipped;
        }
    }
}
