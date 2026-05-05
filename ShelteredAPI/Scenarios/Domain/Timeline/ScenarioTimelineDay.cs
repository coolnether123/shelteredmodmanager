using System.Collections.Generic;

using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Timeline bucket for all scenario entries scheduled on one day.
    /// </summary>
    public class ScenarioTimelineDay
    {
        public ScenarioTimelineDay()
        {
            Entries = new List<ScenarioTimelineEntry>();
        }

        public int Day { get; set; }
        public List<ScenarioTimelineEntry> Entries { get; private set; }
    }
}
