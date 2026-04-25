namespace ModAPI.Scenarios
{
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
        UnknownReference = 10
    }
}
