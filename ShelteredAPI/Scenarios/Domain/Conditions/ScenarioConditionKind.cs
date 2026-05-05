using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    /// <summary>
    /// Built-in condition types understood by the scenario gate evaluator.
    /// Custom mods can add handlers for custom condition behavior.
    /// </summary>
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

    /// <summary>
    /// Evaluation mode for grouped scenario conditions.
    /// </summary>
    public enum ScenarioConditionGroupMode
    {
        All = 0,
        Any = 1
    }
}
