using ModAPI.Scenarios;

using ShelteredAPI.Content;
namespace ShelteredAPI.Scenarios.Domain.Objects{
    /// <summary>
    /// Initial runtime state for an authored scenario object or scene sprite.
    /// </summary>
    public enum ScenarioObjectStartState
    {
        StartsEnabled = 0,
        StartsDisabled = 1,
        StartsHidden = 2,
        StartsLocked = 3,
        AppearsLater = 4,
        RemovedAtStart = 5
    }
}
