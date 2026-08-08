using ShelteredAPI.Scenarios.Domain.Scheduling;
using ShelteredScenarioEditor.Application.Commands;
namespace ShelteredScenarioEditor.Domain.Timeline{
    /// <summary>
    /// One scheduled or derived entry shown in scenario timeline tools.
    /// </summary>
    internal class ScenarioTimelineEntry
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
        public string SourceKind { get; set; }
        public string SourceCollection { get; set; }
        public int SourceIndex { get; set; }
        public string SourceId { get; set; }
        public string OwnerWindowId { get; set; }
        public string FocusAutomationId { get; set; }
        public ScenarioAuthoringCommand FocusCommand { get; set; }
    }
}
