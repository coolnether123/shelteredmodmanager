using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Public{
    public sealed class ScenarioWorldLaunchRequest
    {
        public string StorageScenarioId { get; set; }
        public SaveEntry StartupSave { get; set; }
        public SaveManager.SaveType SaveType { get; set; }
        public string TargetLabel { get; set; }
        public ScenarioBaseGameMode BaseGameMode { get; set; }
        public ScenarioDefinition Definition { get; set; }
    }

    /// <summary>Authored runtime entity category returned by the identity query.</summary>
    public enum ScenarioRuntimeIdentityKind
    {
        None = 0,
        SceneSpritePlacement = 1,
        ObjectPlacement = 2
    }

    /// <summary>Detached identity for a scenario-owned entity in the current Unity world.</summary>
    public sealed class ScenarioRuntimeIdentity
    {
        public ScenarioRuntimeIdentityKind Kind { get; internal set; }
        public string PlacementId { get; internal set; }
        public string ScenarioObjectId { get; internal set; }
        public string RuntimeBindingKey { get; internal set; }
        public int GridX { get; internal set; }
        public int GridY { get; internal set; }
    }
}
