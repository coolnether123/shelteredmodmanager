using System.Collections.Generic;

using ModAPI.Scenarios;

using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Scheduling;
namespace ShelteredAPI.Scenarios.Domain.Conditions{
    /// <summary>
    /// One typed condition reference with properties needed by the selected condition kind.
    /// Target IDs point at scenario-local objects such as items, quests, survivors, gates, or flags.
    /// </summary>
    public class ScenarioConditionRef
    {
        public ScenarioConditionRef()
        {
            Kind = ScenarioConditionKind.TimeReached;
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public ScenarioConditionKind Kind { get; set; }
        public string TargetId { get; set; }
        public string Comparison { get; set; }
        public int Quantity { get; set; }
        public string StatId { get; set; }
        public int StatValue { get; set; }
        public string TraitId { get; set; }
        public string FlagId { get; set; }
        public string FlagValue { get; set; }
        public ScenarioScheduleTime Time { get; set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }
}
