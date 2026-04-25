namespace ModAPI.Scenarios
{
    public enum ScenarioConditionKind
    {
        TimeReached = 0,
        ItemQuantityAvailable = 1,
        TechnologyUnlocked = 2,
        QuestActive = 3,
        QuestCompleted = 4,
        QuestFailed = 5,
        SurvivorPresent = 6,
        SurvivorStatCheck = 7,
        SurvivorTraitCheck = 8,
        BunkerExpansionUnlocked = 9,
        CustomTrigger = 10,
        ScenarioFlagSet = 11
    }

    public enum ScenarioConditionGroupMode
    {
        All = 0,
        Any = 1
    }
}
