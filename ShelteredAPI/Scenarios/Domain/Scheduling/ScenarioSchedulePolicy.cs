using ModAPI.Scenarios;
namespace ShelteredAPI.Scenarios.Domain.Scheduling{
    /// <summary>
    /// Repetition policy for a scheduled scenario action.
    /// Non-repeatable actions fire once; repeatable actions respect cooldown before firing again.
    /// </summary>
    public class ScenarioSchedulePolicy
    {
        public ScenarioSchedulePolicy()
        {
            Repeatable = false;
            CooldownMinutes = 0;
            WindowEndDay = 0;
            Chance = 1f;
            JitterMinutes = 0;
            MaxRuns = 0;
        }

        public bool Repeatable { get; set; }
        public int CooldownMinutes { get; set; }
        public int WindowEndDay { get; set; }
        public float Chance { get; set; }
        public int JitterMinutes { get; set; }
        public int MaxRuns { get; set; }
    }
}
