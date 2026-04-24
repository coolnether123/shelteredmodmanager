using System.Collections.Generic;

namespace ModAPI.Scenarios
{
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
