using System.Collections.Generic;

namespace ShelteredScenarioEditor.Domain.Timeline{
    /// <summary>
    /// Timeline bucket for all scenario entries scheduled on one day.
    /// </summary>
    internal class ScenarioTimelineDay
    {
        public ScenarioTimelineDay()
        {
            Entries = new List<ScenarioTimelineEntry>();
        }

        public int Day { get; set; }
        public List<ScenarioTimelineEntry> Entries { get; private set; }
    }
}
