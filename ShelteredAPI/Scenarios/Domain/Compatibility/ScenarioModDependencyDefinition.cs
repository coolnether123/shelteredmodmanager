using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Mod dependency or compatibility rule discovered or authored for a scenario.
    /// Reasons and content references explain why the dependency exists for validation and UI.
    /// </summary>
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
