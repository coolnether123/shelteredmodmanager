using System.Collections.Generic;

using ModAPI.Scenarios;

using ShelteredAPI.Content;
namespace ShelteredAPI.Scenarios.Domain.Runtime{
    internal class ScenarioRuntimeState
    {
        public ScenarioRuntimeState()
        {
            ExecutedActions = new List<ScenarioExecutedActionRecord>();
            Flags = new List<ScenarioRuntimeFlag>();
            FiredTriggers = new List<ScenarioFiredTriggerRecord>();
            UnlockedBunker = new List<ScenarioUnlockedBunkerRecord>();
            ObjectStates = new List<ScenarioObjectRuntimeStateRecord>();
        }

        public string ScenarioId { get; set; }
        public string ScenarioVersion { get; set; }
        public string RuntimeBindingId { get; set; }
        public string ScenarioOutcome { get; set; }
        public string ScenarioOutcomeConditionId { get; set; }
        public int LastProcessedDay { get; set; }
        public int LastProcessedHour { get; set; }
        public int LastProcessedMinute { get; set; }
        public List<ScenarioExecutedActionRecord> ExecutedActions { get; private set; }
        public List<ScenarioRuntimeFlag> Flags { get; private set; }
        public List<ScenarioFiredTriggerRecord> FiredTriggers { get; private set; }
        public List<ScenarioUnlockedBunkerRecord> UnlockedBunker { get; private set; }
        public List<ScenarioObjectRuntimeStateRecord> ObjectStates { get; private set; }
    }

    internal class ScenarioRuntimeFlag
    {
        public string FlagId { get; set; }
        public string Value { get; set; }
    }

    internal class ScenarioFiredTriggerRecord
    {
        public string TriggerId { get; set; }
        public string Source { get; set; }
        public int FiredDay { get; set; }
        public int FiredHour { get; set; }
        public int FiredMinute { get; set; }
        public int FireCount { get; set; }
    }

    internal class ScenarioUnlockedBunkerRecord
    {
        public string ExpansionId { get; set; }
        public int Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
    }
}
