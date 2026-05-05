using System.Collections.Generic;

using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Effects;
namespace ShelteredAPI.Scenarios.Domain.Scheduling{
    /// <summary>
    /// Action scheduled on the scenario timeline.
    /// Conditions and gate checks run before effects are dispatched.
    /// </summary>
    public class ScenarioScheduledActionDefinition
    {
        public ScenarioScheduledActionDefinition()
        {
            DueTime = new ScenarioScheduleTime();
            Policy = new ScenarioSchedulePolicy();
            ConditionRefs = new List<ScenarioConditionRef>();
            Effects = new List<ScenarioEffectDefinition>();
        }

        public string Id { get; set; }
        public string ActionType { get; set; }
        public string GateId { get; set; }
        public ScenarioScheduleTime DueTime { get; set; }
        public ScenarioSchedulePolicy Policy { get; set; }
        public List<ScenarioConditionRef> ConditionRefs { get; private set; }
        public List<ScenarioEffectDefinition> Effects { get; private set; }
    }
}
