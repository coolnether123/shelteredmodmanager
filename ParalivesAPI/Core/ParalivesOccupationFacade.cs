using System.Collections.Generic;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesScheduleStatus
    {
        public bool IsInScheduledDay { get; internal set; }

        public bool IsInScheduledHours { get; internal set; }

        public bool ShouldBeWorkingNow { get; internal set; }
    }

    public sealed class ParalivesOccupationSummary
    {
        public ulong CharacterGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public bool IsKnownOccupation { get; internal set; }

        public SchoolJobTypes Type { get; internal set; }

        public bool IsSchool { get; internal set; }

        public bool IsActive { get; internal set; }

        public int Level { get; internal set; }

        public int JobPerformance { get; internal set; }

        public int AverageGrade { get; internal set; }

        public int PendingUpgradeCount { get; internal set; }

        public int UpgradesCompletedCount { get; internal set; }

        public int ExtraCount { get; internal set; }

        public int MaxExtraSlots { get; internal set; }

        public int NumberOfVacationDaysAvailable { get; internal set; }

        public int NumberOfStrikes { get; internal set; }

        public int CurrentOccupationIndex { get; internal set; }

        public int OccupationIndexToGoTo { get; internal set; }

        public bool IsInScheduledDay { get; internal set; }

        public bool IsInScheduledHours { get; internal set; }

        public bool ShouldBeWorkingNow { get; internal set; }
    }

    public sealed class ParalivesOccupationUpgradeResult
    {
        public bool Succeeded { get; internal set; }

        public bool PassedPerformanceCheck { get; internal set; }

        public int NumberOfUpgrades { get; internal set; }

        public string Message { get; internal set; }
    }

    public sealed class ParalivesOccupationFacade
    {
        private readonly ParalivesCharacterFacade _characters;

        internal ParalivesOccupationFacade(ParalivesCharacterFacade characters)
        {
            _characters = characters;
        }

        public bool TryGetOccupationData(
            global::AssetCharacter character,
            int occupationIndex,
            out global::AssetCharacterOccupationData occupationData)
        {
            occupationData = null;
            if (character == null || character.Data == null || character.Data.Occupations == null)
                return false;
            if (occupationIndex < 0 || occupationIndex >= character.Data.Occupations.Count)
                return false;

            occupationData = character.Data.Occupations[occupationIndex];
            return occupationData != null;
        }

        public bool TryGetOccupationData(
            ulong characterGuid,
            int occupationIndex,
            out global::AssetCharacterOccupationData occupationData)
        {
            occupationData = null;
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                && TryGetOccupationData(character, occupationIndex, out occupationData);
        }

        public bool TryGetOccupation(
            global::AssetCharacter character,
            int occupationIndex,
            out Occupation occupation)
        {
            occupation = null;
            global::AssetCharacterOccupationData occupationData;
            if (!TryGetOccupationData(character, occupationIndex, out occupationData))
                return false;

            Occupations occupations = global::Settings.Get<Occupations>();
            if (occupations == null)
                return false;

            occupation = occupations.GetOccupationByGUID(occupationData.Occupation);
            return occupation != null;
        }

        public bool TryFindActiveOccupation(
            global::AssetCharacter character,
            ulong occupationGuid,
            out int occupationIndex,
            out global::AssetCharacterOccupationData occupationData,
            out Occupation occupation)
        {
            occupationIndex = -1;
            occupationData = null;
            occupation = null;

            if (character == null || character.Data == null || character.Data.Occupations == null || occupationGuid == 0UL)
                return false;

            for (int i = 0; i < character.Data.Occupations.Count; i++)
            {
                global::AssetCharacterOccupationData data = character.Data.Occupations[i];
                if (data == null || !data.IsActive || data.Occupation != occupationGuid)
                    continue;

                occupationIndex = i;
                occupationData = data;
                TryGetOccupation(character, i, out occupation);
                return true;
            }

            return false;
        }

        public bool TryFindActiveOccupation(
            ulong characterGuid,
            ulong occupationGuid,
            out int occupationIndex,
            out global::AssetCharacterOccupationData occupationData,
            out Occupation occupation)
        {
            occupationIndex = -1;
            occupationData = null;
            occupation = null;

            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                && TryFindActiveOccupation(character, occupationGuid, out occupationIndex, out occupationData, out occupation);
        }

        public bool IsSchool(ulong occupationGuid)
        {
            Occupations occupations = global::Settings.Get<Occupations>();
            Occupation occupation = occupations != null ? occupations.GetOccupationByGUID(occupationGuid) : null;
            return occupation != null && occupation.Type == SchoolJobTypes.School;
        }

        public int[] GetActiveOccupationIndexes(global::AssetCharacter character)
        {
            List<int> indexes = new List<int>();
            if (character == null || character.Data == null || character.Data.Occupations == null)
                return indexes.ToArray();

            for (int i = 0; i < character.Data.Occupations.Count; i++)
            {
                global::AssetCharacterOccupationData data = character.Data.Occupations[i];
                if (data != null && data.IsActive)
                    indexes.Add(i);
            }

            return indexes.ToArray();
        }

        public int[] GetActiveOccupationIndexes(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? GetActiveOccupationIndexes(character)
                : new int[0];
        }

        public bool Enroll(
            global::AssetCharacter character,
            ulong occupationGuid,
            ScheduleDaysOfWeek selectedDays,
            OccupiedHours selectedHours,
            int startingRank)
        {
            if (character == null || character.Data == null || occupationGuid == 0UL || global::OccupationsManager.Instance == null)
                return false;

            try
            {
                if (character.Data.Occupations == null)
                    character.Data.Occupations = new List<global::AssetCharacterOccupationData>();

                global::OccupationsManager.Instance.EnrollToOccupation(
                    character,
                    occupationGuid,
                    selectedDays,
                    selectedHours,
                    startingRank);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool Enroll(
            ulong characterGuid,
            ulong occupationGuid,
            ScheduleDaysOfWeek selectedDays,
            OccupiedHours selectedHours,
            int startingRank)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                && Enroll(character, occupationGuid, selectedDays, selectedHours, startingRank);
        }

        public bool Unenroll(global::AssetCharacter character, int occupationIndex)
        {
            return Unenroll(character, occupationIndex, true);
        }

        public bool Unenroll(global::AssetCharacter character, int occupationIndex, bool wasFired)
        {
            if (character == null || global::OccupationsManager.Instance == null)
                return false;

            global::AssetCharacterOccupationData occupationData;
            if (!TryGetOccupationData(character, occupationIndex, out occupationData) || !occupationData.IsActive)
                return false;

            try
            {
                global::OccupationsManager.Instance.UnenrollToOccupation(character, occupationIndex, wasFired);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ParalivesOccupationUpgradeResult GrantUpgrade(global::AssetCharacter character, int occupationIndex)
        {
            return GrantUpgrade(character, occupationIndex, false);
        }

        public ParalivesOccupationUpgradeResult GrantUpgrade(
            global::AssetCharacter character,
            int occupationIndex,
            bool forceUpgrade)
        {
            ParalivesOccupationUpgradeResult result = new ParalivesOccupationUpgradeResult
            {
                Message = string.Empty
            };

            if (character == null || global::OccupationsManager.Instance == null)
            {
                result.Message = "Occupation manager is not ready.";
                return result;
            }

            global::AssetCharacterOccupationData occupationData;
            if (!TryGetOccupationData(character, occupationIndex, out occupationData))
            {
                result.Message = "Occupation index is invalid.";
                return result;
            }

            try
            {
                var upgrade = global::OccupationsManager.Instance.GrantOccupationUpgradeToCharacter(
                    character,
                    occupationIndex,
                    forceUpgrade);
                result.PassedPerformanceCheck = upgrade.Item1;
                result.NumberOfUpgrades = upgrade.Item2;
                result.Succeeded = forceUpgrade || result.PassedPerformanceCheck || result.NumberOfUpgrades > 0;
                result.Message = result.Succeeded ? "Granted." : "Performance check failed.";
                return result;
            }
            catch (System.Exception ex)
            {
                result.Message = ex.Message;
                return result;
            }
        }

        public int GetMaxExtraSlots(global::AssetCharacter character, int occupationIndex)
        {
            if (character == null || character.Data == null)
                return 0;

            try
            {
                Occupations occupations = global::Settings.Get<Occupations>();
                return occupations == null ? 0 : occupations.GetMaxNumberOfExtraSlots(character, occupationIndex);
            }
            catch
            {
                return 0;
            }
        }

        public bool TryGrantExtraUnlockable(global::AssetCharacter character, int occupationIndex, ulong unlockableGuid)
        {
            if (character == null || unlockableGuid == 0UL || global::OccupationsManager.Instance == null)
                return false;

            global::AssetCharacterOccupationData occupationData;
            if (!TryGetOccupationData(character, occupationIndex, out occupationData))
                return false;

            Occupations occupations = global::Settings.Get<Occupations>();
            OccupationUnlockable unlockable = occupations == null
                ? null
                : occupations.GetOccupationUnlockableByGUID(unlockableGuid);
            if (unlockable == null || unlockable.Type != OccupationUnlockableTypes.Extra)
                return false;

            if (occupationData.Extras == null)
                occupationData.Extras = new List<global::AssetCharacterOccupationUnlockableData>();

            for (int i = 0; i < occupationData.Extras.Count; i++)
            {
                global::AssetCharacterOccupationUnlockableData extra = occupationData.Extras[i];
                if (extra != null && extra.OccupationUnlockable == unlockableGuid)
                    return false;
            }

            if (occupationData.Extras.Count >= GetMaxExtraSlots(character, occupationIndex))
                return false;

            try
            {
                global::OccupationsManager.Instance.AddOrLevelUpUnlockable(
                    character,
                    occupationData.Extras,
                    unlockableGuid,
                    occupationIndex);
                character.IsSaveDirty = true;
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool TryGetExpertise(
            global::AssetCharacter character,
            ulong unlockableGuid,
            out global::AssetCharacterOccupationUnlockableData expertise)
        {
            expertise = null;
            if (character == null || character.Data == null || character.Data.OccupationExpertises == null || unlockableGuid == 0UL)
                return false;

            for (int i = 0; i < character.Data.OccupationExpertises.Count; i++)
            {
                global::AssetCharacterOccupationUnlockableData data = character.Data.OccupationExpertises[i];
                if (data != null && data.OccupationUnlockable == unlockableGuid)
                {
                    expertise = data;
                    return true;
                }
            }

            return false;
        }

        public int GetExpertiseLevel(global::AssetCharacter character, ulong unlockableGuid)
        {
            global::AssetCharacterOccupationUnlockableData data;
            return TryGetExpertise(character, unlockableGuid, out data) ? data.Level : 0;
        }

        public bool SetExpertiseLevel(global::AssetCharacter character, ulong unlockableGuid, int level)
        {
            return SetExpertiseLevel(character, unlockableGuid, level, -1f);
        }

        public bool SetExpertiseLevel(global::AssetCharacter character, ulong unlockableGuid, int level, float timeMinutes)
        {
            if (character == null || character.Data == null || unlockableGuid == 0UL)
                return false;

            if (character.Data.OccupationExpertises == null)
                character.Data.OccupationExpertises = new List<global::AssetCharacterOccupationUnlockableData>();

            int clampedLevel = level < 1 ? 1 : level;
            global::AssetCharacterOccupationUnlockableData expertise;
            if (!TryGetExpertise(character, unlockableGuid, out expertise))
            {
                expertise = new global::AssetCharacterOccupationUnlockableData
                {
                    OccupationUnlockable = unlockableGuid,
                    TimeAdded = timeMinutes >= 0f ? timeMinutes : global::ParaTime.TotalMinutes
                };
                character.Data.OccupationExpertises.Add(expertise);
            }

            if (expertise.Level != clampedLevel)
            {
                if (clampedLevel > expertise.Level)
                    expertise.TimeOfLastLeveledUp = timeMinutes >= 0f ? timeMinutes : global::ParaTime.TotalMinutes;
                expertise.Level = clampedLevel;
            }

            character.IsSaveDirty = true;
            return true;
        }

        public bool RemoveExpertise(global::AssetCharacter character, ulong unlockableGuid)
        {
            if (character == null || character.Data == null || character.Data.OccupationExpertises == null || unlockableGuid == 0UL)
                return false;

            bool changed = false;
            for (int i = character.Data.OccupationExpertises.Count - 1; i >= 0; i--)
            {
                global::AssetCharacterOccupationUnlockableData data = character.Data.OccupationExpertises[i];
                if (data != null && data.OccupationUnlockable == unlockableGuid)
                {
                    character.Data.OccupationExpertises.RemoveAt(i);
                    changed = true;
                }
            }

            if (changed)
                character.IsSaveDirty = true;

            return changed;
        }

        public int RemoveExpertisesForOccupation(global::AssetCharacter character, ulong occupationGuid)
        {
            if (character == null || character.Data == null || character.Data.OccupationExpertises == null || occupationGuid == 0UL)
                return 0;

            Occupations occupations = global::Settings.Get<Occupations>();
            Occupation occupation = occupations == null ? null : occupations.GetOccupationByGUID(occupationGuid);
            if (occupation == null || occupation.Unlockables == null)
                return 0;

            HashSet<ulong> unlockables = new HashSet<ulong>();
            for (int i = 0; i < occupation.Unlockables.Length; i++)
            {
                PossibleUnlockable possible = occupation.Unlockables[i];
                if (possible != null && possible.Unlockable != 0UL)
                    unlockables.Add(possible.Unlockable);
            }

            int removed = 0;
            for (int i = character.Data.OccupationExpertises.Count - 1; i >= 0; i--)
            {
                global::AssetCharacterOccupationUnlockableData data = character.Data.OccupationExpertises[i];
                if (data != null && unlockables.Contains(data.OccupationUnlockable))
                {
                    character.Data.OccupationExpertises.RemoveAt(i);
                    removed++;
                }
            }

            if (removed > 0)
                character.IsSaveDirty = true;

            return removed;
        }

        public bool ClearPendingUpgrades(global::AssetCharacter character, int occupationIndex)
        {
            global::AssetCharacterOccupationData data;
            if (!TryGetOccupationData(character, occupationIndex, out data))
                return false;

            bool changed = false;
            if (data.PendingUpgradeCount != 0)
            {
                data.PendingUpgradeCount = 0;
                changed = true;
            }

            if (data.PendingRandomizedUpgrades != null && data.PendingRandomizedUpgrades.Count > 0)
            {
                data.PendingRandomizedUpgrades.Clear();
                changed = true;
            }

            if (changed)
                character.IsSaveDirty = true;

            return changed;
        }

        public int[] GetActiveSchoolIndexes(global::AssetCharacter character)
        {
            List<int> indexes = new List<int>();
            int[] active = GetActiveOccupationIndexes(character);
            for (int i = 0; i < active.Length; i++)
            {
                Occupation occupation;
                if (TryGetOccupation(character, active[i], out occupation) && occupation.Type == SchoolJobTypes.School)
                    indexes.Add(active[i]);
            }

            return indexes.ToArray();
        }

        public int[] GetActiveSchoolIndexes(ulong characterGuid)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? GetActiveSchoolIndexes(character)
                : new int[0];
        }

        public bool TryFindActiveSchool(
            global::AssetCharacter character,
            out int occupationIndex,
            out global::AssetCharacterOccupationData occupationData,
            out Occupation occupation)
        {
            occupationIndex = -1;
            occupationData = null;
            occupation = null;

            int[] schoolIndexes = GetActiveSchoolIndexes(character);
            if (schoolIndexes.Length == 0)
                return false;

            occupationIndex = schoolIndexes[0];
            return TryGetOccupationData(character, occupationIndex, out occupationData)
                && TryGetOccupation(character, occupationIndex, out occupation);
        }

        public ParalivesScheduleStatus GetScheduleStatus(global::AssetCharacter character, int occupationIndex)
        {
            ParalivesScheduleStatus status = new ParalivesScheduleStatus();
            if (character == null || global::OccupationsManager.Instance == null)
                return status;

            try
            {
                var schedule = global::OccupationsManager.Instance.CheckIfInSchedule(
                    character,
                    occupationIndex,
                    global::ParaTime.DayOfWeek,
                    global::ParaTime.MinutesOfDay,
                    false,
                    true);

                status.IsInScheduledDay = schedule.Item1;
                status.IsInScheduledHours = schedule.Item2;
                status.ShouldBeWorkingNow = global::OccupationsManager.Instance.ShouldBeWorkingNow(character, occupationIndex);
            }
            catch
            {
            }

            return status;
        }

        public int GetAverageGrade(global::AssetCharacter character, ulong occupationGuid)
        {
            if (character == null || occupationGuid == 0UL || global::OccupationsManager.Instance == null)
                return 0;

            Occupations occupations = global::Settings.Get<Occupations>();
            Occupation occupation = occupations != null ? occupations.GetOccupationByGUID(occupationGuid) : null;
            if (occupation == null)
                return 0;

            try
            {
                return global::OccupationsManager.Instance.GetAverageGrade(character, occupation);
            }
            catch
            {
                return 0;
            }
        }

        public bool SetPerformance(global::AssetCharacter character, int occupationIndex, int performance)
        {
            global::AssetCharacterOccupationData data;
            if (!TryGetOccupationData(character, occupationIndex, out data))
                return false;

            int clamped = Clamp(performance, 0, 100);
            if (data.JobPerformance == clamped)
                return false;

            data.JobPerformance = clamped;
            character.IsSaveDirty = true;
            return true;
        }

        public bool SuppressAttendanceToday(global::AssetCharacter character, int occupationIndex)
        {
            global::AssetCharacterOccupationData data;
            if (!TryGetOccupationData(character, occupationIndex, out data))
                return false;

            bool changed = false;
            if (data.NextSkippedDays == null)
                data.NextSkippedDays = new List<int>();
            if (!data.NextSkippedDays.Contains(0))
            {
                data.NextSkippedDays.Add(0);
                changed = true;
            }

            if (character.Data.OccupationIndexToGoTo == occupationIndex)
            {
                character.Data.OccupationIndexToGoTo = -1;
                changed = true;
            }

            if (character.Data.CurrentOccupationIndex == occupationIndex)
            {
                character.Data.CurrentOccupationIndex = -1;
                character.Data.TimeLeftCurrentlyAffectedOccupation = global::ParaTime.TotalMinutes;
                changed = true;
            }

            if (changed)
                character.IsSaveDirty = true;

            return changed;
        }

        public bool SuppressSchoolAttendanceToday(global::AssetCharacter character)
        {
            bool changed = false;
            int[] schools = GetActiveSchoolIndexes(character);
            for (int i = 0; i < schools.Length; i++)
                changed |= SuppressAttendanceToday(character, schools[i]);

            return changed;
        }

        public ParalivesOccupationSummary GetOccupationSummary(global::AssetCharacter character, int occupationIndex)
        {
            ParalivesOccupationSummary summary = new ParalivesOccupationSummary
            {
                CharacterGuid = character == null ? 0UL : character.GUID,
                OccupationIndex = occupationIndex,
                DisplayName = string.Empty,
                CurrentOccupationIndex = character == null || character.Data == null ? -1 : character.Data.CurrentOccupationIndex,
                OccupationIndexToGoTo = character == null || character.Data == null ? -1 : character.Data.OccupationIndexToGoTo
            };

            global::AssetCharacterOccupationData data;
            if (!TryGetOccupationData(character, occupationIndex, out data))
                return summary;

            summary.OccupationGuid = data.Occupation;
            summary.IsActive = data.IsActive;
            summary.Level = data.Level;
            summary.JobPerformance = data.JobPerformance;
            summary.PendingUpgradeCount = data.PendingUpgradeCount;
            summary.UpgradesCompletedCount = data.UpgradesCompletedCount;
            summary.ExtraCount = data.Extras == null ? 0 : data.Extras.Count;
            summary.MaxExtraSlots = GetMaxExtraSlots(character, occupationIndex);
            summary.NumberOfVacationDaysAvailable = data.NumberOfVacationDaysAvailable;
            summary.NumberOfStrikes = data.NumberOfStrikes;

            Occupation occupation;
            if (TryGetOccupation(character, occupationIndex, out occupation))
            {
                summary.IsKnownOccupation = true;
                summary.DisplayName = occupation.DisplayName ?? string.Empty;
                summary.Type = occupation.Type;
                summary.IsSchool = occupation.Type == SchoolJobTypes.School;
                summary.AverageGrade = GetAverageGrade(character, occupation.GUID);
            }

            ParalivesScheduleStatus schedule = GetScheduleStatus(character, occupationIndex);
            summary.IsInScheduledDay = schedule.IsInScheduledDay;
            summary.IsInScheduledHours = schedule.IsInScheduledHours;
            summary.ShouldBeWorkingNow = schedule.ShouldBeWorkingNow;
            return summary;
        }

        public ParalivesOccupationSummary GetOccupationSummary(ulong characterGuid, int occupationIndex)
        {
            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                ? GetOccupationSummary(character, occupationIndex)
                : new ParalivesOccupationSummary
                {
                    CharacterGuid = characterGuid,
                    OccupationIndex = occupationIndex,
                    DisplayName = string.Empty,
                    CurrentOccupationIndex = -1,
                    OccupationIndexToGoTo = -1
                };
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
