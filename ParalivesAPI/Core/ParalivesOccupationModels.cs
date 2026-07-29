using Setting;

namespace ParalivesAPI.Core
{
    public enum ParalivesOccupationKind
    {
        Any = 0,
        Job = 1,
        School = 2,
        RemoteWork = 3,
        Club = 4,
        Apprenticeship = 5,
        Gig = 6,
        Unknown = 99,
        Custom = 100
    }

    public enum ParalivesOccupationUnlockableKind
    {
        Unknown = 0,
        Expertise = 1,
        Extra = 2,
        Instant = 3
    }

    public enum ParalivesOccupationRegistrationStatus
    {
        Success,
        Duplicate,
        SettingsNotReady,
        InvalidDefinition,
        Error
    }

    public sealed class ParalivesOccupationDefinition
    {
        public ParalivesOccupationDefinition()
        {
            StableId = string.Empty;
            ModId = string.Empty;
            DisplayName = string.Empty;
            TranslationKey = string.Empty;
            Kind = ParalivesOccupationKind.Job;
            DomainGuids = new ulong[0];
            AppropriateLifeStageGuids = new ulong[0];
            AutonomyTagGuids = new ulong[0];
            Tasks = new ParalivesOccupationTaskDefinition[0];
            Unlockables = new ParalivesOccupationUnlockableDefinition[0];
            MaxNumberOfExtraSlots = 3;
            RarityWeight = 1;
        }

        public ulong Guid { get; set; }

        public ulong OccupationGuid
        {
            get { return Guid; }
            set { Guid = value; }
        }

        public string StableId { get; set; }

        public string ModId { get; set; }

        public string DisplayName { get; set; }

        public string TranslationKey { get; set; }

        public ParalivesOccupationKind Kind { get; set; }

        public ulong CompanyGuid { get; set; }

        public ulong ProgressionLevelGuid { get; set; }

        public ulong ScheduleGuid { get; set; }

        public ulong[] DomainGuids { get; set; }

        public ulong[] AppropriateLifeStageGuids { get; set; }

        public ulong[] AutonomyTagGuids { get; set; }

        public bool OverridesCompanyRabbitHole { get; set; }

        public bool IsRabbitHole { get; set; }

        public float TravelDurationMinutes { get; set; }

        public int MaxNumberOfExtraSlots { get; set; }

        public int RarityWeight { get; set; }

        public ulong OutfitTypeGuid { get; set; }

        public ulong WorkOutfitGuid { get; set; }

        public bool ForcedToAppearEveryday { get; set; }

        public ParalivesOccupationTaskDefinition[] Tasks { get; set; }

        public ParalivesOccupationUnlockableDefinition[] Unlockables { get; set; }
    }

    public sealed class ParalivesOccupationRegistrationResult
    {
        public ParalivesOccupationRegistrationStatus Status { get; internal set; }

        public bool Succeeded { get; internal set; }

        public bool Accepted { get; internal set; }

        public bool Applied { get; internal set; }

        public bool SettingsReady { get; internal set; }

        public bool IsDuplicate { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public string Message { get; internal set; }

        public int RegisteredCount { get; internal set; }

        public int AppliedCount { get; internal set; }

        public int DuplicateCount { get; internal set; }

        public int InvalidCount { get; internal set; }

        public int ErrorCount { get; internal set; }
    }

    public sealed class ParalivesOccupationEnrollmentOptions
    {
        public ParalivesOccupationEnrollmentOptions()
        {
            DaysOptionIndex = -1;
            HoursOptionIndex = -1;
            StartingRank = 1;
        }

        public int StartingRank { get; set; }

        public bool AllowDuplicateActiveOccupation { get; set; }

        public int DaysOptionIndex { get; set; }

        public int HoursOptionIndex { get; set; }

        public bool HasSelectedDays { get; set; }

        public ScheduleDaysOfWeek SelectedDays { get; set; }

        public bool HasSelectedHours { get; set; }

        public ulong SelectedHoursGuid { get; set; }

        public float SelectedStartTime { get; set; }

        public float SelectedDuration { get; set; }

        public bool SelectedHoursAreDefault { get; set; }
    }

    public sealed class ParalivesOccupationEnrollmentResult
    {
        public ParalivesOccupationEnrollmentResult()
        {
            Code = string.Empty;
            Message = string.Empty;
            OccupationIndex = -1;
        }

        public bool Succeeded { get; set; }

        public string Code { get; set; }

        public string Message { get; set; }

        public ulong CharacterGuid { get; set; }

        public ulong OccupationGuid { get; set; }

        public int OccupationIndex { get; set; }

        public ParalivesOccupationSnapshot Snapshot { get; set; }
    }

    public sealed class ParalivesOccupationOperationResult
    {
        public ParalivesOccupationOperationResult()
        {
            Message = string.Empty;
            OccupationIndex = -1;
        }

        public bool Succeeded { get; set; }

        public string Message { get; set; }

        public ulong CharacterGuid { get; set; }

        public int OccupationIndex { get; set; }

        public ulong OccupationGuid { get; set; }

        public ParalivesOccupationRestoreToken RestoreToken { get; set; }
    }

    public sealed class ParalivesOccupationUnlockableSnapshot
    {
        public ulong CharacterGuid { get; set; }

        public int OccupationIndex { get; set; }

        public ulong OccupationGuid { get; set; }

        public ulong UnlockableGuid { get; set; }

        public string DisplayName { get; set; }

        public string TranslationKey { get; set; }

        public bool IsKnownUnlockable { get; set; }

        public bool IsEnabled { get; set; }

        public int Type { get; set; }

        public bool IsExpertise { get; set; }

        public bool IsExtra { get; set; }

        public bool IsInstant { get; set; }

        public bool IsAttachedToOccupation { get; set; }

        public bool IsAcquired { get; set; }

        public bool IsPendingUpgradeOption { get; set; }

        public int Level { get; set; }

        public int StartingLevel { get; set; }

        public int MaxLevel { get; set; }

        public float TimeOfLastLeveledUp { get; set; }

        public float TimeAdded { get; set; }

        public int UnlockableWasAdded { get; set; }

        public int Value { get; set; }

        public int ExtraSlotIndex { get; set; }

        public int PendingUpgradeSlot { get; set; }

        public bool IsAutoAddedWhenEnrolled { get; set; }
    }

    public sealed class ParalivesOccupationUnlockableDefinition
    {
        public ParalivesOccupationUnlockableDefinition()
        {
            DisplayName = string.Empty;
            TranslationKey = string.Empty;
            DescriptionKey = string.Empty;
            StableId = string.Empty;
            Kind = ParalivesOccupationUnlockableKind.Unknown;
            RequiredOccupationKind = ParalivesOccupationKind.Any;
        }

        public ulong UnlockableGuid { get; set; }

        public ulong OccupationGuid { get; set; }

        public string StableId { get; set; }

        public string DisplayName { get; set; }

        public string TranslationKey { get; set; }

        public string DescriptionKey { get; set; }

        public ulong DescriptionGuid { get; set; }

        public ParalivesOccupationUnlockableKind Kind { get; set; }

        public ParalivesOccupationKind RequiredOccupationKind { get; set; }

        public bool Enabled { get; set; }

        public int StartingValue { get; set; }

        public int MaxLevel { get; set; }

        public int MoneyBonus { get; set; }

        public bool IsAutoAddedWhenEnrolled { get; set; }
    }

    public sealed class ParalivesOccupationSkillLevelSnapshot
    {
        public ulong SkillGuid { get; set; }

        public int Level { get; set; }
    }

    public sealed class ParalivesOccupationSnapshot
    {
        public ParalivesOccupationSnapshot()
        {
            DisplayName = string.Empty;
            OccupationIndex = -1;
            CurrentOccupationIndex = -1;
            OccupationIndexToGoTo = -1;
            CurrentlyAffectedOccupationIndex = -1;
            Schedule = new ParalivesAssignedOccupationScheduleSnapshot();
            StartingUsefulSkills = new ParalivesOccupationSkillLevelSnapshot[0];
            Extras = new ParalivesOccupationUnlockableSnapshot[0];
            Expertises = new ParalivesOccupationUnlockableSnapshot[0];
            PendingRandomizedUpgradeGuids = new ulong[0];
            TimestampsOfStrikes = new float[0];
            NextSkippedDays = new int[0];
        }

        public bool Exists { get; set; }

        public ulong CharacterGuid { get; set; }

        public int OccupationIndex { get; set; }

        public ulong OccupationGuid { get; set; }

        public string DisplayName { get; set; }

        public bool IsKnownOccupation { get; set; }

        public ParalivesOccupationKind Kind { get; set; }

        public int NativeKindValue { get; set; }

        public bool IsActive { get; set; }

        public int Level { get; set; }

        public float StartTimestamp { get; set; }

        public float EndTimestamp { get; set; }

        public ParalivesAssignedOccupationScheduleSnapshot Schedule { get; set; }

        public float TimeLastChangedSchedule { get; set; }

        public ParalivesOccupationSkillLevelSnapshot[] StartingUsefulSkills { get; set; }

        public int PendingUpgradeCount { get; set; }

        public int UpgradesCompletedCount { get; set; }

        public int CurrentPendingUpgradeLastGeneratedAtCount { get; set; }

        public ParalivesOccupationUnlockableSnapshot[] Extras { get; set; }

        public ParalivesOccupationUnlockableSnapshot[] Expertises { get; set; }

        public ulong[] PendingRandomizedUpgradeGuids { get; set; }

        public int JobPerformance { get; set; }

        public float[] TimestampsOfStrikes { get; set; }

        public int NumberOfVacationDaysAvailable { get; set; }

        public int[] NextSkippedDays { get; set; }

        public int LastDayUpdated { get; set; }

        public bool HasEndedWorkedDay { get; set; }

        public int NumberOfStrikes { get; set; }

        public int MaxExtraSlots { get; set; }

        public int AverageGrade { get; set; }

        public int CurrentOccupationIndex { get; set; }

        public int OccupationIndexToGoTo { get; set; }

        public int CurrentlyAffectedOccupationIndex { get; set; }

        public float TimeLeftCurrentlyAffectedOccupation { get; set; }

        public bool WasCurrentOccupation { get; set; }

        public bool WasOccupationIndexToGoTo { get; set; }

        public bool WasCurrentlyAffectedOccupation { get; set; }

        public bool IsInScheduledDay { get; set; }

        public bool IsInScheduledHours { get; set; }

        public bool ShouldBeWorkingNow { get; set; }
    }

    public sealed class ParalivesOccupationRestoreToken
    {
        public ParalivesOccupationRestoreToken()
        {
            PreviousOccupationIndex = -1;
            ReplacementOccupationIndex = -1;
        }

        public ulong CharacterGuid { get; set; }

        public ulong PreviousOccupationGuid { get; set; }

        public int PreviousOccupationIndex { get; set; }

        public ParalivesOccupationKind PreviousOccupationKind { get; set; }

        public bool WasActive { get; set; }

        public float CapturedAtTimestamp { get; set; }

        public ulong ReplacedByOccupationGuid { get; set; }

        public int ReplacementOccupationIndex { get; set; }

        public ParalivesOccupationSnapshot Snapshot { get; set; }
    }
}
