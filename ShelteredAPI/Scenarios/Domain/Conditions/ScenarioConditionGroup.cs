using System.Collections.Generic;

using ModAPI.Scenarios;
namespace ShelteredAPI.Scenarios.Domain.Conditions{
    /// <summary>
    /// Nested condition group used by scenario gates and scheduled actions.
    /// Groups can be combined with all/any semantics.
    /// </summary>
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
