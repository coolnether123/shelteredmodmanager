using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Named gate that unlocks scenario content when its conditions are satisfied.
    /// Gates can be referenced by placements, routes, effects, and scheduled actions.
    /// </summary>
    public class ScenarioGateDefinition
    {
        public ScenarioGateDefinition()
        {
            Conditions = new ScenarioConditionGroup();
        }

        public string Id { get; set; }
        public string DisplayName { get; set; }
        public ScenarioConditionGroup Conditions { get; set; }
    }
}
