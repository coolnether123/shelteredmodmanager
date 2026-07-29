namespace ParalivesAPI.Core
{
    public sealed class ParalivesCharacterSnapshot
    {
        public bool Exists { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public string ShortName { get; internal set; }

        public ulong HouseholdGuid { get; internal set; }

        public ulong[] HouseholdCharacterGuids { get; internal set; }

        public bool IsInHousehold { get; internal set; }

        public bool IsInCurrentHousehold { get; internal set; }

        public ulong LifeStageGuid { get; internal set; }

        public string LifeStageDisplayName { get; internal set; }

        public ulong CurrentLotGuid { get; internal set; }

        public bool IsSelected { get; internal set; }

        public int SelectedPlayerIndex { get; internal set; }

        public bool IsAvailableForGameplay { get; internal set; }

        public bool IsDead { get; internal set; }

        public bool IsTakenAway { get; internal set; }

        public bool IsDeadOrTakenAway { get; internal set; }

        public bool IsUnselectable { get; internal set; }

        public bool IsVisualLoaded { get; internal set; }

        public bool IsVisibleInWorld { get; internal set; }

        public bool IsDummy { get; internal set; }

        public bool DoNotLoadVisual { get; internal set; }

        public ulong[] CharacterRequirementsMet { get; internal set; }
    }
}
