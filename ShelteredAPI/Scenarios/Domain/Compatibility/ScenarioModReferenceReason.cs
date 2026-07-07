using ModAPI.Scenarios;
namespace ShelteredAPI.Scenarios.Domain.Compatibility{
    /// <summary>
    /// Reason a scenario references or depends on a mod.
    /// Used by compatibility reports to show actionable dependency explanations.
    /// </summary>
    public enum ScenarioModReferenceReason
    {
        ExplicitDependency = 0,
        InventoryItem = 1,
        RecipeOrContent = 2,
        SpriteOrAsset = 3,
        QuestContent = 4,
        ConditionKind = 5,
        EffectKind = 6,
        TimelineEntry = 7,
        BunkerObject = 8,
        SurvivorTraitOrStat = 9,
        ActorAuthoringComponent = 10,
        UnknownReference = 11
    }
}
