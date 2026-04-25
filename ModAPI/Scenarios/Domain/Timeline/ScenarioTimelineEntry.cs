namespace ModAPI.Scenarios
{
    public class ScenarioTimelineEntry
    {
        public ScenarioTimelineEntry()
        {
            When = new ScenarioScheduleTime();
            Status = ScenarioTimelineEntryStatus.Pending;
        }

        public string Id { get; set; }
        public ScenarioTimelineEntryKind Kind { get; set; }
        public ScenarioScheduleTime When { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string OwnerStage { get; set; }
        public string OwnerId { get; set; }
        public string TargetId { get; set; }
        public ScenarioTimelineEntryStatus Status { get; set; }
        public string Warning { get; set; }
        public string Source { get; set; }
    }
}
