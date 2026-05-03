namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Stable runtime facade for scenario trigger interactions.
    /// </summary>
    public static class ShelteredScenarioRuntime
    {
        public static bool FireTrigger(string triggerId)
        {
            return ScenarioTriggerRuntime.Fire(triggerId);
        }

        public static bool FireTrigger(string triggerId, string source, out string message)
        {
            return ScenarioTriggerRuntime.Fire(triggerId, source, out message);
        }
    }
}
