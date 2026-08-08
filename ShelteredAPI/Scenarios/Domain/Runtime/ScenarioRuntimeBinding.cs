using ModAPI.Scenarios;

using ShelteredAPI.Content;
using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Domain.Runtime{
    /// <summary>
    /// Per-save scenario binding metadata. This is separate from ScenarioDefinition on
    /// purpose: a scenario is reusable data, while a binding only records whether one
    /// save slot is currently governed by that data.
    /// </summary>
    internal class ScenarioRuntimeBinding
    {
        public string ScenarioId { get; set; }
        public string VersionApplied { get; set; }
        public bool IsActive { get; set; }
        public bool IsConvertedToNormalSave { get; set; }
        public int DayCreated { get; set; }
        public string RunId { get; set; }
        public bool IsPreview { get; set; }
        public int? ScenarioQuestInstanceId { get; set; }
    }
}
