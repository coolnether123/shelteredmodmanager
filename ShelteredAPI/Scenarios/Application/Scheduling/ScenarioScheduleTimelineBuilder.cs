using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
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
