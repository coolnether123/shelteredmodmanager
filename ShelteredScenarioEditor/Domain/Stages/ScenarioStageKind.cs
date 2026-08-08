namespace ShelteredScenarioEditor.Domain.Stages{
    /// <summary>
    /// Authoring workflow stage used by scenario editor and validation UI.
    /// These values organize editor surfaces; they are not vanilla ScenarioDef stages.
    /// </summary>
    internal enum ScenarioStageKind
    {
        None = 0,
        Bunker = 1,
        BunkerBackground = 2,
        BunkerSurface = 3,
        BunkerInside = 4,
        InventoryStorage = 5,
        People = 6,
        Events = 7,
        Quests = 8,
        Map = 9,
        Test = 10,
        Publish = 11,
        Assets = 12
    }
}
