namespace ParalivesAPI.Core
{
    public sealed class ParalivesActionContentSnapshot
    {
        public bool Exists { get; internal set; }

        public ulong ActionGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public int Type { get; internal set; }

        public int EndCondition { get; internal set; }

        public bool IsCancellable { get; internal set; }

        public bool HasLocomotion { get; internal set; }

        public ulong AnimationGuid { get; internal set; }

        public ulong ItemFinderRuleGuid { get; internal set; }

        public ulong SittingItemFinderRuleGuid { get; internal set; }

        public int RequirementCount { get; internal set; }

        public int OutcomeCount { get; internal set; }
    }

    public sealed class ParalivesInteractionContentSnapshot
    {
        public bool Exists { get; internal set; }

        public ulong InteractionGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public string TranslationKey { get; internal set; }

        public ulong ActionGuid { get; internal set; }

        public ulong CharacterRequirementGuid { get; internal set; }

        public ulong OtherCharacterRequirementGuid { get; internal set; }

        public ulong SocialGroupRuleGuid { get; internal set; }

        public bool IsInstant { get; internal set; }

        public bool IsPlayerCancellable { get; internal set; }

        public bool ForceInteractionDoneSolo { get; internal set; }

        public bool InjectToAllEvenIfTargeted { get; internal set; }

        public int StartingRequirementChecks { get; internal set; }

        public int RunningRequirementChecks { get; internal set; }

        public int UsabilityRuleCount { get; internal set; }
    }

    public sealed class ParalivesInteractionGroupChildSnapshot
    {
        public ulong ItemGuid { get; internal set; }

        public int Type { get; internal set; }

        public ulong InteractionGuid { get; internal set; }

        public ulong GroupGuid { get; internal set; }

        public bool IsNestedNameDifferentThanInteractionName { get; internal set; }

        public string DisplayNameOfNestedInteraction { get; internal set; }

        public string NestedNameTranslationKey { get; internal set; }
    }

    public sealed class ParalivesInteractionGroupContentSnapshot
    {
        public bool Exists { get; internal set; }

        public ulong GroupGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public string TranslationKey { get; internal set; }

        public bool DisplayNameIsSkinnable { get; internal set; }

        public bool DisplayNameIsImpostorLotType { get; internal set; }

        public bool ItemMouseOverHighlight { get; internal set; }

        public ParalivesInteractionGroupChildSnapshot[] Children { get; internal set; }
    }

    public sealed class ParalivesSkillContentSnapshot
    {
        public bool Exists { get; internal set; }

        public ulong SkillGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public string TranslationKey { get; internal set; }

        public bool Enabled { get; internal set; }

        public int MaxLevel { get; internal set; }

        public ulong ParentKnowledgeSkillGuid { get; internal set; }

        public ulong RestrictedToRequirementGuid { get; internal set; }
    }

    public sealed class ParalivesOccupationContentSnapshot
    {
        public bool Exists { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public string TranslationKey { get; internal set; }

        public int Type { get; internal set; }

        public ulong CompanyGuid { get; internal set; }

        public ulong ProgressionLevelGuid { get; internal set; }

        public ulong ScheduleGuid { get; internal set; }

        public ulong[] DomainGuids { get; internal set; }

        public ulong[] AppropriateLifeStageGuids { get; internal set; }

        public bool IsRabbitHole { get; internal set; }

        public bool OverridesCompanyRabbitHole { get; internal set; }

        public float TravelDuration { get; internal set; }

        public int MaxNumberOfExtraSlots { get; internal set; }
    }
}
