namespace ParalivesAPI.Core
{
    public enum ParalivesWantChangeType
    {
        Added,
        Offered,
        StatusChanged
    }

    public enum ParalivesSkillChangeType
    {
        LevelSet,
        BurstExperience,
        OverTimeExperience
    }

    public enum ParalivesStatusEffectChangeType
    {
        Added,
        Removed
    }

    public enum ParalivesNeedChangeType
    {
        SetValue,
        ChangedByValue,
        Relieved,
        ForceRelieved
    }

    public enum ParalivesRelationshipChangeType
    {
        LabelUnlocked,
        LabelRemoved,
        LabelLevelChanged
    }

    public enum ParalivesMemoryChangeType
    {
        Written,
        Cancelled,
        BrainLogicExecuted,
        BrainLogicActionExecuted
    }

    public enum ParalivesGoalChangeType
    {
        Added,
        Tracked,
        RewardClaimed,
        WantObjectiveCompleted,
        Cancelled,
        RequestTurnedIn
    }
}
