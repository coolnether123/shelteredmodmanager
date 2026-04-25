namespace ModAPI.Scenarios
{
    public class ScenarioSchedulePolicy
    {
        public ScenarioSchedulePolicy()
        {
            Repeatable = false;
            CooldownMinutes = 0;
        }

        public bool Repeatable { get; set; }
        public int CooldownMinutes { get; set; }
    }
}
