using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public class ScenarioObjectIdentity
    {
        public ScenarioObjectIdentity()
        {
            Tags = new List<string>();
            StartState = ScenarioObjectStartState.StartsEnabled;
        }

        public string ScenarioObjectId { get; set; }
        public string RuntimeBindingKey { get; set; }
        public ScenarioObjectStartState StartState { get; set; }
        public string PlacementPhase { get; set; }
        public string RequiredFoundationId { get; set; }
        public string RequiredBunkerExpansionId { get; set; }
        public string UnlockGateId { get; set; }
        public string ScheduledActivationId { get; set; }
        public List<string> Tags { get; private set; }
    }
}
