using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// One scenario effect and the fields needed by its selected effect kind.
    /// Effects are executed by scenario runtime handlers after schedule and gate checks pass.
    /// </summary>
    public class ScenarioEffectDefinition
    {
        public ScenarioEffectDefinition()
        {
            Kind = ScenarioEffectKind.SetScenarioFlag;
            Properties = new List<ScenarioProperty>();
        }

        public string Id { get; set; }
        public ScenarioEffectKind Kind { get; set; }
        public string TargetId { get; set; }
        public string ItemId { get; set; }
        public int Quantity { get; set; }
        public string WeatherState { get; set; }
        public int DurationHours { get; set; }
        public string SurvivorId { get; set; }
        public string QuestId { get; set; }
        public string ObjectId { get; set; }
        public string BunkerExpansionId { get; set; }
        public string FlagId { get; set; }
        public string FlagValue { get; set; }
        public string TriggerId { get; set; }
        public List<ScenarioProperty> Properties { get; private set; }
    }
}
