namespace ModAPI.Scenarios
{
    public enum ScenarioTimelineEntryKind
    {
        Bunker = 0,
        Object = 1,
        Survivor = 2,
        Inventory = 3,
        Weather = 4,
        Quest = 5,
        Map = 6,
        CustomModded = 7
    }

    public enum ScenarioTimelineEntryStatus
    {
        Pending = 0,
        Fired = 1,
        Blocked = 2,
        Failed = 3,
        Warning = 4
    }
}
