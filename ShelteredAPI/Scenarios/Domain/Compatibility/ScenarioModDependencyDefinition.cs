using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    public class ScenarioModDependencyDefinition
    {
        public ScenarioModDependencyDefinition()
        {
            Kind = ScenarioModDependencyKind.Required;
            Reasons = new List<ScenarioModReferenceReason>();
            ContentReferences = new List<string>();
        }

        public string ModId { get; set; }
        public string Version { get; set; }
        public ScenarioModDependencyKind Kind { get; set; }
        public bool Manual { get; set; }
        public List<ScenarioModReferenceReason> Reasons { get; private set; }
        public List<string> ContentReferences { get; private set; }
    }
}
