using System;
using System.Collections.Generic;
using ShelteredAPI.Content;

namespace ShelteredAPI.Scenarios.Application.Runtime
{
    internal enum ScenarioRuntimeExecutionLogOutcome
    {
        Scheduled = 0,
        Fired = 1,
        SkippedConditionFalse = 2,
        FailedWithError = 3,
        OnceAlreadyConsumed = 4,
        ManuallyFired = 5
    }

    internal sealed class ScenarioRuntimeExecutionLogEntry
    {
        public int Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string ElementId { get; set; }
        public string DisplayName { get; set; }
        public string Kind { get; set; }
        public ScenarioRuntimeExecutionLogOutcome Outcome { get; set; }
        public string ConditionSummary { get; set; }
        public string Detail { get; set; }

        public string ToPlainLanguage()
        {
            string name = string.IsNullOrEmpty(DisplayName) ? (ElementId ?? "Scenario element") : DisplayName;
            string verb = Outcome == ScenarioRuntimeExecutionLogOutcome.Scheduled ? "scheduled"
                : Outcome == ScenarioRuntimeExecutionLogOutcome.Fired ? "fired"
                : Outcome == ScenarioRuntimeExecutionLogOutcome.ManuallyFired ? "manually fired"
                : Outcome == ScenarioRuntimeExecutionLogOutcome.SkippedConditionFalse ? "skipped (condition false)"
                : Outcome == ScenarioRuntimeExecutionLogOutcome.OnceAlreadyConsumed ? "skipped (already consumed)"
                : "failed";
            return "Day " + Day + " " + Hour.ToString("D2") + ":" + Minute.ToString("D2")
                + " - " + (Kind ?? "Element") + " '" + name + "' " + verb
                + (string.IsNullOrEmpty(ConditionSummary) ? string.Empty : ": " + ConditionSummary)
                + (string.IsNullOrEmpty(Detail) ? string.Empty : " (" + Detail + ")");
        }
    }

    /// <summary>Small, in-memory authoring journal. It deliberately does not add save-file churn.</summary>
    internal sealed class ScenarioRuntimeExecutionLog
    {
        internal const int Capacity = 128;
        private readonly ScenarioRuntimeExecutionLogEntry[] _entries = new ScenarioRuntimeExecutionLogEntry[Capacity];
        private int _next;
        private int _count;

        public bool Enabled { get; set; }
        public int Count { get { return _count; } }

        public void Clear()
        {
            Array.Clear(_entries, 0, _entries.Length);
            _next = 0;
            _count = 0;
        }

        public void Record(string elementId, string displayName, string kind, ScenarioRuntimeExecutionLogOutcome outcome, string conditionSummary, string detail)
        {
            // The closed-console path is a single branch and allocates nothing.
            if (!Enabled)
                return;

            _entries[_next] = new ScenarioRuntimeExecutionLogEntry
            {
                Day = GameTime.Day,
                Hour = GameTime.Hour,
                Minute = GameTime.Minute,
                ElementId = elementId,
                DisplayName = displayName,
                Kind = kind,
                Outcome = outcome,
                ConditionSummary = conditionSummary,
                Detail = detail
            };
            _next = (_next + 1) % Capacity;
            if (_count < Capacity)
                _count++;
        }

        public ScenarioRuntimeExecutionLogEntry[] GetMostRecentFirst(int maximum)
        {
            int take = Math.Min(Math.Max(0, maximum), _count);
            ScenarioRuntimeExecutionLogEntry[] copy = new ScenarioRuntimeExecutionLogEntry[take];
            for (int i = 0; i < take; i++)
            {
                int index = (_next - 1 - i + Capacity) % Capacity;
                copy[i] = _entries[index];
            }
            return copy;
        }
    }
}
