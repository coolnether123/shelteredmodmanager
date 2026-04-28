using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    public class ScenarioConditionGroup
    {
        public ScenarioConditionGroup()
        {
            Mode = ScenarioConditionGroupMode.All;
            Conditions = new List<ScenarioConditionRef>();
            Groups = new List<ScenarioConditionGroup>();
        }

        public ScenarioConditionGroupMode Mode { get; set; }
        public List<ScenarioConditionRef> Conditions { get; private set; }
        public List<ScenarioConditionGroup> Groups { get; private set; }
    }
}
