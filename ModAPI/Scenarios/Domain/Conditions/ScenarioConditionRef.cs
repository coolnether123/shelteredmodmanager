using System.Collections.Generic;

namespace ModAPI.Scenarios
{
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
