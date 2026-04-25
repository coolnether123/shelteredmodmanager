using System.Collections.Generic;

namespace ModAPI.Scenarios
{
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
