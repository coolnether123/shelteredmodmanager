using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Domain.Compatibility;
namespace ShelteredScenarioEditor.Application.Compatibility{
    internal sealed class ScenarioModCompatibilityReport
    {
        public ScenarioModCompatibilityReport()
        {
            RequiredMods = new List<ScenarioModDependencyDefinition>();
            OptionalMods = new List<ScenarioModDependencyDefinition>();
            MissingRequiredMods = new List<ScenarioModDependencyDefinition>();
            VersionMismatches = new List<ScenarioModDependencyDefinition>();
            UnknownReferences = new List<string>();
        }

        public List<ScenarioModDependencyDefinition> RequiredMods { get; private set; }
        public List<ScenarioModDependencyDefinition> OptionalMods { get; private set; }
        public List<ScenarioModDependencyDefinition> MissingRequiredMods { get; private set; }
        public List<ScenarioModDependencyDefinition> VersionMismatches { get; private set; }
        public List<string> UnknownReferences { get; private set; }
    }
}
