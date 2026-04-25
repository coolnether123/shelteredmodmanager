using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioModAuthoringExtension
    {
        void AddTimelineEntries(ScenarioDefinition definition, IList<ScenarioTimelineEntry> entries);
        void AddCompatibilityReferences(ScenarioDefinition definition, ScenarioModReferenceIndex index);
    }
}
