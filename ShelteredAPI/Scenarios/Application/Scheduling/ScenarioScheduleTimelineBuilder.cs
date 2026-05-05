using System.Collections.Generic;
using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Runtime;
using ShelteredAPI.Scenarios.Domain.Timeline;
namespace ShelteredAPI.Scenarios.Application.Scheduling{
    internal sealed class ScenarioScheduleTimelineBuilder
    {
        private readonly ScenarioTimelineBuilder _timelineBuilder;

        public ScenarioScheduleTimelineBuilder(ScenarioTimelineBuilder timelineBuilder)
        {
            _timelineBuilder = timelineBuilder;
        }

        public List<ScenarioTimelineEntry> Build(ScenarioDefinition definition, ScenarioRuntimeState runtimeState)
        {
            return _timelineBuilder.BuildEntries(definition, runtimeState);
        }
    }
}
