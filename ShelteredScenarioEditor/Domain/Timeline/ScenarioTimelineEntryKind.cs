namespace ShelteredScenarioEditor.Domain.Timeline{
    /// <summary>
    /// Category used to group scenario timeline entries in editor and diagnostics views.
    /// </summary>
    internal enum ScenarioTimelineEntryKind
    {
        Bunker = 0,
        Object = 1,
        Survivor = 2,
        Inventory = 3,
        Weather = 4,
        Quest = 5,
        Map = 6,
        CustomModded = 7,
        Story = 8,
        WorldEvent = 9,
        Journal = 10
    }

    /// <summary>
    /// Runtime or validation status for a scenario timeline entry.
    /// </summary>
    internal enum ScenarioTimelineEntryStatus
    {
        Pending = 0,
        Fired = 1,
        Blocked = 2,
        Failed = 3,
        Warning = 4
    }
}
