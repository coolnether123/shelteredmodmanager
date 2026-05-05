using ModAPI.Scenarios;
namespace ShelteredAPI.Scenarios.Domain.Scheduling{
    /// <summary>
    /// Scenario-local time expressed as day, hour, and minute.
    /// Defaults to day 1 at 08:00 to match the typical start-of-run authoring baseline.
    /// </summary>
    public class ScenarioScheduleTime
    {
        public ScenarioScheduleTime()
        {
            Day = 1;
            Hour = 8;
            Minute = 0;
        }

        public int Day { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
    }
}
