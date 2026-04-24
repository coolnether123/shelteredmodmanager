namespace ModAPI.Scenarios
{
    public enum ScenarioExecutedActionStatus
    {
        Succeeded = 0,
        Failed = 1,
        Blocked = 2,
        Skipped = 3
    }

    public class ScenarioExecutedActionRecord
    {
        public string ScenarioId { get; set; }
        public string ScenarioVersion { get; set; }
        public string RuntimeBindingId { get; set; }
        public string ActionKey { get; set; }
        public string ActionType { get; set; }
        public int FiredDay { get; set; }
        public int FiredHour { get; set; }
        public int FiredMinute { get; set; }
        public ScenarioExecutedActionStatus Status { get; set; }
        public string Message { get; set; }
    }
}
