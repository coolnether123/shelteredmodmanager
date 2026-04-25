namespace ModAPI.Scenarios
{
    /// <summary>
    /// Per-save scenario binding metadata. This is separate from ScenarioDefinition on
    /// purpose: a scenario is reusable data, while a binding only records whether one
    /// save slot is currently governed by that data.
    /// </summary>
    public class ScenarioRuntimeBinding
    {
        public string ScenarioId { get; set; }
        public string VersionApplied { get; set; }
        public bool IsActive { get; set; }
        public bool IsConvertedToNormalSave { get; set; }
        public int DayCreated { get; set; }
        public int? LastEditorSaveTick { get; set; }
    }
}
