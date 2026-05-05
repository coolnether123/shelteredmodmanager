using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Timeline;
namespace ShelteredAPI.Scenarios.Application.Compatibility{
    internal interface IScenarioModAuthoringExtension
    {
        void AddTimelineEntries(ScenarioDefinition definition, IList<ScenarioTimelineEntry> entries);
        void AddCompatibilityReferences(ScenarioDefinition definition, ScenarioModReferenceIndex index);
    }
}
