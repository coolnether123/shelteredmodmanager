using System;
using System.Collections.Generic;
using ModAPI.Core;
using ParalivesAPI.Stable;
using Setting;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesOccupationScheduleDaysOption
    {
        public ulong Guid { get; set; }

        public ScheduleDaysOfWeek Days { get; set; }
    }

    public sealed class ParalivesOccupationScheduleHoursOption
    {
        public ulong Guid { get; set; }

        public float StartTime { get; set; }

        public float Duration { get; set; }

        public bool IsDefault { get; set; }
    }

    public sealed class ParalivesOccupationScheduleDefinition
    {
        public ulong ScheduleGuid { get; set; }

        public string DisplayName { get; set; }

        public ParalivesOccupationScheduleDaysOption[] PossibleDays { get; set; }

        public ParalivesOccupationScheduleHoursOption[] PossibleHours { get; set; }
    }

    public sealed class ParalivesOccupationScheduleTypeSnapshot
    {
        public bool Exists { get; internal set; }

        public ulong ScheduleGuid { get; internal set; }

        public string DisplayName { get; internal set; }

        public ParalivesOccupationScheduleDaysOption[] PossibleDays { get; internal set; }

        public ParalivesOccupationScheduleHoursOption[] PossibleHours { get; internal set; }
    }

    public sealed class ParalivesAssignedOccupationScheduleSnapshot
    {
        public bool Exists { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public ulong ScheduleGuid { get; internal set; }

        public bool IsKnownOccupation { get; internal set; }

        public int OccupationType { get; internal set; }

        public bool IsSchool { get; internal set; }

        public bool IsActive { get; internal set; }

        public ScheduleDaysOfWeek ChosenDays { get; internal set; }

        public ParalivesOccupationScheduleHoursOption ChosenHours { get; internal set; }

        public bool IsInScheduledDay { get; internal set; }

        public bool IsInScheduledHours { get; internal set; }

        public bool ShouldBeWorkingNow { get; internal set; }

        public int DayOfWeek { get; internal set; }

        public float MinutesOfDay { get; internal set; }
    }

    public sealed class ParalivesOccupationScheduleFacade : IParalivesOccupationSchedules
    {
        private readonly object _sync = new object();
        private readonly List<OccupationScheduleType> _registeredSchedules = new List<OccupationScheduleType>();
        private readonly ParalivesOccupationFacade _occupations;
        private readonly ParalivesCharacterFacade _characters;

        internal ParalivesOccupationScheduleFacade(
            ParalivesOccupationFacade occupations,
            ParalivesCharacterFacade characters)
        {
            _occupations = occupations;
            _characters = characters;
        }

        public int RegisteredScheduleCount
        {
            get { lock (_sync) return _registeredSchedules.Count; }
        }

        public void RegisterSchedule(ParalivesOccupationScheduleDefinition definition)
        {
            Register(definition);
        }

        public void Register(ParalivesOccupationScheduleDefinition definition)
        {
            if (definition == null)
                throw new ArgumentNullException("definition");

            RegisterScheduleType(CreateScheduleType(definition));
        }

        private void RegisterScheduleType(OccupationScheduleType scheduleType)
        {
            ValidateScheduleType(scheduleType, "scheduleType");

            lock (_sync)
                Upsert(_registeredSchedules, scheduleType);
        }

        public bool TryRegisterSchedule(ParalivesOccupationScheduleDefinition definition, out string message)
        {
            message = string.Empty;

            try
            {
                Register(definition);
                return true;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return false;
            }
        }

        public bool EnsureRegistered()
        {
            return ApplyWhenReady();
        }

        public bool ApplyWhenReady()
        {
            try
            {
                return ApplyWhenReadyCore();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ParalivesOccupationScheduleFacade.ApplyWhenReady",
                    "Failed to apply Paralives occupation schedule registrations: " + ex.Message);
                return false;
            }
        }

        public bool TryReadSchedule(ulong scheduleGuid, out ParalivesOccupationScheduleTypeSnapshot snapshot)
        {
            return TryReadScheduleType(scheduleGuid, out snapshot);
        }

        public bool TryReadScheduleType(ulong scheduleGuid, out ParalivesOccupationScheduleTypeSnapshot snapshot)
        {
            snapshot = CreateMissingSchedule(scheduleGuid);
            if (scheduleGuid == 0UL)
                return false;

            Occupations occupations = GetOccupationsOrNull();
            if (occupations == null)
                return false;

            OccupationScheduleType schedule = occupations.GetScheduleTypeByGUID(scheduleGuid);
            if (schedule == null)
                return false;

            snapshot = CreateScheduleSnapshot(schedule);
            return true;
        }

        public ParalivesOccupationScheduleTypeSnapshot ReadSchedule(ulong scheduleGuid)
        {
            return ReadScheduleType(scheduleGuid);
        }

        public ParalivesOccupationScheduleTypeSnapshot ReadScheduleType(ulong scheduleGuid)
        {
            ParalivesOccupationScheduleTypeSnapshot snapshot;
            return TryReadScheduleType(scheduleGuid, out snapshot)
                ? snapshot
                : CreateMissingSchedule(scheduleGuid);
        }

        public ParalivesOccupationScheduleTypeSnapshot[] ReadSchedules()
        {
            Occupations occupations = GetOccupationsOrNull();
            if (occupations == null || occupations.AllScheduleTypes == null)
                return new ParalivesOccupationScheduleTypeSnapshot[0];

            List<ParalivesOccupationScheduleTypeSnapshot> snapshots =
                new List<ParalivesOccupationScheduleTypeSnapshot>();
            for (int i = 0; i < occupations.AllScheduleTypes.Length; i++)
            {
                OccupationScheduleType schedule = occupations.AllScheduleTypes[i];
                if (schedule != null)
                    snapshots.Add(CreateScheduleSnapshot(schedule));
            }

            return snapshots.ToArray();
        }

        public bool TryReadScheduleForOccupation(
            ulong occupationGuid,
            out ParalivesOccupationScheduleTypeSnapshot snapshot)
        {
            snapshot = CreateMissingSchedule(0UL);
            if (occupationGuid == 0UL)
                return false;

            Occupations occupations = GetOccupationsOrNull();
            if (occupations == null)
                return false;

            Occupation occupation = occupations.GetOccupationByGUID(occupationGuid);
            return occupation != null && TryReadScheduleType(occupation.Schedule, out snapshot);
        }

        public bool TryReadAssignedSchedule(
            ulong characterGuid,
            int occupationIndex,
            out ParalivesAssignedOccupationScheduleSnapshot snapshot)
        {
            snapshot = CreateMissingAssignedSchedule(characterGuid, occupationIndex);

            global::AssetCharacter character;
            return _characters.TryGet(characterGuid, out character)
                && TryReadAssignedSchedule(character, occupationIndex, out snapshot);
        }

        private bool TryReadAssignedSchedule(
            global::AssetCharacter character,
            int occupationIndex,
            out ParalivesAssignedOccupationScheduleSnapshot snapshot)
        {
            snapshot = CreateMissingAssignedSchedule(
                character == null ? 0UL : character.GUID,
                occupationIndex);

            global::AssetCharacterOccupationData data;
            if (!_occupations.TryGetOccupationData(character, occupationIndex, out data))
                return false;

            Occupation occupation;
            _occupations.TryGetOccupation(character, occupationIndex, out occupation);

            bool isInScheduledDay;
            bool isInScheduledHours;
            ReadScheduleWindow(character, occupationIndex, out isInScheduledDay, out isInScheduledHours);

            OccupiedHours chosenHours = data.ChosenScheduleHours;
            snapshot = new ParalivesAssignedOccupationScheduleSnapshot
            {
                Exists = true,
                CharacterGuid = character.GUID,
                OccupationIndex = occupationIndex,
                OccupationGuid = data.Occupation,
                ScheduleGuid = occupation != null ? occupation.Schedule : 0UL,
                IsKnownOccupation = occupation != null,
                OccupationType = occupation != null ? (int)occupation.Type : 0,
                IsSchool = occupation != null && occupation.Type == SchoolJobTypes.School,
                IsActive = data.IsActive,
                ChosenDays = data.ChosenScheduledDays,
                ChosenHours = CreateHoursOption(chosenHours),
                IsInScheduledDay = isInScheduledDay,
                IsInScheduledHours = isInScheduledHours,
                ShouldBeWorkingNow = isInScheduledDay && isInScheduledHours,
                DayOfWeek = global::ParaTime.DayOfWeek,
                MinutesOfDay = global::ParaTime.MinutesOfDay
            };
            return true;
        }

        public ParalivesAssignedOccupationScheduleSnapshot ReadAssignedSchedule(
            ulong characterGuid,
            int occupationIndex)
        {
            ParalivesAssignedOccupationScheduleSnapshot snapshot;
            return TryReadAssignedSchedule(characterGuid, occupationIndex, out snapshot)
                ? snapshot
                : CreateMissingAssignedSchedule(characterGuid, occupationIndex);
        }

        private bool ApplyWhenReadyCore()
        {
            if (global::Settings.Instance == null)
                return false;

            Occupations occupations = global::Settings.Get<Occupations>();
            if (occupations == null)
                return false;

            OccupationScheduleType[] schedules;
            lock (_sync)
                schedules = _registeredSchedules.ToArray();

            bool changed = false;
            for (int i = 0; i < schedules.Length; i++)
                changed |= EnsureScheduleType(occupations, schedules[i]);

            return changed;
        }

        private static bool EnsureScheduleType(Occupations occupations, OccupationScheduleType schedule)
        {
            if (occupations == null || schedule == null || schedule.GUID == 0UL)
                return false;

            if (ContainsScheduleType(occupations.AllScheduleTypes, schedule.GUID))
                return false;

            occupations.AllScheduleTypes = Append(occupations.AllScheduleTypes, schedule);
            return true;
        }

        private static OccupationScheduleType CreateScheduleType(
            ParalivesOccupationScheduleDefinition definition)
        {
            if (definition.ScheduleGuid == 0UL)
                throw new ArgumentException("Schedule definitions must have a non-zero ScheduleGuid.", "definition");

            if (definition.PossibleDays == null || definition.PossibleDays.Length == 0)
                throw new ArgumentException("Schedule definitions must include at least one day option.", "definition");

            if (definition.PossibleHours == null || definition.PossibleHours.Length == 0)
                throw new ArgumentException("Schedule definitions must include at least one hours option.", "definition");

            return new OccupationScheduleType
            {
                GUID = definition.ScheduleGuid,
                DisplayName = definition.DisplayName ?? string.Empty,
                PossibleOccupiedDays = CreateDays(definition),
                PossibleOccupiedHours = CreateHours(definition)
            };
        }

        private static EnumAndGuid<ScheduleDaysOfWeek>[] CreateDays(
            ParalivesOccupationScheduleDefinition definition)
        {
            EnumAndGuid<ScheduleDaysOfWeek>[] days =
                new EnumAndGuid<ScheduleDaysOfWeek>[definition.PossibleDays.Length];
            for (int i = 0; i < definition.PossibleDays.Length; i++)
            {
                ParalivesOccupationScheduleDaysOption option = definition.PossibleDays[i];
                if (option == null)
                    throw new ArgumentException("Schedule day options cannot be null.", "definition");
                if (option.Days == ScheduleDaysOfWeek.None)
                    throw new ArgumentException("Schedule day options must include at least one day.", "definition");

                ulong guid = option.Guid != 0UL
                    ? option.Guid
                    : ParalivesGuid.FromStableName(
                        "ParalivesAPI.OccupationScheduleDays",
                        definition.ScheduleGuid + ":" + i + ":" + option.Days);

                days[i] = new EnumAndGuid<ScheduleDaysOfWeek>
                {
                    GUID = guid,
                    Value = option.Days
                };
            }

            return days;
        }

        private static OccupiedHours[] CreateHours(ParalivesOccupationScheduleDefinition definition)
        {
            OccupiedHours[] hours = new OccupiedHours[definition.PossibleHours.Length];
            for (int i = 0; i < definition.PossibleHours.Length; i++)
            {
                ParalivesOccupationScheduleHoursOption option = definition.PossibleHours[i];
                if (option == null)
                    throw new ArgumentException("Schedule hours options cannot be null.", "definition");
                if (option.Duration < 0f)
                    throw new ArgumentException("Schedule hours duration cannot be negative.", "definition");

                ulong guid = option.Guid != 0UL
                    ? option.Guid
                    : ParalivesGuid.FromStableName(
                        "ParalivesAPI.OccupationScheduleHours",
                        definition.ScheduleGuid + ":" + i + ":" + option.StartTime + ":" + option.Duration);

                hours[i] = new OccupiedHours
                {
                    GUID = guid,
                    StartTime = option.StartTime,
                    Duration = option.Duration,
                    IsDefault = option.IsDefault
                };
            }

            return hours;
        }

        private static void ValidateScheduleType(OccupationScheduleType scheduleType, string parameterName)
        {
            if (scheduleType == null)
                throw new ArgumentNullException(parameterName);
            if (scheduleType.GUID == 0UL)
                throw new ArgumentException("Registered schedule types must have a non-zero GUID.", parameterName);
            if (scheduleType.PossibleOccupiedDays == null || scheduleType.PossibleOccupiedDays.Length == 0)
                throw new ArgumentException("Registered schedule types must include at least one day option.", parameterName);
            if (scheduleType.PossibleOccupiedHours == null || scheduleType.PossibleOccupiedHours.Length == 0)
                throw new ArgumentException("Registered schedule types must include at least one hours option.", parameterName);
        }

        private static ParalivesOccupationScheduleTypeSnapshot CreateScheduleSnapshot(OccupationScheduleType schedule)
        {
            if (schedule == null)
                return CreateMissingSchedule(0UL);

            return new ParalivesOccupationScheduleTypeSnapshot
            {
                Exists = true,
                ScheduleGuid = schedule.GUID,
                DisplayName = schedule.DisplayName ?? string.Empty,
                PossibleDays = CreateDaysSnapshots(schedule.PossibleOccupiedDays),
                PossibleHours = CreateHoursSnapshots(schedule.PossibleOccupiedHours)
            };
        }

        private static ParalivesOccupationScheduleTypeSnapshot CreateMissingSchedule(ulong scheduleGuid)
        {
            return new ParalivesOccupationScheduleTypeSnapshot
            {
                ScheduleGuid = scheduleGuid,
                DisplayName = string.Empty,
                PossibleDays = new ParalivesOccupationScheduleDaysOption[0],
                PossibleHours = new ParalivesOccupationScheduleHoursOption[0]
            };
        }

        private static ParalivesAssignedOccupationScheduleSnapshot CreateMissingAssignedSchedule(
            ulong characterGuid,
            int occupationIndex)
        {
            return new ParalivesAssignedOccupationScheduleSnapshot
            {
                CharacterGuid = characterGuid,
                OccupationIndex = occupationIndex,
                ChosenHours = new ParalivesOccupationScheduleHoursOption(),
                DayOfWeek = global::ParaTime.DayOfWeek,
                MinutesOfDay = global::ParaTime.MinutesOfDay
            };
        }

        private static ParalivesOccupationScheduleDaysOption[] CreateDaysSnapshots(
            EnumAndGuid<ScheduleDaysOfWeek>[] days)
        {
            if (days == null || days.Length == 0)
                return new ParalivesOccupationScheduleDaysOption[0];

            List<ParalivesOccupationScheduleDaysOption> snapshots =
                new List<ParalivesOccupationScheduleDaysOption>();
            for (int i = 0; i < days.Length; i++)
            {
                EnumAndGuid<ScheduleDaysOfWeek> option = days[i];
                if (option == null)
                    continue;

                snapshots.Add(new ParalivesOccupationScheduleDaysOption
                {
                    Guid = option.GUID,
                    Days = option.Value
                });
            }

            return snapshots.ToArray();
        }

        private static ParalivesOccupationScheduleHoursOption[] CreateHoursSnapshots(OccupiedHours[] hours)
        {
            if (hours == null || hours.Length == 0)
                return new ParalivesOccupationScheduleHoursOption[0];

            List<ParalivesOccupationScheduleHoursOption> snapshots =
                new List<ParalivesOccupationScheduleHoursOption>();
            for (int i = 0; i < hours.Length; i++)
            {
                ParalivesOccupationScheduleHoursOption option = CreateHoursOption(hours[i]);
                if (option != null)
                    snapshots.Add(option);
            }

            return snapshots.ToArray();
        }

        private static ParalivesOccupationScheduleHoursOption CreateHoursOption(OccupiedHours hours)
        {
            if (hours == null)
                return new ParalivesOccupationScheduleHoursOption();

            return new ParalivesOccupationScheduleHoursOption
            {
                Guid = hours.GUID,
                StartTime = hours.StartTime,
                Duration = hours.Duration,
                IsDefault = hours.IsDefault
            };
        }

        private static void ReadScheduleWindow(
            global::AssetCharacter character,
            int occupationIndex,
            out bool isInScheduledDay,
            out bool isInScheduledHours)
        {
            isInScheduledDay = false;
            isInScheduledHours = false;

            try
            {
                if (character == null || global::OccupationsManager.Instance == null)
                    return;

                var schedule = global::OccupationsManager.Instance.CheckIfInSchedule(
                    character,
                    occupationIndex,
                    global::ParaTime.DayOfWeek,
                    global::ParaTime.MinutesOfDay,
                    false,
                    true);
                isInScheduledDay = schedule.Item1;
                isInScheduledHours = schedule.Item2;
            }
            catch
            {
                isInScheduledDay = false;
                isInScheduledHours = false;
            }
        }

        private static Occupations GetOccupationsOrNull()
        {
            try
            {
                if (global::Settings.Instance == null)
                    return null;

                return global::Settings.Get<Occupations>();
            }
            catch
            {
                return null;
            }
        }

        private static bool ContainsScheduleType(OccupationScheduleType[] schedules, ulong guid)
        {
            if (schedules == null)
                return false;

            for (int i = 0; i < schedules.Length; i++)
            {
                if (schedules[i] != null && schedules[i].GUID == guid)
                    return true;
            }

            return false;
        }

        private static void Upsert(List<OccupationScheduleType> schedules, OccupationScheduleType schedule)
        {
            for (int i = 0; i < schedules.Count; i++)
            {
                if (schedules[i] != null && schedules[i].GUID == schedule.GUID)
                {
                    schedules[i] = schedule;
                    return;
                }
            }

            schedules.Add(schedule);
        }

        private static T[] Append<T>(T[] source, T item)
        {
            int length = source != null ? source.Length : 0;
            T[] result = new T[length + 1];
            if (length > 0)
                Array.Copy(source, result, length);

            result[length] = item;
            return result;
        }
    }
}
