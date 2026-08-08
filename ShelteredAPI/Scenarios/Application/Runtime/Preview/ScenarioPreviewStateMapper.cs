using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Public;

namespace ShelteredAPI.Scenarios.Application.Runtime
{
    internal static class ScenarioPreviewStateMapper
    {
        public static ScenarioRuntimeSnapshot CaptureRuntimeState()
        {
            ScenarioRuntimeStateService service = ScenarioRuntimeCompositionRoot.Resolve<ScenarioRuntimeStateService>();
            ScenarioRuntimeState state = service != null ? service.State : null;
            if (state == null)
                return null;

            ScenarioRuntimeSnapshot snapshot = new ScenarioRuntimeSnapshot
            {
                ScenarioId = state.ScenarioId,
                ScenarioVersion = state.ScenarioVersion,
                RuntimeBindingId = state.RuntimeBindingId,
                Outcome = state.ScenarioOutcome,
                OutcomeConditionId = state.ScenarioOutcomeConditionId,
                LastProcessedDay = state.LastProcessedDay,
                LastProcessedHour = state.LastProcessedHour,
                LastProcessedMinute = state.LastProcessedMinute
            };
            for (int i = 0; state.ExecutedActions != null && i < state.ExecutedActions.Count; i++)
            {
                ScenarioExecutedActionRecord action = state.ExecutedActions[i];
                if (action != null)
                {
                    snapshot.AddAction(new ScenarioRuntimeActionSnapshot
                    {
                        ActionKey = action.ActionKey,
                        ActionType = action.ActionType,
                        Day = action.FiredDay,
                        Hour = action.FiredHour,
                        Minute = action.FiredMinute,
                        Status = action.Status.ToString(),
                        Message = action.Message
                    });
                }
            }
            for (int i = 0; state.Flags != null && i < state.Flags.Count; i++)
            {
                ScenarioRuntimeFlag flag = state.Flags[i];
                if (flag != null)
                    snapshot.AddFlag(new ScenarioRuntimeFlagSnapshot { Id = flag.FlagId, Value = flag.Value });
            }
            return snapshot;
        }

        public static ScenarioRuntimeExecutionEntrySnapshot[] CaptureExecutionLog(int maximum)
        {
            ScenarioRuntimeExecutionLogEntry[] entries = ScenarioRuntimeCompositionRoot
                .Resolve<ScenarioRuntimeExecutionLog>()
                .GetMostRecentFirst(maximum);
            ScenarioRuntimeExecutionEntrySnapshot[] snapshots =
                new ScenarioRuntimeExecutionEntrySnapshot[entries != null ? entries.Length : 0];
            for (int i = 0; entries != null && i < entries.Length; i++)
            {
                ScenarioRuntimeExecutionLogEntry entry = entries[i];
                snapshots[i] = new ScenarioRuntimeExecutionEntrySnapshot
                {
                    Day = entry.Day,
                    Hour = entry.Hour,
                    Minute = entry.Minute,
                    ElementId = entry.ElementId,
                    DisplayName = entry.DisplayName,
                    Kind = entry.Kind,
                    Outcome = entry.Outcome.ToString(),
                    ConditionSummary = entry.ConditionSummary,
                    Detail = entry.Detail,
                    PlainLanguage = entry.ToPlainLanguage()
                };
            }
            return snapshots;
        }
    }
}
