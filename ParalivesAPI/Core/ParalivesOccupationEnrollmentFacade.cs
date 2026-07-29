using System;
using System.Collections.Generic;
using ParalivesAPI.Stable;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesOccupationEnrollmentFacade : IParalivesOccupationEnrollment
    {
        private readonly ParalivesCharacterFacade _characters;
        private readonly ParalivesOccupationFacade _occupations;

        internal ParalivesOccupationEnrollmentFacade(
            ParalivesCharacterFacade characters,
            ParalivesOccupationFacade occupations)
        {
            _characters = characters;
            _occupations = occupations;
        }

        public bool TryGetActive(
            ulong characterGuid,
            ulong occupationGuid,
            out ParalivesOccupationSnapshot snapshot)
        {
            snapshot = CreateMissingSnapshot(characterGuid, -1);

            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character))
                return false;

            int occupationIndex;
            global::AssetCharacterOccupationData occupationData;
            Occupation occupation;
            if (!_occupations.TryFindActiveOccupation(
                    character,
                    occupationGuid,
                    out occupationIndex,
                    out occupationData,
                    out occupation))
            {
                return false;
            }

            snapshot = BuildSnapshot(character, occupationIndex, occupationData, occupation);
            return true;
        }

        public ParalivesOccupationSnapshot ReadSnapshot(ulong characterGuid, int occupationIndex)
        {
            ParalivesOccupationSnapshot snapshot;
            return TryReadSnapshot(characterGuid, occupationIndex, out snapshot)
                ? snapshot
                : CreateMissingSnapshot(characterGuid, occupationIndex);
        }

        public bool TryReadSnapshot(
            ulong characterGuid,
            int occupationIndex,
            out ParalivesOccupationSnapshot snapshot)
        {
            snapshot = CreateMissingSnapshot(characterGuid, occupationIndex);

            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character))
                return false;

            global::AssetCharacterOccupationData data;
            if (!_occupations.TryGetOccupationData(character, occupationIndex, out data))
                return false;

            Occupation occupation;
            _occupations.TryGetOccupation(character, occupationIndex, out occupation);
            snapshot = BuildSnapshot(character, occupationIndex, data, occupation);
            return true;
        }

        public ParalivesOccupationSnapshot[] ReadActiveSnapshots(ulong characterGuid)
        {
            int[] indexes = _occupations.GetActiveOccupationIndexes(characterGuid);
            List<ParalivesOccupationSnapshot> snapshots = new List<ParalivesOccupationSnapshot>();

            for (int i = 0; i < indexes.Length; i++)
            {
                ParalivesOccupationSnapshot snapshot;
                if (TryReadSnapshot(characterGuid, indexes[i], out snapshot))
                    snapshots.Add(snapshot);
            }

            return snapshots.ToArray();
        }

        public bool TryGetActiveByKind(
            ulong characterGuid,
            ParalivesOccupationKind occupationKind,
            out ParalivesOccupationSnapshot snapshot)
        {
            snapshot = CreateMissingSnapshot(characterGuid, -1);

            global::AssetCharacter character;
            if (!_characters.TryGet(characterGuid, out character)
                || character.Data == null
                || character.Data.Occupations == null)
            {
                return false;
            }

            for (int i = 0; i < character.Data.Occupations.Count; i++)
            {
                global::AssetCharacterOccupationData data = character.Data.Occupations[i];
                if (data == null || !data.IsActive)
                    continue;

                Occupation occupation;
                _occupations.TryGetOccupation(character, i, out occupation);
                if (!MatchesKind(occupation, occupationKind))
                    continue;

                snapshot = BuildSnapshot(character, i, data, occupation);
                return true;
            }

            return false;
        }

        public ParalivesOccupationEnrollmentResult TryEnroll(
            ulong characterGuid,
            ulong occupationGuid)
        {
            return TryEnroll(characterGuid, occupationGuid, new ParalivesOccupationEnrollmentOptions());
        }

        public ParalivesOccupationEnrollmentResult TryEnroll(
            ulong characterGuid,
            ulong occupationGuid,
            int startingRank)
        {
            return TryEnroll(characterGuid, occupationGuid, new ParalivesOccupationEnrollmentOptions
            {
                StartingRank = startingRank
            });
        }

        public ParalivesOccupationEnrollmentResult TryEnroll(
            ulong characterGuid,
            ulong occupationGuid,
            ScheduleDaysOfWeek selectedDays,
            OccupiedHours selectedHours,
            int startingRank)
        {
            ParalivesOccupationEnrollmentOptions options = new ParalivesOccupationEnrollmentOptions
            {
                HasSelectedDays = selectedDays != ScheduleDaysOfWeek.None,
                SelectedDays = selectedDays,
                StartingRank = startingRank
            };

            if (selectedHours != null)
            {
                options.HasSelectedHours = true;
                options.SelectedHoursGuid = selectedHours.GUID;
                options.SelectedStartTime = selectedHours.StartTime;
                options.SelectedDuration = selectedHours.Duration;
                options.SelectedHoursAreDefault = selectedHours.IsDefault;
            }

            return TryEnroll(characterGuid, occupationGuid, options);
        }

        public ParalivesOccupationEnrollmentResult TryEnroll(
            ulong characterGuid,
            ulong occupationGuid,
            ParalivesOccupationEnrollmentOptions options)
        {
            options = options ?? new ParalivesOccupationEnrollmentOptions();

            global::AssetCharacter character;
            if (!TryResolveCharacter(characterGuid, out character, out ParalivesOccupationEnrollmentResult failure))
                return failure;

            Occupation occupation;
            OccupationScheduleType scheduleType;
            string error;
            if (!TryResolveOccupation(occupationGuid, out occupation, out scheduleType, out error))
                return Fail("occupation-not-ready", error, characterGuid, occupationGuid, -1, null);

            int activeIndex;
            global::AssetCharacterOccupationData activeData;
            Occupation activeOccupation;
            if (!options.AllowDuplicateActiveOccupation
                && _occupations.TryFindActiveOccupation(character, occupationGuid, out activeIndex, out activeData, out activeOccupation))
            {
                return Fail(
                    "already-active",
                    "The character already has this occupation active.",
                    characterGuid,
                    occupationGuid,
                    activeIndex,
                    BuildSnapshot(character, activeIndex, activeData, activeOccupation));
            }

            ScheduleDaysOfWeek selectedDays;
            OccupiedHours selectedHours;
            if (!TryResolveScheduleOptions(options, scheduleType, out selectedDays, out selectedHours, out error))
                return Fail("invalid-schedule", error, characterGuid, occupationGuid, -1, null);

            EnsureOccupationLists(character);

            int beforeCount = character.Data.Occupations.Count;
            int startingRank = options.StartingRank <= 0 ? 1 : options.StartingRank;
            bool enrolled = _occupations.Enroll(character, occupationGuid, selectedDays, selectedHours, startingRank);
            if (!enrolled)
            {
                return Fail(
                    "native-enroll-failed",
                    "The native occupation manager did not enroll the character.",
                    characterGuid,
                    occupationGuid,
                    -1,
                    null);
            }

            int newIndex = FindNewActiveOccupationIndex(character, occupationGuid, beforeCount);
            if (newIndex < 0
                && !_occupations.TryFindActiveOccupation(character, occupationGuid, out newIndex, out activeData, out activeOccupation))
            {
                return Fail(
                    "enroll-not-applied",
                    "The native occupation manager returned without creating an active occupation entry.",
                    characterGuid,
                    occupationGuid,
                    -1,
                    null);
            }

            global::AssetCharacterOccupationData newData;
            _occupations.TryGetOccupationData(character, newIndex, out newData);
            _occupations.TryGetOccupation(character, newIndex, out occupation);
            _characters.MarkSaveDirty(character);

            return Success(
                "enrolled",
                "Enrolled.",
                characterGuid,
                occupationGuid,
                newIndex,
                BuildSnapshot(character, newIndex, newData, occupation));
        }

        public ParalivesOccupationEnrollmentResult TryUnenroll(
            ulong characterGuid,
            int occupationIndex)
        {
            return TryUnenroll(characterGuid, occupationIndex, false);
        }

        public ParalivesOccupationEnrollmentResult TryUnenroll(
            ulong characterGuid,
            int occupationIndex,
            bool wasFired)
        {
            global::AssetCharacter character;
            if (!TryResolveCharacter(characterGuid, out character, out ParalivesOccupationEnrollmentResult failure))
                return failure;

            global::AssetCharacterOccupationData data;
            if (!_occupations.TryGetOccupationData(character, occupationIndex, out data))
            {
                return Fail(
                    "invalid-index",
                    "The occupation index is invalid.",
                    characterGuid,
                    0UL,
                    occupationIndex,
                    null);
            }

            Occupation occupation;
            _occupations.TryGetOccupation(character, occupationIndex, out occupation);
            ParalivesOccupationSnapshot before = BuildSnapshot(character, occupationIndex, data, occupation);
            if (!data.IsActive)
            {
                return Fail(
                    "not-active",
                    "The occupation entry is already inactive.",
                    characterGuid,
                    data.Occupation,
                    occupationIndex,
                    before);
            }

            if (!_occupations.Unenroll(character, occupationIndex, wasFired))
            {
                return Fail(
                    "native-unenroll-failed",
                    "The native occupation manager did not unenroll the character.",
                    characterGuid,
                    data.Occupation,
                    occupationIndex,
                    before);
            }

            ClearRuntimeIndexesForOccupation(character, occupationIndex);
            _characters.MarkSaveDirty(character);

            global::AssetCharacterOccupationData afterData;
            _occupations.TryGetOccupationData(character, occupationIndex, out afterData);
            return Success(
                "unenrolled",
                "Unenrolled.",
                characterGuid,
                before.OccupationGuid,
                occupationIndex,
                BuildSnapshot(character, occupationIndex, afterData, occupation));
        }

        public ParalivesOccupationEnrollmentResult TrySwap(
            ulong characterGuid,
            ulong fromOccupationGuid,
            ulong toOccupationGuid,
            out ParalivesOccupationRestoreToken restoreToken)
        {
            return TrySwap(
                characterGuid,
                fromOccupationGuid,
                toOccupationGuid,
                new ParalivesOccupationEnrollmentOptions(),
                out restoreToken);
        }

        public ParalivesOccupationEnrollmentResult TrySwap(
            ulong characterGuid,
            ulong fromOccupationGuid,
            ulong toOccupationGuid,
            ParalivesOccupationEnrollmentOptions toOccupationOptions,
            out ParalivesOccupationRestoreToken restoreToken)
        {
            restoreToken = null;

            if (fromOccupationGuid == 0UL || toOccupationGuid == 0UL)
            {
                return Fail(
                    "invalid-occupation-guid",
                    "Both source and target occupation GUIDs are required.",
                    characterGuid,
                    toOccupationGuid,
                    -1,
                    null);
            }

            if (fromOccupationGuid == toOccupationGuid)
            {
                return Fail(
                    "same-occupation",
                    "Source and target occupation GUIDs are the same.",
                    characterGuid,
                    toOccupationGuid,
                    -1,
                    null);
            }

            global::AssetCharacter character;
            if (!TryResolveCharacter(characterGuid, out character, out ParalivesOccupationEnrollmentResult failure))
                return failure;

            int fromIndex;
            global::AssetCharacterOccupationData fromData;
            Occupation fromOccupation;
            if (!_occupations.TryFindActiveOccupation(
                    character,
                    fromOccupationGuid,
                    out fromIndex,
                    out fromData,
                    out fromOccupation))
            {
                return Fail(
                    "source-not-active",
                    "The source occupation is not active for this character.",
                    characterGuid,
                    fromOccupationGuid,
                    -1,
                    null);
            }

            Occupation targetOccupation;
            OccupationScheduleType targetScheduleType;
            string error;
            if (!TryResolveOccupation(toOccupationGuid, out targetOccupation, out targetScheduleType, out error))
                return Fail("target-not-ready", error, characterGuid, toOccupationGuid, -1, null);

            int existingTargetIndex;
            global::AssetCharacterOccupationData existingTargetData;
            Occupation existingTargetOccupation;
            if (_occupations.TryFindActiveOccupation(
                    character,
                    toOccupationGuid,
                    out existingTargetIndex,
                    out existingTargetData,
                    out existingTargetOccupation))
            {
                return Fail(
                    "target-already-active",
                    "The target occupation is already active for this character.",
                    characterGuid,
                    toOccupationGuid,
                    existingTargetIndex,
                    BuildSnapshot(character, existingTargetIndex, existingTargetData, existingTargetOccupation));
            }

            ParalivesOccupationSnapshot sourceSnapshot = BuildSnapshot(character, fromIndex, fromData, fromOccupation);
            restoreToken = CreateRestoreToken(sourceSnapshot);
            restoreToken.ReplacedByOccupationGuid = toOccupationGuid;

            ParalivesOccupationEnrollmentResult unenroll = TryUnenroll(characterGuid, fromIndex, false);
            if (!unenroll.Succeeded)
                return unenroll;

            ParalivesOccupationEnrollmentResult enroll = TryEnroll(characterGuid, toOccupationGuid, toOccupationOptions);
            if (enroll.Succeeded)
            {
                restoreToken.ReplacementOccupationIndex = enroll.OccupationIndex;
                return Success(
                    "swapped",
                    "Swapped.",
                    characterGuid,
                    toOccupationGuid,
                    enroll.OccupationIndex,
                    enroll.Snapshot);
            }

            ParalivesOccupationEnrollmentResult rollback = TryRestore(characterGuid, restoreToken);
            string message = rollback.Succeeded
                ? "Target enrollment failed; the previous occupation was restored."
                : "Target enrollment failed and previous occupation restore failed: " + rollback.Message;

            return Fail(
                "swap-enroll-failed",
                message,
                characterGuid,
                toOccupationGuid,
                enroll.OccupationIndex,
                rollback.Snapshot);
        }

        public ParalivesOccupationEnrollmentResult TryRestore(
            ulong characterGuid,
            ParalivesOccupationRestoreToken restoreToken)
        {
            if (restoreToken == null || restoreToken.Snapshot == null)
            {
                return Fail(
                    "invalid-restore-token",
                    "A restore token with a snapshot is required.",
                    characterGuid,
                    0UL,
                    -1,
                    null);
            }

            if (restoreToken.CharacterGuid != 0UL && restoreToken.CharacterGuid != characterGuid)
            {
                return Fail(
                    "character-mismatch",
                    "The restore token belongs to a different character.",
                    characterGuid,
                    restoreToken.PreviousOccupationGuid,
                    restoreToken.PreviousOccupationIndex,
                    restoreToken.Snapshot);
            }

            global::AssetCharacter character;
            if (!TryResolveCharacter(characterGuid, out character, out ParalivesOccupationEnrollmentResult failure))
                return failure;

            ParalivesOccupationSnapshot snapshot = restoreToken.Snapshot;
            if (snapshot.OccupationGuid == 0UL)
                snapshot.OccupationGuid = restoreToken.PreviousOccupationGuid;
            if (snapshot.CharacterGuid == 0UL)
                snapshot.CharacterGuid = characterGuid;

            Occupation occupation;
            OccupationScheduleType scheduleType;
            string error;
            if (!TryResolveOccupation(snapshot.OccupationGuid, out occupation, out scheduleType, out error))
                return Fail("occupation-not-ready", error, characterGuid, snapshot.OccupationGuid, snapshot.OccupationIndex, snapshot);

            if (restoreToken.ReplacedByOccupationGuid != 0UL)
            {
                int replacementIndex;
                global::AssetCharacterOccupationData replacementData;
                Occupation replacementOccupation;
                if (_occupations.TryFindActiveOccupation(
                        character,
                        restoreToken.ReplacedByOccupationGuid,
                        out replacementIndex,
                        out replacementData,
                        out replacementOccupation))
                {
                    ParalivesOccupationEnrollmentResult replacementUnenroll =
                        TryUnenroll(characterGuid, replacementIndex, false);
                    if (!replacementUnenroll.Succeeded)
                    {
                        return Fail(
                            "replacement-unenroll-failed",
                            "The replacement occupation could not be unenrolled: " + replacementUnenroll.Message,
                            characterGuid,
                            snapshot.OccupationGuid,
                            snapshot.OccupationIndex,
                            snapshot);
                    }
                }
            }

            EnsureOccupationLists(character);

            int restoreIndex = FindRestoreIndex(character, snapshot);
            int activeIndex;
            global::AssetCharacterOccupationData activeData;
            Occupation activeOccupation;
            if (_occupations.TryFindActiveOccupation(
                    character,
                    snapshot.OccupationGuid,
                    out activeIndex,
                    out activeData,
                    out activeOccupation)
                && activeIndex != restoreIndex)
            {
                return Fail(
                    "previous-already-active",
                    "Another entry for the previous occupation is already active.",
                    characterGuid,
                    snapshot.OccupationGuid,
                    activeIndex,
                    BuildSnapshot(character, activeIndex, activeData, activeOccupation));
            }

            global::AssetCharacterOccupationData restoreData;
            if (restoreIndex >= 0)
            {
                restoreData = character.Data.Occupations[restoreIndex];
            }
            else
            {
                restoreData = new global::AssetCharacterOccupationData();
                character.Data.Occupations.Add(restoreData);
                restoreIndex = character.Data.Occupations.Count - 1;
            }

            ApplySnapshotToOccupationData(snapshot, restoreData);
            ApplyExpertiseSnapshots(character, snapshot.Expertises);
            ApplyRuntimeIndexes(character, restoreIndex, snapshot);
            _characters.MarkSaveDirty(character);

            return Success(
                "restored",
                "Restored.",
                characterGuid,
                snapshot.OccupationGuid,
                restoreIndex,
                BuildSnapshot(character, restoreIndex, restoreData, occupation));
        }

        private bool TryResolveCharacter(
            ulong characterGuid,
            out global::AssetCharacter character,
            out ParalivesOccupationEnrollmentResult result)
        {
            result = null;
            character = null;

            if (characterGuid == 0UL)
            {
                result = Fail("invalid-character-guid", "A character GUID is required.", characterGuid, 0UL, -1, null);
                return false;
            }

            if (!_characters.TryGet(characterGuid, out character) || character == null || character.Data == null)
            {
                result = Fail("character-not-found", "The character could not be found.", characterGuid, 0UL, -1, null);
                return false;
            }

            return true;
        }

        private static bool TryResolveOccupation(
            ulong occupationGuid,
            out Occupation occupation,
            out OccupationScheduleType scheduleType,
            out string error)
        {
            occupation = null;
            scheduleType = null;
            error = null;

            if (occupationGuid == 0UL)
            {
                error = "An occupation GUID is required.";
                return false;
            }

            Occupations occupations = GetOccupationSettings();
            if (occupations == null)
            {
                error = "Occupation settings are not ready.";
                return false;
            }

            occupation = occupations.GetOccupationByGUID(occupationGuid);
            if (occupation == null)
            {
                error = "Occupation definition was not found.";
                return false;
            }

            scheduleType = occupations.GetScheduleTypeByGUID(occupation.Schedule);
            if (scheduleType == null)
            {
                error = "Occupation schedule definition was not found.";
                return false;
            }

            if (scheduleType.PossibleOccupiedDays == null
                || scheduleType.PossibleOccupiedDays.Length == 0
                || scheduleType.PossibleOccupiedHours == null
                || scheduleType.PossibleOccupiedHours.Length == 0)
            {
                error = "Occupation schedule has no selectable days or hours.";
                return false;
            }

            return true;
        }

        private static Occupations GetOccupationSettings()
        {
            try
            {
                return global::Settings.Get<Occupations>();
            }
            catch
            {
                return null;
            }
        }

        private static bool TryResolveScheduleOptions(
            ParalivesOccupationEnrollmentOptions options,
            OccupationScheduleType scheduleType,
            out ScheduleDaysOfWeek selectedDays,
            out OccupiedHours selectedHours,
            out string error)
        {
            selectedDays = ScheduleDaysOfWeek.None;
            selectedHours = null;
            error = null;

            if (options.HasSelectedDays)
            {
                selectedDays = options.SelectedDays;
            }
            else if (options.DaysOptionIndex >= 0)
            {
                if (options.DaysOptionIndex >= scheduleType.PossibleOccupiedDays.Length)
                {
                    error = "Selected occupation days option index is outside the schedule definition.";
                    return false;
                }

                selectedDays = scheduleType.PossibleOccupiedDays[options.DaysOptionIndex];
            }

            if (options.HasSelectedHours)
            {
                selectedHours = new OccupiedHours
                {
                    GUID = options.SelectedHoursGuid,
                    StartTime = options.SelectedStartTime,
                    Duration = options.SelectedDuration,
                    IsDefault = options.SelectedHoursAreDefault
                };
            }
            else if (options.HoursOptionIndex >= 0)
            {
                if (options.HoursOptionIndex >= scheduleType.PossibleOccupiedHours.Length)
                {
                    error = "Selected occupation hours option index is outside the schedule definition.";
                    return false;
                }

                selectedHours = CloneOccupiedHours(scheduleType.PossibleOccupiedHours[options.HoursOptionIndex]);
            }

            return true;
        }

        private static bool MatchesKind(Occupation occupation, ParalivesOccupationKind kind)
        {
            if (kind == ParalivesOccupationKind.Any)
                return true;
            if (occupation == null)
                return kind == ParalivesOccupationKind.Unknown;

            return ToOccupationKind(occupation.Type) == kind;
        }

        private static ParalivesOccupationKind ToOccupationKind(SchoolJobTypes nativeKind)
        {
            if (nativeKind == SchoolJobTypes.Job)
                return ParalivesOccupationKind.Job;
            if (nativeKind == SchoolJobTypes.School)
                return ParalivesOccupationKind.School;
            return ParalivesOccupationKind.Unknown;
        }

        private ParalivesOccupationSnapshot BuildSnapshot(
            global::AssetCharacter character,
            int occupationIndex,
            global::AssetCharacterOccupationData data,
            Occupation occupation)
        {
            ParalivesOccupationSnapshot snapshot = CreateMissingSnapshot(
                character == null ? 0UL : character.GUID,
                occupationIndex);

            if (character == null || character.Data == null || data == null)
                return snapshot;

            snapshot.Exists = true;
            snapshot.OccupationGuid = data.Occupation;
            snapshot.IsActive = data.IsActive;
            snapshot.Level = data.Level;
            snapshot.StartTimestamp = data.StartTimestamp;
            snapshot.EndTimestamp = data.EndTimestamp;
            snapshot.Schedule = CreateScheduleSnapshot(character, occupationIndex, data, occupation);
            snapshot.TimeLastChangedSchedule = data.TimeLastChangedSchedule;
            snapshot.StartingUsefulSkills = SnapshotStartingSkills(data.StartingUsefulSkillsLevels);
            snapshot.PendingUpgradeCount = data.PendingUpgradeCount;
            snapshot.UpgradesCompletedCount = data.UpgradesCompletedCount;
            snapshot.CurrentPendingUpgradeLastGeneratedAtCount = data.CurrentPendingUpgradeLastGeneratedAtCount;
            snapshot.Extras = SnapshotUnlockables(data.Extras);
            snapshot.PendingRandomizedUpgradeGuids = CopyUlongList(data.PendingRandomizedUpgrades);
            snapshot.JobPerformance = data.JobPerformance;
            snapshot.TimestampsOfStrikes = CopyFloatList(data.TimestampsOfStrikes);
            snapshot.NumberOfVacationDaysAvailable = data.NumberOfVacationDaysAvailable;
            snapshot.NextSkippedDays = CopyIntList(data.NextSkippedDays);
            snapshot.LastDayUpdated = data.LastDayUpdated;
            snapshot.HasEndedWorkedDay = data.HasEndedWorkedDay;
            snapshot.NumberOfStrikes = data.TimestampsOfStrikes == null ? 0 : data.TimestampsOfStrikes.Count;
            snapshot.MaxExtraSlots = _occupations.GetMaxExtraSlots(character, occupationIndex);
            snapshot.CurrentOccupationIndex = character.Data.CurrentOccupationIndex;
            snapshot.OccupationIndexToGoTo = character.Data.OccupationIndexToGoTo;
            snapshot.CurrentlyAffectedOccupationIndex = character.Data.CurrentlyAffectedOccupationIndex;
            snapshot.TimeLeftCurrentlyAffectedOccupation = character.Data.TimeLeftCurrentlyAffectedOccupation;
            snapshot.WasCurrentOccupation = character.Data.CurrentOccupationIndex == occupationIndex;
            snapshot.WasOccupationIndexToGoTo = character.Data.OccupationIndexToGoTo == occupationIndex;
            snapshot.WasCurrentlyAffectedOccupation = character.Data.CurrentlyAffectedOccupationIndex == occupationIndex;

            if (occupation != null)
            {
                snapshot.IsKnownOccupation = true;
                snapshot.DisplayName = occupation.DisplayName ?? string.Empty;
                snapshot.Kind = ToOccupationKind(occupation.Type);
                snapshot.NativeKindValue = (int)occupation.Type;
                snapshot.AverageGrade = _occupations.GetAverageGrade(character, occupation.GUID);
                snapshot.Expertises = SnapshotExpertises(character, occupation);
            }
            else
            {
                snapshot.Kind = ParalivesOccupationKind.Unknown;
                snapshot.NativeKindValue = -1;
                snapshot.Expertises = new ParalivesOccupationUnlockableSnapshot[0];
            }

            ParalivesScheduleStatus schedule = _occupations.GetScheduleStatus(character, occupationIndex);
            snapshot.IsInScheduledDay = schedule.IsInScheduledDay;
            snapshot.IsInScheduledHours = schedule.IsInScheduledHours;
            snapshot.ShouldBeWorkingNow = schedule.ShouldBeWorkingNow;
            if (snapshot.Schedule != null)
            {
                snapshot.Schedule.IsInScheduledDay = schedule.IsInScheduledDay;
                snapshot.Schedule.IsInScheduledHours = schedule.IsInScheduledHours;
                snapshot.Schedule.ShouldBeWorkingNow = schedule.ShouldBeWorkingNow;
            }
            return snapshot;
        }

        private static ParalivesOccupationSnapshot CreateMissingSnapshot(ulong characterGuid, int occupationIndex)
        {
            return new ParalivesOccupationSnapshot
            {
                CharacterGuid = characterGuid,
                OccupationIndex = occupationIndex
            };
        }

        private static ParalivesAssignedOccupationScheduleSnapshot CreateScheduleSnapshot(
            global::AssetCharacter character,
            int occupationIndex,
            global::AssetCharacterOccupationData data,
            Occupation occupation)
        {
            ParalivesAssignedOccupationScheduleSnapshot snapshot = new ParalivesAssignedOccupationScheduleSnapshot
            {
                Exists = data != null,
                CharacterGuid = character == null ? 0UL : character.GUID,
                OccupationIndex = occupationIndex,
                OccupationGuid = data == null ? 0UL : data.Occupation,
                ScheduleGuid = occupation == null ? 0UL : occupation.Schedule,
                IsKnownOccupation = occupation != null,
                OccupationType = occupation == null ? -1 : (int)occupation.Type,
                IsSchool = occupation != null && occupation.Type == SchoolJobTypes.School,
                IsActive = data != null && data.IsActive,
                ChosenDays = data == null ? ScheduleDaysOfWeek.None : data.ChosenScheduledDays,
                DayOfWeek = global::ParaTime.DayOfWeek,
                MinutesOfDay = global::ParaTime.MinutesOfDay
            };

            if (data != null && data.ChosenScheduleHours != null)
            {
                snapshot.ChosenHours = new ParalivesOccupationScheduleHoursOption
                {
                    Guid = data.ChosenScheduleHours.GUID,
                    StartTime = data.ChosenScheduleHours.StartTime,
                    Duration = data.ChosenScheduleHours.Duration,
                    IsDefault = data.ChosenScheduleHours.IsDefault
                };
            }

            return snapshot;
        }

        private static ParalivesOccupationSkillLevelSnapshot[] SnapshotStartingSkills(
            global::StartingSkillLevelForOccupation[] skills)
        {
            if (skills == null || skills.Length == 0)
                return new ParalivesOccupationSkillLevelSnapshot[0];

            ParalivesOccupationSkillLevelSnapshot[] result =
                new ParalivesOccupationSkillLevelSnapshot[skills.Length];
            for (int i = 0; i < skills.Length; i++)
            {
                result[i] = new ParalivesOccupationSkillLevelSnapshot
                {
                    SkillGuid = skills[i].SkillGUID,
                    Level = skills[i].Level
                };
            }

            return result;
        }

        private static ParalivesOccupationUnlockableSnapshot[] SnapshotUnlockables(
            List<global::AssetCharacterOccupationUnlockableData> unlockables)
        {
            if (unlockables == null || unlockables.Count == 0)
                return new ParalivesOccupationUnlockableSnapshot[0];

            List<ParalivesOccupationUnlockableSnapshot> snapshots =
                new List<ParalivesOccupationUnlockableSnapshot>();
            for (int i = 0; i < unlockables.Count; i++)
            {
                global::AssetCharacterOccupationUnlockableData unlockable = unlockables[i];
                if (unlockable != null)
                    snapshots.Add(SnapshotUnlockable(unlockable));
            }

            return snapshots.ToArray();
        }

        private static ParalivesOccupationUnlockableSnapshot[] SnapshotExpertises(
            global::AssetCharacter character,
            Occupation occupation)
        {
            if (character == null
                || character.Data == null
                || character.Data.OccupationExpertises == null
                || occupation == null
                || occupation.Unlockables == null)
            {
                return new ParalivesOccupationUnlockableSnapshot[0];
            }

            HashSet<ulong> occupationUnlockables = new HashSet<ulong>();
            for (int i = 0; i < occupation.Unlockables.Length; i++)
            {
                PossibleUnlockable possible = occupation.Unlockables[i];
                if (possible != null && possible.Unlockable != 0UL)
                    occupationUnlockables.Add(possible.Unlockable);
            }

            List<ParalivesOccupationUnlockableSnapshot> snapshots =
                new List<ParalivesOccupationUnlockableSnapshot>();
            for (int i = 0; i < character.Data.OccupationExpertises.Count; i++)
            {
                global::AssetCharacterOccupationUnlockableData expertise =
                    character.Data.OccupationExpertises[i];
                if (expertise != null && occupationUnlockables.Contains(expertise.OccupationUnlockable))
                    snapshots.Add(SnapshotUnlockable(expertise));
            }

            return snapshots.ToArray();
        }

        private static ParalivesOccupationUnlockableSnapshot SnapshotUnlockable(
            global::AssetCharacterOccupationUnlockableData unlockable)
        {
            return new ParalivesOccupationUnlockableSnapshot
            {
                UnlockableGuid = unlockable.OccupationUnlockable,
                Level = unlockable.Level,
                TimeOfLastLeveledUp = unlockable.TimeOfLastLeveledUp,
                TimeAdded = unlockable.TimeAdded,
                UnlockableWasAdded = (int)unlockable.UnlockableWasAdded,
                Value = unlockable.Value
            };
        }

        private static ulong[] CopyUlongList(List<ulong> source)
        {
            if (source == null || source.Count == 0)
                return new ulong[0];
            return source.ToArray();
        }

        private static float[] CopyFloatList(List<float> source)
        {
            if (source == null || source.Count == 0)
                return new float[0];
            return source.ToArray();
        }

        private static int[] CopyIntList(List<int> source)
        {
            if (source == null || source.Count == 0)
                return new int[0];
            return source.ToArray();
        }

        private static ParalivesOccupationRestoreToken CreateRestoreToken(
            ParalivesOccupationSnapshot snapshot)
        {
            return new ParalivesOccupationRestoreToken
            {
                CharacterGuid = snapshot.CharacterGuid,
                PreviousOccupationGuid = snapshot.OccupationGuid,
                PreviousOccupationIndex = snapshot.OccupationIndex,
                PreviousOccupationKind = snapshot.Kind,
                WasActive = snapshot.IsActive,
                CapturedAtTimestamp = global::ParaTime.TotalMinutes,
                Snapshot = snapshot
            };
        }

        private static void ApplySnapshotToOccupationData(
            ParalivesOccupationSnapshot snapshot,
            global::AssetCharacterOccupationData data)
        {
            data.Occupation = snapshot.OccupationGuid;
            data.Level = snapshot.Level <= 0 ? 1 : snapshot.Level;
            data.StartTimestamp = snapshot.StartTimestamp;
            data.EndTimestamp = snapshot.IsActive ? 0f : snapshot.EndTimestamp;
            data.ChosenScheduledDays = snapshot.Schedule == null
                ? ScheduleDaysOfWeek.None
                : snapshot.Schedule.ChosenDays;
            data.ChosenScheduleHours = CreateOccupiedHours(snapshot.Schedule);
            data.TimeLastChangedSchedule = snapshot.TimeLastChangedSchedule;
            data.StartingUsefulSkillsLevels = CreateStartingSkills(snapshot.StartingUsefulSkills);
            data.PendingUpgradeCount = snapshot.PendingUpgradeCount;
            data.UpgradesCompletedCount = snapshot.UpgradesCompletedCount;
            data.CurrentPendingUpgradeLastGeneratedAtCount = snapshot.CurrentPendingUpgradeLastGeneratedAtCount;
            data.Extras = CreateUnlockableData(snapshot.Extras);
            data.PendingRandomizedUpgrades = new List<ulong>(snapshot.PendingRandomizedUpgradeGuids ?? new ulong[0]);
            data.JobPerformance = snapshot.JobPerformance;
            data.TimestampsOfStrikes = new List<float>(snapshot.TimestampsOfStrikes ?? new float[0]);
            data.NumberOfVacationDaysAvailable = snapshot.NumberOfVacationDaysAvailable;
            data.NextSkippedDays = new List<int>(snapshot.NextSkippedDays ?? new int[0]);
            data.LastDayUpdated = snapshot.LastDayUpdated;
            data.HasEndedWorkedDay = snapshot.HasEndedWorkedDay;
        }

        private static OccupiedHours CreateOccupiedHours(ParalivesAssignedOccupationScheduleSnapshot snapshot)
        {
            if (snapshot == null || snapshot.ChosenHours == null)
                return new OccupiedHours();

            return new OccupiedHours
            {
                GUID = snapshot.ChosenHours.Guid,
                StartTime = snapshot.ChosenHours.StartTime,
                Duration = snapshot.ChosenHours.Duration,
                IsDefault = snapshot.ChosenHours.IsDefault
            };
        }

        private static global::StartingSkillLevelForOccupation[] CreateStartingSkills(
            ParalivesOccupationSkillLevelSnapshot[] snapshots)
        {
            if (snapshots == null || snapshots.Length == 0)
                return null;

            global::StartingSkillLevelForOccupation[] result =
                new global::StartingSkillLevelForOccupation[snapshots.Length];
            for (int i = 0; i < snapshots.Length; i++)
            {
                ParalivesOccupationSkillLevelSnapshot snapshot = snapshots[i];
                if (snapshot == null)
                    continue;

                result[i] = new global::StartingSkillLevelForOccupation
                {
                    SkillGUID = snapshot.SkillGuid,
                    Level = snapshot.Level
                };
            }

            return result;
        }

        private static List<global::AssetCharacterOccupationUnlockableData> CreateUnlockableData(
            ParalivesOccupationUnlockableSnapshot[] snapshots)
        {
            List<global::AssetCharacterOccupationUnlockableData> result =
                new List<global::AssetCharacterOccupationUnlockableData>();
            if (snapshots == null)
                return result;

            for (int i = 0; i < snapshots.Length; i++)
            {
                ParalivesOccupationUnlockableSnapshot snapshot = snapshots[i];
                if (snapshot == null || snapshot.UnlockableGuid == 0UL)
                    continue;

                result.Add(CreateUnlockableData(snapshot));
            }

            return result;
        }

        private static global::AssetCharacterOccupationUnlockableData CreateUnlockableData(
            ParalivesOccupationUnlockableSnapshot snapshot)
        {
            return new global::AssetCharacterOccupationUnlockableData
            {
                OccupationUnlockable = snapshot.UnlockableGuid,
                Level = snapshot.Level <= 0 ? 1 : snapshot.Level,
                TimeOfLastLeveledUp = snapshot.TimeOfLastLeveledUp,
                TimeAdded = snapshot.TimeAdded,
                UnlockableWasAdded = (UnlockableWasAdded)snapshot.UnlockableWasAdded,
                Value = snapshot.Value
            };
        }

        private static void ApplyExpertiseSnapshots(
            global::AssetCharacter character,
            ParalivesOccupationUnlockableSnapshot[] expertises)
        {
            if (character == null || character.Data == null || expertises == null)
                return;

            if (character.Data.OccupationExpertises == null)
                character.Data.OccupationExpertises = new List<global::AssetCharacterOccupationUnlockableData>();

            for (int i = 0; i < expertises.Length; i++)
            {
                ParalivesOccupationUnlockableSnapshot snapshot = expertises[i];
                if (snapshot == null || snapshot.UnlockableGuid == 0UL)
                    continue;

                global::AssetCharacterOccupationUnlockableData data =
                    FindUnlockable(character.Data.OccupationExpertises, snapshot.UnlockableGuid);
                if (data == null)
                {
                    character.Data.OccupationExpertises.Add(CreateUnlockableData(snapshot));
                    continue;
                }

                data.Level = snapshot.Level <= 0 ? 1 : snapshot.Level;
                data.TimeOfLastLeveledUp = snapshot.TimeOfLastLeveledUp;
                data.TimeAdded = snapshot.TimeAdded;
                data.UnlockableWasAdded = (UnlockableWasAdded)snapshot.UnlockableWasAdded;
                data.Value = snapshot.Value;
            }
        }

        private static global::AssetCharacterOccupationUnlockableData FindUnlockable(
            List<global::AssetCharacterOccupationUnlockableData> data,
            ulong unlockableGuid)
        {
            if (data == null || unlockableGuid == 0UL)
                return null;

            for (int i = 0; i < data.Count; i++)
            {
                global::AssetCharacterOccupationUnlockableData item = data[i];
                if (item != null && item.OccupationUnlockable == unlockableGuid)
                    return item;
            }

            return null;
        }

        private static void ApplyRuntimeIndexes(
            global::AssetCharacter character,
            int restoreIndex,
            ParalivesOccupationSnapshot snapshot)
        {
            if (character == null || character.Data == null)
                return;

            if (!snapshot.IsActive)
            {
                ClearRuntimeIndexesForOccupation(character, restoreIndex);
                return;
            }

            if (snapshot.WasCurrentOccupation)
                character.Data.CurrentOccupationIndex = restoreIndex;
            if (snapshot.WasOccupationIndexToGoTo)
                character.Data.OccupationIndexToGoTo = restoreIndex;
            if (snapshot.WasCurrentlyAffectedOccupation)
            {
                character.Data.CurrentlyAffectedOccupationIndex = restoreIndex;
                character.Data.TimeLeftCurrentlyAffectedOccupation = snapshot.TimeLeftCurrentlyAffectedOccupation;
            }
        }

        private static bool ClearRuntimeIndexesForOccupation(
            global::AssetCharacter character,
            int occupationIndex)
        {
            if (character == null || character.Data == null || occupationIndex < 0)
                return false;

            bool changed = false;
            if (character.Data.CurrentOccupationIndex == occupationIndex)
            {
                character.Data.CurrentOccupationIndex = -1;
                changed = true;
            }

            if (character.Data.OccupationIndexToGoTo == occupationIndex)
            {
                character.Data.OccupationIndexToGoTo = -1;
                changed = true;
            }

            if (character.Data.CurrentlyAffectedOccupationIndex == occupationIndex)
            {
                character.Data.CurrentlyAffectedOccupationIndex = -1;
                character.Data.TimeLeftCurrentlyAffectedOccupation = 0f;
                changed = true;
            }

            return changed;
        }

        private static void EnsureOccupationLists(global::AssetCharacter character)
        {
            if (character == null || character.Data == null)
                return;

            if (character.Data.Occupations == null)
                character.Data.Occupations = new List<global::AssetCharacterOccupationData>();
            if (character.Data.OccupationExpertises == null)
                character.Data.OccupationExpertises = new List<global::AssetCharacterOccupationUnlockableData>();
            if (character.Data.Wants == null)
                character.Data.Wants = new List<global::AssetCharacterWantData>();
            if (character.Data.Relationships == null)
                character.Data.Relationships = new List<global::AssetCharacterRelationshipData>();
        }

        private static int FindNewActiveOccupationIndex(
            global::AssetCharacter character,
            ulong occupationGuid,
            int startIndex)
        {
            if (character == null
                || character.Data == null
                || character.Data.Occupations == null
                || occupationGuid == 0UL)
            {
                return -1;
            }

            int first = startIndex < 0 ? 0 : startIndex;
            for (int i = first; i < character.Data.Occupations.Count; i++)
            {
                global::AssetCharacterOccupationData data = character.Data.Occupations[i];
                if (data != null && data.IsActive && data.Occupation == occupationGuid)
                    return i;
            }

            return -1;
        }

        private static int FindRestoreIndex(
            global::AssetCharacter character,
            ParalivesOccupationSnapshot snapshot)
        {
            if (character == null
                || character.Data == null
                || character.Data.Occupations == null
                || snapshot == null)
            {
                return -1;
            }

            if (snapshot.OccupationIndex >= 0
                && snapshot.OccupationIndex < character.Data.Occupations.Count)
            {
                global::AssetCharacterOccupationData preferred =
                    character.Data.Occupations[snapshot.OccupationIndex];
                if (preferred != null && preferred.Occupation == snapshot.OccupationGuid)
                    return snapshot.OccupationIndex;
            }

            int inactiveMatch = -1;
            for (int i = 0; i < character.Data.Occupations.Count; i++)
            {
                global::AssetCharacterOccupationData data = character.Data.Occupations[i];
                if (data == null || data.Occupation != snapshot.OccupationGuid)
                    continue;

                if (data.IsActive)
                    return i;
                if (inactiveMatch < 0)
                    inactiveMatch = i;
            }

            return inactiveMatch;
        }

        private static OccupiedHours CloneOccupiedHours(OccupiedHours source)
        {
            if (source == null)
                return null;

            return new OccupiedHours
            {
                GUID = source.GUID,
                StartTime = source.StartTime,
                Duration = source.Duration,
                IsDefault = source.IsDefault
            };
        }

        private static ParalivesOccupationEnrollmentResult Success(
            string code,
            string message,
            ulong characterGuid,
            ulong occupationGuid,
            int occupationIndex,
            ParalivesOccupationSnapshot snapshot)
        {
            return new ParalivesOccupationEnrollmentResult
            {
                Succeeded = true,
                Code = code ?? string.Empty,
                Message = message ?? string.Empty,
                CharacterGuid = characterGuid,
                OccupationGuid = occupationGuid,
                OccupationIndex = occupationIndex,
                Snapshot = snapshot
            };
        }

        private static ParalivesOccupationEnrollmentResult Fail(
            string code,
            string message,
            ulong characterGuid,
            ulong occupationGuid,
            int occupationIndex,
            ParalivesOccupationSnapshot snapshot)
        {
            return new ParalivesOccupationEnrollmentResult
            {
                Succeeded = false,
                Code = code ?? string.Empty,
                Message = message ?? string.Empty,
                CharacterGuid = characterGuid,
                OccupationGuid = occupationGuid,
                OccupationIndex = occupationIndex,
                Snapshot = snapshot
            };
        }
    }
}
