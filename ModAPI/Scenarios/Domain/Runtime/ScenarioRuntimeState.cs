using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public class ScenarioRuntimeState
    {
        public ScenarioRuntimeState()
        {
            ExecutedActions = new List<ScenarioExecutedActionRecord>();
            Flags = new List<ScenarioRuntimeFlag>();
            UnlockedBunker = new List<ScenarioUnlockedBunkerRecord>();
            ObjectStates = new List<ScenarioObjectRuntimeStateRecord>();
        }

        public string ScenarioId { get; set; }
        public string ScenarioVersion { get; set; }
        public string RuntimeBindingId { get; set; }
        public int LastProcessedDay { get; set; }
        public int LastProcessedHour { get; set; }
        public int LastProcessedMinute { get; set; }
        public List<ScenarioExecutedActionRecord> ExecutedActions { get; private set; }
        public List<ScenarioRuntimeFlag> Flags { get; private set; }
        public List<ScenarioUnlockedBunkerRecord> UnlockedBunker { get; private set; }
        public List<ScenarioObjectRuntimeStateRecord> ObjectStates { get; private set; }
    }

    public class ScenarioRuntimeFlag
    {
        public string FlagId { get; set; }
        public string Value { get; set; }
    }

    public class ScenarioUnlockedBunkerRecord
    {
        public string ExpansionId { get; set; }
        public int Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
    }
}
