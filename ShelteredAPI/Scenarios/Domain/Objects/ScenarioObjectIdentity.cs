using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Runtime identity and activation metadata for a scenario object.
    /// This is shared by object placements and runtime state tracking.
    /// </summary>
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
