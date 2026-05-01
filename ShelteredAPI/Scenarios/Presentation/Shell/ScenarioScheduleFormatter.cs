namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioScheduleFormatter
    {
        public static string Format(ScenarioScheduleTime time)
        {
            if (time == null)
                return "unscheduled";

            return "day " + time.Day + " " + time.Hour.ToString("D2") + ":" + time.Minute.ToString("D2");
        }
    }
}
