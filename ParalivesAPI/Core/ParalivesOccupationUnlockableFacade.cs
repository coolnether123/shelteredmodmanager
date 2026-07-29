using System.Collections.Generic;
using ParalivesAPI.Stable;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesOccupationUnlockableReadResult
    {
        public bool Succeeded { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public int PendingUpgradeCount { get; internal set; }

        public int MaxExtraSlots { get; internal set; }

        public string Message { get; internal set; }

        public ParalivesOccupationUnlockableSnapshot[] Unlockables { get; internal set; }
    }

    public sealed class ParalivesOccupationUnlockableMutationResult
    {
        public bool Succeeded { get; internal set; }

        public bool Changed { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public ulong UnlockableGuid { get; internal set; }

        public int RequestedLevel { get; internal set; }

        public int PreviousLevel { get; internal set; }

        public int CurrentLevel { get; internal set; }

        public int PreviousPendingUpgradeCount { get; internal set; }

        public int CurrentPendingUpgradeCount { get; internal set; }

        public string Message { get; internal set; }
    }

    public sealed class ParalivesOccupationUnlockableFacade : IParalivesOccupationUnlockables
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesOccupationFacade _occupations;

        internal ParalivesOccupationUnlockableFacade(
            ParalivesCharacterFacade characters,
            ParalivesOccupationFacade occupations)
        {
            _characters = characters;
            _occupations = occupations;
        }

        public ParalivesOccupationUnlockableReadResult ReadUnlockables(ulong characterGuid, int occupationIndex)
        {
            global::AssetCharacter character;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            ParalivesOccupationUnlockableReadResult failure;
            if (!TryResolveReadContext(characterGuid, occupationIndex, out character, out occupationData, out occupation, out failure))
                return failure;

            List<ParalivesOccupationUnlockableSnapshot> snapshots = new List<ParalivesOccupationUnlockableSnapshot>();
            AddExpertiseSnapshots(character, occupationIndex, occupationData, occupation, snapshots);
            AddExtraSnapshots(character, occupationIndex, occupationData, occupation, snapshots);

            return CreateReadSuccess(character, occupationIndex, occupationData, snapshots.ToArray(), string.Empty);
        }

        public ParalivesOccupationUnlockableReadResult ReadExpertises(ulong characterGuid, int occupationIndex)
        {
            global::AssetCharacter character;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            ParalivesOccupationUnlockableReadResult failure;
            if (!TryResolveReadContext(characterGuid, occupationIndex, out character, out occupationData, out occupation, out failure))
                return failure;

            List<ParalivesOccupationUnlockableSnapshot> snapshots = new List<ParalivesOccupationUnlockableSnapshot>();
            AddExpertiseSnapshots(character, occupationIndex, occupationData, occupation, snapshots);
            return CreateReadSuccess(character, occupationIndex, occupationData, snapshots.ToArray(), string.Empty);
        }

        public ParalivesOccupationUnlockableReadResult ReadExtras(ulong characterGuid, int occupationIndex)
        {
            global::AssetCharacter character;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            ParalivesOccupationUnlockableReadResult failure;
            if (!TryResolveReadContext(characterGuid, occupationIndex, out character, out occupationData, out occupation, out failure))
                return failure;

            List<ParalivesOccupationUnlockableSnapshot> snapshots = new List<ParalivesOccupationUnlockableSnapshot>();
            AddExtraSnapshots(character, occupationIndex, occupationData, occupation, snapshots);
            return CreateReadSuccess(character, occupationIndex, occupationData, snapshots.ToArray(), string.Empty);
        }

        public ParalivesOccupationUnlockableReadResult ReadPendingUpgrades(ulong characterGuid, int occupationIndex)
        {
            global::AssetCharacter character;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            ParalivesOccupationUnlockableReadResult failure;
            if (!TryResolveReadContext(characterGuid, occupationIndex, out character, out occupationData, out occupation, out failure))
                return failure;

            List<ParalivesOccupationUnlockableSnapshot> snapshots = new List<ParalivesOccupationUnlockableSnapshot>();
            if (occupationData.PendingRandomizedUpgrades != null)
            {
                for (int i = 0; i < occupationData.PendingRandomizedUpgrades.Count; i++)
                {
                    ulong unlockableGuid = occupationData.PendingRandomizedUpgrades[i];
                    if (unlockableGuid == 0UL)
                        continue;

                    snapshots.Add(CreateSnapshot(
                        character,
                        occupationIndex,
                        occupationData,
                        occupation,
                        unlockableGuid,
                        null,
                        -1,
                        i,
                        true));
                }
            }

            string message = string.Empty;
            if (occupationData.PendingUpgradeCount > 0 && snapshots.Count == 0)
                message = "Pending upgrade options have not been generated by the native occupation manager yet.";

            return CreateReadSuccess(character, occupationIndex, occupationData, snapshots.ToArray(), message);
        }

        public ParalivesOccupationUnlockableMutationResult SetExpertiseLevel(
            ulong characterGuid,
            int occupationIndex,
            ulong unlockableGuid,
            int level)
        {
            global::AssetCharacter character;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            ParalivesOccupationUnlockableMutationResult failure;
            if (!TryResolveMutationContext(characterGuid, occupationIndex, unlockableGuid, out character, out occupationData, out occupation, out failure))
                return failure;

            OccupationUnlockable unlockable;
            if (!TryGetTypedUnlockable(unlockableGuid, OccupationUnlockableTypes.Expertise, out unlockable, out failure))
                return WithContext(failure, character, occupationIndex, occupationData, unlockableGuid);

            PossibleUnlockable possible;
            if (!TryFindPossibleUnlockable(occupation, unlockableGuid, out possible))
                return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "Unlockable is not attached to this occupation.");

            int clampedLevel = level < 1 ? 1 : level;
            if (possible.MaxLevel > 0 && clampedLevel > possible.MaxLevel)
                clampedLevel = possible.MaxLevel;

            int previousLevel = GetUnlockableLevel(character.Data.OccupationExpertises, unlockableGuid);
            bool succeeded = _occupations.SetExpertiseLevel(character, unlockableGuid, clampedLevel);
            int currentLevel = GetUnlockableLevel(character.Data.OccupationExpertises, unlockableGuid);

            return new ParalivesOccupationUnlockableMutationResult
            {
                Succeeded = succeeded,
                Changed = previousLevel != currentLevel,
                CharacterGuid = character.GUID,
                OccupationIndex = occupationIndex,
                OccupationGuid = occupationData.Occupation,
                UnlockableGuid = unlockableGuid,
                RequestedLevel = level,
                PreviousLevel = previousLevel,
                CurrentLevel = currentLevel,
                PreviousPendingUpgradeCount = occupationData.PendingUpgradeCount,
                CurrentPendingUpgradeCount = occupationData.PendingUpgradeCount,
                Message = succeeded ? "Expertise level set." : "Unable to set expertise level."
            };
        }

        public ParalivesOccupationUnlockableMutationResult GrantExtra(
            ulong characterGuid,
            int occupationIndex,
            ulong unlockableGuid)
        {
            global::AssetCharacter character;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            ParalivesOccupationUnlockableMutationResult failure;
            if (!TryResolveMutationContext(characterGuid, occupationIndex, unlockableGuid, out character, out occupationData, out occupation, out failure))
                return failure;

            OccupationUnlockable unlockable;
            if (!TryGetTypedUnlockable(unlockableGuid, OccupationUnlockableTypes.Extra, out unlockable, out failure))
                return WithContext(failure, character, occupationIndex, occupationData, unlockableGuid);

            if (HasUnlockable(occupationData.Extras, unlockableGuid))
                return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "Extra is already granted.");

            if (Count(occupationData.Extras) >= _occupations.GetMaxExtraSlots(character, occupationIndex))
                return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "No extra slot is available.");

            int previousLevel = GetUnlockableLevel(occupationData.Extras, unlockableGuid);
            bool succeeded = _occupations.TryGrantExtraUnlockable(character, occupationIndex, unlockableGuid);
            int currentLevel = GetUnlockableLevel(occupationData.Extras, unlockableGuid);

            return new ParalivesOccupationUnlockableMutationResult
            {
                Succeeded = succeeded,
                Changed = previousLevel != currentLevel || (succeeded && previousLevel == 0),
                CharacterGuid = character.GUID,
                OccupationIndex = occupationIndex,
                OccupationGuid = occupationData.Occupation,
                UnlockableGuid = unlockableGuid,
                PreviousLevel = previousLevel,
                CurrentLevel = currentLevel,
                PreviousPendingUpgradeCount = occupationData.PendingUpgradeCount,
                CurrentPendingUpgradeCount = occupationData.PendingUpgradeCount,
                Message = succeeded ? "Extra granted." : "Unable to grant extra."
            };
        }

        public ParalivesOccupationUnlockableMutationResult RemoveExpertise(
            ulong characterGuid,
            int occupationIndex,
            ulong unlockableGuid)
        {
            global::AssetCharacter character;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            ParalivesOccupationUnlockableMutationResult failure;
            if (!TryResolveMutationContext(characterGuid, occupationIndex, unlockableGuid, out character, out occupationData, out occupation, out failure))
                return failure;

            OccupationUnlockable unlockable;
            if (!TryGetTypedUnlockable(unlockableGuid, OccupationUnlockableTypes.Expertise, out unlockable, out failure))
                return WithContext(failure, character, occupationIndex, occupationData, unlockableGuid);

            PossibleUnlockable possible;
            if (!TryFindPossibleUnlockable(occupation, unlockableGuid, out possible))
                return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "Unlockable is not attached to this occupation.");

            int previousLevel = GetUnlockableLevel(character.Data.OccupationExpertises, unlockableGuid);
            if (previousLevel == 0)
                return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "Expertise is not present.");

            bool succeeded = _occupations.RemoveExpertise(character, unlockableGuid);
            int currentLevel = GetUnlockableLevel(character.Data.OccupationExpertises, unlockableGuid);

            return new ParalivesOccupationUnlockableMutationResult
            {
                Succeeded = succeeded,
                Changed = previousLevel != currentLevel,
                CharacterGuid = character.GUID,
                OccupationIndex = occupationIndex,
                OccupationGuid = occupationData.Occupation,
                UnlockableGuid = unlockableGuid,
                PreviousLevel = previousLevel,
                CurrentLevel = currentLevel,
                PreviousPendingUpgradeCount = occupationData.PendingUpgradeCount,
                CurrentPendingUpgradeCount = occupationData.PendingUpgradeCount,
                Message = succeeded ? "Expertise removed." : "Unable to remove expertise."
            };
        }

        public ParalivesOccupationUnlockableMutationResult ClearPendingUpgrades(ulong characterGuid, int occupationIndex)
        {
            global::AssetCharacter character;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            ParalivesOccupationUnlockableMutationResult failure;
            if (!TryResolveMutationContext(characterGuid, occupationIndex, 0UL, out character, out occupationData, out occupation, out failure))
                return failure;

            int previousCount = occupationData.PendingUpgradeCount;
            int previousOptions = Count(occupationData.PendingRandomizedUpgrades);
            bool changed = _occupations.ClearPendingUpgrades(character, occupationIndex);

            return new ParalivesOccupationUnlockableMutationResult
            {
                Succeeded = true,
                Changed = changed,
                CharacterGuid = character.GUID,
                OccupationIndex = occupationIndex,
                OccupationGuid = occupationData.Occupation,
                PreviousPendingUpgradeCount = previousCount,
                CurrentPendingUpgradeCount = occupationData.PendingUpgradeCount,
                Message = changed || previousOptions > 0 ? "Pending upgrades cleared." : "No pending upgrades to clear."
            };
        }

        public ParalivesOccupationUnlockableMutationResult CompletePendingUpgrade(
            ulong characterGuid,
            int occupationIndex,
            ulong unlockableGuid)
        {
            global::AssetCharacter character;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            ParalivesOccupationUnlockableMutationResult failure;
            if (!TryResolveMutationContext(characterGuid, occupationIndex, unlockableGuid, out character, out occupationData, out occupation, out failure))
                return failure;

            if (global::OccupationsManager.Instance == null)
                return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "Occupation manager is not ready.");

            if (occupationData.PendingUpgradeCount <= 0)
                return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "No pending upgrade is available.");

            OccupationUnlockable unlockable = null;
            if (unlockableGuid == 0UL)
            {
                if (occupation != null && occupation.Type != SchoolJobTypes.Job)
                    return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "The native occupation type does not expose a level upgrade option.");
            }
            else
            {
                if (!TryGetUnlockable(unlockableGuid, out unlockable))
                    return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "Unlockable is not registered.");

                if (!EnsurePendingOptionsGenerated(character, occupationIndex, occupationData, out failure))
                    return WithContext(failure, character, occupationIndex, occupationData, unlockableGuid);

                if (occupationData.PendingRandomizedUpgrades != null
                    && occupationData.PendingRandomizedUpgrades.Count > 0
                    && !Contains(occupationData.PendingRandomizedUpgrades, unlockableGuid))
                {
                    return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "Unlockable is not a pending upgrade option.");
                }

                if (unlockable.Type == OccupationUnlockableTypes.Expertise)
                {
                    PossibleUnlockable possible;
                    if (!TryFindPossibleUnlockable(occupation, unlockableGuid, out possible))
                        return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "Expertise is not attached to this occupation.");
                }
                else if (unlockable.Type == OccupationUnlockableTypes.Extra)
                {
                    if (HasUnlockable(occupationData.Extras, unlockableGuid))
                        return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "Extra is already granted.");

                    if (Count(occupationData.Extras) >= _occupations.GetMaxExtraSlots(character, occupationIndex))
                        return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, "No extra slot is available.");
                }
            }

            int previousPendingCount = occupationData.PendingUpgradeCount;
            int previousLevel = unlockableGuid == 0UL
                ? occupationData.Level
                : GetUnlockableLevel(GetAcquiredList(character, occupationData, unlockable), unlockableGuid);

            try
            {
                global::OccupationsManager.Instance.CompleteUpgrade(character, occupationIndex, unlockable);
            }
            catch (System.Exception ex)
            {
                return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, ex.Message);
            }

            int currentLevel = unlockableGuid == 0UL
                ? occupationData.Level
                : GetUnlockableLevel(GetAcquiredList(character, occupationData, unlockable), unlockableGuid);

            return new ParalivesOccupationUnlockableMutationResult
            {
                Succeeded = true,
                Changed = previousPendingCount != occupationData.PendingUpgradeCount || previousLevel != currentLevel,
                CharacterGuid = character.GUID,
                OccupationIndex = occupationIndex,
                OccupationGuid = occupationData.Occupation,
                UnlockableGuid = unlockableGuid,
                PreviousLevel = previousLevel,
                CurrentLevel = currentLevel,
                PreviousPendingUpgradeCount = previousPendingCount,
                CurrentPendingUpgradeCount = occupationData.PendingUpgradeCount,
                Message = "Pending upgrade completed."
            };
        }

        private bool TryResolveReadContext(
            ulong characterGuid,
            int occupationIndex,
            out global::AssetCharacter character,
            out global::AssetCharacterOccupationData occupationData,
            out Occupation occupation,
            out ParalivesOccupationUnlockableReadResult failure)
        {
            character = null;
            occupationData = null;
            occupation = null;
            failure = null;

            if (!_characters.TryGet(characterGuid, out character))
            {
                failure = CreateReadFailure(characterGuid, occupationIndex, 0UL, "Character not found.");
                return false;
            }

            if (!_occupations.TryGetOccupationData(character, occupationIndex, out occupationData))
            {
                failure = CreateReadFailure(characterGuid, occupationIndex, 0UL, "Occupation index is invalid.");
                return false;
            }

            _occupations.TryGetOccupation(character, occupationIndex, out occupation);
            return true;
        }

        private bool TryResolveMutationContext(
            ulong characterGuid,
            int occupationIndex,
            ulong unlockableGuid,
            out global::AssetCharacter character,
            out global::AssetCharacterOccupationData occupationData,
            out Occupation occupation,
            out ParalivesOccupationUnlockableMutationResult failure)
        {
            character = null;
            occupationData = null;
            occupation = null;
            failure = null;

            if (!_characters.TryGet(characterGuid, out character))
            {
                failure = CreateMutationFailure(characterGuid, occupationIndex, 0UL, unlockableGuid, "Character not found.");
                return false;
            }

            if (!_occupations.TryGetOccupationData(character, occupationIndex, out occupationData))
            {
                failure = CreateMutationFailure(characterGuid, occupationIndex, 0UL, unlockableGuid, "Occupation index is invalid.");
                return false;
            }

            _occupations.TryGetOccupation(character, occupationIndex, out occupation);
            return true;
        }

        private static void AddExpertiseSnapshots(
            global::AssetCharacter character,
            int occupationIndex,
            global::AssetCharacterOccupationData occupationData,
            Occupation occupation,
            List<ParalivesOccupationUnlockableSnapshot> snapshots)
        {
            if (occupation == null || occupation.Unlockables == null)
                return;

            for (int i = 0; i < occupation.Unlockables.Length; i++)
            {
                PossibleUnlockable possible = occupation.Unlockables[i];
                if (possible == null || possible.Unlockable == 0UL)
                    continue;

                OccupationUnlockable unlockable;
                if (!TryGetUnlockable(possible.Unlockable, out unlockable) || unlockable.Type != OccupationUnlockableTypes.Expertise)
                    continue;

                global::AssetCharacterOccupationUnlockableData data = FindUnlockable(
                    character == null || character.Data == null ? null : character.Data.OccupationExpertises,
                    possible.Unlockable);

                snapshots.Add(CreateSnapshot(
                    character,
                    occupationIndex,
                    occupationData,
                    occupation,
                    possible.Unlockable,
                    data,
                    -1,
                    -1,
                    false));
            }
        }

        private static void AddExtraSnapshots(
            global::AssetCharacter character,
            int occupationIndex,
            global::AssetCharacterOccupationData occupationData,
            Occupation occupation,
            List<ParalivesOccupationUnlockableSnapshot> snapshots)
        {
            if (occupationData == null || occupationData.Extras == null)
                return;

            for (int i = 0; i < occupationData.Extras.Count; i++)
            {
                global::AssetCharacterOccupationUnlockableData data = occupationData.Extras[i];
                if (data == null)
                    continue;

                snapshots.Add(CreateSnapshot(
                    character,
                    occupationIndex,
                    occupationData,
                    occupation,
                    data.OccupationUnlockable,
                    data,
                    i,
                    -1,
                    false));
            }
        }

        private static ParalivesOccupationUnlockableSnapshot CreateSnapshot(
            global::AssetCharacter character,
            int occupationIndex,
            global::AssetCharacterOccupationData occupationData,
            Occupation occupation,
            ulong unlockableGuid,
            global::AssetCharacterOccupationUnlockableData data,
            int extraSlotIndex,
            int pendingUpgradeSlot,
            bool isPendingUpgrade)
        {
            OccupationUnlockable unlockable;
            TryGetUnlockable(unlockableGuid, out unlockable);

            PossibleUnlockable possible;
            bool attached = TryFindPossibleUnlockable(occupation, unlockableGuid, out possible);

            return new ParalivesOccupationUnlockableSnapshot
            {
                CharacterGuid = character == null ? 0UL : character.GUID,
                OccupationIndex = occupationIndex,
                OccupationGuid = occupationData == null ? 0UL : occupationData.Occupation,
                UnlockableGuid = unlockableGuid,
                DisplayName = unlockable == null ? string.Empty : (unlockable.DisplayName ?? string.Empty),
                TranslationKey = unlockable == null ? string.Empty : "OccupationUnlockableName_" + (unlockable.DisplayName ?? string.Empty),
                IsKnownUnlockable = unlockable != null,
                IsEnabled = unlockable != null && unlockable.Enabled,
                Type = unlockable == null ? -1 : (int)unlockable.Type,
                IsExpertise = unlockable != null && unlockable.Type == OccupationUnlockableTypes.Expertise,
                IsExtra = unlockable != null && unlockable.Type == OccupationUnlockableTypes.Extra,
                IsInstant = unlockable != null && unlockable.Type == OccupationUnlockableTypes.Instant,
                IsAttachedToOccupation = attached,
                IsAcquired = data != null,
                IsPendingUpgradeOption = isPendingUpgrade,
                Level = data == null ? 0 : data.Level,
                StartingLevel = possible == null ? 0 : possible.StartingLevel,
                MaxLevel = possible == null ? 0 : possible.MaxLevel,
                TimeAdded = data == null ? 0f : data.TimeAdded,
                TimeOfLastLeveledUp = data == null ? 0f : data.TimeOfLastLeveledUp,
                UnlockableWasAdded = data == null ? -1 : (int)data.UnlockableWasAdded,
                Value = data == null ? 0 : data.Value,
                ExtraSlotIndex = extraSlotIndex,
                PendingUpgradeSlot = pendingUpgradeSlot,
                IsAutoAddedWhenEnrolled = possible != null && possible.IsAutoAddedWhenEnrolled
            };
        }

        private bool TryGetTypedUnlockable(
            ulong unlockableGuid,
            OccupationUnlockableTypes expectedType,
            out OccupationUnlockable unlockable,
            out ParalivesOccupationUnlockableMutationResult failure)
        {
            unlockable = null;
            failure = null;

            if (unlockableGuid == 0UL)
            {
                failure = CreateMutationFailure(0UL, -1, 0UL, unlockableGuid, "Unlockable GUID is empty.");
                return false;
            }

            if (!TryGetUnlockable(unlockableGuid, out unlockable))
            {
                failure = CreateMutationFailure(0UL, -1, 0UL, unlockableGuid, "Unlockable is not registered.");
                return false;
            }

            if (unlockable.Type != expectedType)
            {
                failure = CreateMutationFailure(0UL, -1, 0UL, unlockableGuid, "Unlockable type is not supported by this operation.");
                return false;
            }

            return true;
        }

        private static bool TryGetUnlockable(ulong unlockableGuid, out OccupationUnlockable unlockable)
        {
            unlockable = null;
            if (unlockableGuid == 0UL)
                return false;

            try
            {
                Occupations occupations = global::Settings.Get<Occupations>();
                if (occupations == null)
                    return false;

                unlockable = occupations.GetOccupationUnlockableByGUID(unlockableGuid);
                return unlockable != null;
            }
            catch
            {
                unlockable = null;
                return false;
            }
        }

        private static bool TryFindPossibleUnlockable(Occupation occupation, ulong unlockableGuid, out PossibleUnlockable possible)
        {
            possible = null;
            if (occupation == null || occupation.Unlockables == null || unlockableGuid == 0UL)
                return false;

            for (int i = 0; i < occupation.Unlockables.Length; i++)
            {
                PossibleUnlockable candidate = occupation.Unlockables[i];
                if (candidate != null && candidate.Unlockable == unlockableGuid)
                {
                    possible = candidate;
                    return true;
                }
            }

            return false;
        }

        private static global::AssetCharacterOccupationUnlockableData FindUnlockable(
            List<global::AssetCharacterOccupationUnlockableData> values,
            ulong unlockableGuid)
        {
            if (values == null || unlockableGuid == 0UL)
                return null;

            for (int i = 0; i < values.Count; i++)
            {
                global::AssetCharacterOccupationUnlockableData data = values[i];
                if (data != null && data.OccupationUnlockable == unlockableGuid)
                    return data;
            }

            return null;
        }

        private static int GetUnlockableLevel(List<global::AssetCharacterOccupationUnlockableData> values, ulong unlockableGuid)
        {
            global::AssetCharacterOccupationUnlockableData data = FindUnlockable(values, unlockableGuid);
            return data == null ? 0 : data.Level;
        }

        private static bool HasUnlockable(List<global::AssetCharacterOccupationUnlockableData> values, ulong unlockableGuid)
        {
            return FindUnlockable(values, unlockableGuid) != null;
        }

        private static List<global::AssetCharacterOccupationUnlockableData> GetAcquiredList(
            global::AssetCharacter character,
            global::AssetCharacterOccupationData occupationData,
            OccupationUnlockable unlockable)
        {
            if (unlockable == null)
                return null;

            if (unlockable.Type == OccupationUnlockableTypes.Expertise)
                return character == null || character.Data == null ? null : character.Data.OccupationExpertises;

            return occupationData == null ? null : occupationData.Extras;
        }

        private bool EnsurePendingOptionsGenerated(
            global::AssetCharacter character,
            int occupationIndex,
            global::AssetCharacterOccupationData occupationData,
            out ParalivesOccupationUnlockableMutationResult failure)
        {
            failure = null;
            if (occupationData.PendingRandomizedUpgrades != null && occupationData.PendingRandomizedUpgrades.Count > 0)
                return true;

            try
            {
                global::OccupationsManager.Instance.GetGeneratedRandomExpertiseAndExtraForOccupationUpgrade(character, occupationIndex);
                return true;
            }
            catch (System.Exception ex)
            {
                failure = CreateMutationFailure(character, occupationIndex, occupationData, 0UL, ex.Message);
                return false;
            }
        }

        private static bool Contains(List<ulong> values, ulong value)
        {
            if (values == null)
                return false;

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                    return true;
            }

            return false;
        }

        private static int Count<T>(List<T> values)
        {
            return values == null ? 0 : values.Count;
        }

        private ParalivesOccupationUnlockableReadResult CreateReadSuccess(
            global::AssetCharacter character,
            int occupationIndex,
            global::AssetCharacterOccupationData occupationData,
            ParalivesOccupationUnlockableSnapshot[] unlockables,
            string message)
        {
            return new ParalivesOccupationUnlockableReadResult
            {
                Succeeded = true,
                CharacterGuid = character == null ? 0UL : character.GUID,
                OccupationIndex = occupationIndex,
                OccupationGuid = occupationData == null ? 0UL : occupationData.Occupation,
                PendingUpgradeCount = occupationData == null ? 0 : occupationData.PendingUpgradeCount,
                MaxExtraSlots = character == null ? 0 : _occupations.GetMaxExtraSlots(character, occupationIndex),
                Message = message ?? string.Empty,
                Unlockables = unlockables ?? new ParalivesOccupationUnlockableSnapshot[0]
            };
        }

        private static ParalivesOccupationUnlockableReadResult CreateReadFailure(
            ulong characterGuid,
            int occupationIndex,
            ulong occupationGuid,
            string message)
        {
            return new ParalivesOccupationUnlockableReadResult
            {
                Succeeded = false,
                CharacterGuid = characterGuid,
                OccupationIndex = occupationIndex,
                OccupationGuid = occupationGuid,
                Message = message ?? string.Empty,
                Unlockables = new ParalivesOccupationUnlockableSnapshot[0]
            };
        }

        private static ParalivesOccupationUnlockableMutationResult CreateMutationFailure(
            global::AssetCharacter character,
            int occupationIndex,
            global::AssetCharacterOccupationData occupationData,
            ulong unlockableGuid,
            string message)
        {
            return CreateMutationFailure(
                character == null ? 0UL : character.GUID,
                occupationIndex,
                occupationData == null ? 0UL : occupationData.Occupation,
                unlockableGuid,
                message);
        }

        private static ParalivesOccupationUnlockableMutationResult CreateMutationFailure(
            ulong characterGuid,
            int occupationIndex,
            ulong occupationGuid,
            ulong unlockableGuid,
            string message)
        {
            return new ParalivesOccupationUnlockableMutationResult
            {
                Succeeded = false,
                Changed = false,
                CharacterGuid = characterGuid,
                OccupationIndex = occupationIndex,
                OccupationGuid = occupationGuid,
                UnlockableGuid = unlockableGuid,
                Message = message ?? string.Empty
            };
        }

        private static ParalivesOccupationUnlockableMutationResult WithContext(
            ParalivesOccupationUnlockableMutationResult result,
            global::AssetCharacter character,
            int occupationIndex,
            global::AssetCharacterOccupationData occupationData,
            ulong unlockableGuid)
        {
            if (result == null)
                return CreateMutationFailure(character, occupationIndex, occupationData, unlockableGuid, string.Empty);

            result.CharacterGuid = character == null ? result.CharacterGuid : character.GUID;
            result.OccupationIndex = occupationIndex;
            result.OccupationGuid = occupationData == null ? result.OccupationGuid : occupationData.Occupation;
            result.UnlockableGuid = unlockableGuid;
            return result;
        }
    }
}
