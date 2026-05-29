namespace ParalivesAPI.Core
{
    public sealed class ParalivesWantChangedEvent
    {
        public ParalivesWantChangeType ChangeType { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public int WantIndex { get; internal set; }

        public ulong WantGuid { get; internal set; }

        public ulong BrainLogicGuid { get; internal set; }

        public ulong CharacterTargetGuid { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public global::AssetCharacterWantStatus Status { get; internal set; }
    }

    public sealed class ParalivesSkillChangedEvent
    {
        public ParalivesSkillChangeType ChangeType { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public int PreviousLevel { get; internal set; }

        public int CurrentLevel { get; internal set; }

        public float PreviousCurrentLevelExperience { get; internal set; }

        public float GrantedExperience { get; internal set; }
    }

    public sealed class ParalivesStatusEffectChangedEvent
    {
        public ParalivesStatusEffectChangeType ChangeType { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public ulong StatusEffectGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public ulong CharacterWhoGaveIt { get; internal set; }
    }

    public sealed class ParalivesNeedChangedEvent
    {
        public ParalivesNeedChangeType ChangeType { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public ulong NeedGuid { get; internal set; }

        public float PreviousValue { get; internal set; }

        public float CurrentValue { get; internal set; }

        public float Amount { get; internal set; }
    }

    public sealed class ParalivesRelationshipChangedEvent
    {
        public ParalivesRelationshipChangeType ChangeType { get; internal set; }

        public ulong SourceCharacterGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public ulong LabelGuid { get; internal set; }

        public int Increment { get; internal set; }

        public int Level { get; internal set; }

        public bool Changed { get; internal set; }
    }

    public sealed class ParalivesMemoryChangedEvent
    {
        public ParalivesMemoryChangeType ChangeType { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public global::Setting.MemoryLogType MemoryLogType { get; internal set; }

        public global::MemoryData Data { get; internal set; }

        public float StartTime { get; internal set; }

        public float EndTime { get; internal set; }

        public bool WasCancelled { get; internal set; }

        public global::Setting.MemoryLogTrigger MemoryLogTrigger { get; internal set; }

        public global::Setting.MemoryLogTriggerWithCancelAndComplete MemoryLogActionTrigger { get; internal set; }

        public bool InHousehold { get; internal set; }
    }

    public sealed class ParalivesGoalChangedEvent
    {
        public ParalivesGoalChangeType ChangeType { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public ulong GoalGuid { get; internal set; }

        public ulong ObjectiveGuid { get; internal set; }

        public ulong RewardGuid { get; internal set; }

        public ulong RequesterGuid { get; internal set; }

        public ulong TargetCharacterGuid { get; internal set; }

        public bool Track { get; internal set; }
    }
}
