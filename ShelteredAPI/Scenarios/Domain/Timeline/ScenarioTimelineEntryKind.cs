using ModAPI.Scenarios;

using ShelteredAPI.Content;
namespace ShelteredAPI.Scenarios.Domain.Timeline{
    /// <summary>
    /// Category used to group scenario timeline entries in editor and diagnostics views.
    /// </summary>
    public enum ScenarioTimelineEntryKind
    {
        Bunker = 0,
        Object = 1,
        Survivor = 2,
        Inventory = 3,
        Weather = 4,
        Quest = 5,
        Map = 6,
        CustomModded = 7,
        Story = 8
    }

    /// <summary>
    /// Runtime or validation status for a scenario timeline entry.
    /// </summary>
    public enum ScenarioTimelineEntryStatus
    {
        Pending = 0,
        Fired = 1,
        Blocked = 2,
        Failed = 3,
        Warning = 4
    }
}
