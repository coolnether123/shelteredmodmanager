using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios.Domain.Validation
{
    /// <summary>Detached story-flow diagnostic returned by the scenario authoring facade.</summary>
    public sealed class ScenarioStoryFlowIssue
    {
        public ScenarioIssueSeverity Severity { get; set; }
        public string Code { get; set; }
        public string Message { get; set; }
        public int StageIndex { get; set; }
        public string StageId { get; set; }
        public int IntercomIndex { get; set; }
    }
}
