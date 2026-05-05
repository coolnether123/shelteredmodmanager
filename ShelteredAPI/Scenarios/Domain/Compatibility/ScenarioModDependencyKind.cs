using ModAPI.Scenarios;
namespace ShelteredAPI.Scenarios.Domain.Compatibility{
    /// <summary>
    /// Relationship between a scenario and another mod.
    /// </summary>
    public enum ScenarioModDependencyKind
    {
        Required = 0,
        Optional = 1,
        Incompatible = 2
    }
}
