namespace ShelteredAPI.Scenarios
{
    internal sealed class ScenarioTimelineViewModel
    {
        public ScenarioTimelineDayViewModel[] Days { get; set; }
    }

    internal sealed class ScenarioTimelineDayViewModel
    {
        public int Day { get; set; }
        public int Count { get; set; }
        public string Badge { get; set; }
        public string Categories { get; set; }
        public ScenarioTimelineEntryViewModel[] Entries { get; set; }
    }

    internal sealed class ScenarioTimelineEntryViewModel
    {
        public string Id { get; set; }
        public string Time { get; set; }
        public string Title { get; set; }
        public string Type { get; set; }
        public string OwnerStage { get; set; }
        public string Status { get; set; }
        public string Warning { get; set; }
        public string ActionId { get; set; }
    }
}
