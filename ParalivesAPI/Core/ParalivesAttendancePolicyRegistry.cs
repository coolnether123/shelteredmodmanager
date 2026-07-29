using System;
using System.Collections.Generic;
using ModAPI.Core;
using ParalivesAPI.Stable;
using Setting;

namespace ParalivesAPI.Core
{
    public delegate bool? ParalivesAttendancePolicy(ParalivesOccupationScheduleContext context);

    public delegate ParalivesOccupationAttendanceDecision ParalivesOccupationAttendanceDecisionPolicy(
        ParalivesOccupationScheduleContext context);

    public enum ParalivesOccupationAttendanceDecision
    {
        UseGameDefault = 0,
        AttendNormally = 1,
        SuppressTravel = 2,
        SkipToday = 3,
        WorkRemotely = 4
    }

    public sealed class ParalivesOccupationScheduleContext
    {
        public global::AssetCharacter Character { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public global::AssetCharacterOccupationData OccupationData { get; internal set; }

        public Occupation Occupation { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public bool IsKnownOccupation { get; internal set; }

        public SchoolJobTypes OccupationType { get; internal set; }

        public bool IsSchool { get; internal set; }

        public bool IsActive { get; internal set; }

        public ulong ScheduleGuid { get; internal set; }

        public ScheduleDaysOfWeek ChosenScheduledDays { get; internal set; }

        public float ScheduledStartTime { get; internal set; }

        public float ScheduledDuration { get; internal set; }

        public float TravelDuration { get; internal set; }

        public bool OriginalShouldAttend { get; internal set; }

        public bool IsInScheduledDay { get; internal set; }

        public bool IsInScheduledHours { get; internal set; }

        public int CurrentOccupationIndex { get; internal set; }

        public int OccupationIndexToGoTo { get; internal set; }

        public float TotalMinutes { get; internal set; }

        public int Day { get; internal set; }

        public int DayOfWeek { get; internal set; }

        public float MinutesOfDay { get; internal set; }
    }

    public sealed class ParalivesAttendancePolicyRegistry : IParalivesOccupationAttendancePolicies
    {
        private readonly object _sync = new object();
        private readonly List<RegisteredPolicy> _policies = new List<RegisteredPolicy>();
        private readonly ParalivesOccupationFacade _occupations;

        internal ParalivesAttendancePolicyRegistry(ParalivesOccupationFacade occupations)
        {
            _occupations = occupations;
        }

        public int RegisteredPolicyCount
        {
            get { lock (_sync) return _policies.Count; }
        }

        public int RegisteredLegacyPolicyCount
        {
            get
            {
                lock (_sync)
                    return CountPolicies(PolicyKind.Legacy);
            }
        }

        public int RegisteredDecisionPolicyCount
        {
            get
            {
                lock (_sync)
                    return CountPolicies(PolicyKind.Decision);
            }
        }

        public IDisposable Register(ParalivesAttendancePolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException("policy");

            RegisteredPolicy entry;
            lock (_sync)
            {
                entry = Find(policy);
                if (entry == null)
                {
                    entry = RegisteredPolicy.ForLegacy(policy);
                    _policies.Add(entry);
                }
            }

            return new Registration(this, entry);
        }

        public IDisposable Register(ParalivesOccupationAttendanceDecisionPolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException("policy");

            RegisteredPolicy entry;
            lock (_sync)
            {
                entry = Find(policy);
                if (entry == null)
                {
                    entry = RegisteredPolicy.ForDecision(policy);
                    _policies.Add(entry);
                }
            }

            return new Registration(this, entry);
        }

        public bool Unregister(ParalivesAttendancePolicy policy)
        {
            if (policy == null)
                return false;

            lock (_sync)
            {
                RegisteredPolicy entry = Find(policy);
                return entry != null && _policies.Remove(entry);
            }
        }

        public bool Unregister(ParalivesOccupationAttendanceDecisionPolicy policy)
        {
            if (policy == null)
                return false;

            lock (_sync)
            {
                RegisteredPolicy entry = Find(policy);
                return entry != null && _policies.Remove(entry);
            }
        }

        internal bool TryResolve(
            global::AssetCharacter character,
            int occupationIndex,
            bool originalShouldAttend,
            out bool shouldAttend)
        {
            shouldAttend = originalShouldAttend;
            if (character == null)
                return false;

            RegisteredPolicy[] policies;
            lock (_sync)
                policies = _policies.ToArray();

            if (policies.Length == 0)
                return false;

            ParalivesOccupationScheduleContext context = CreateContext(character, occupationIndex, originalShouldAttend);
            if (context == null)
                return false;

            for (int i = 0; i < policies.Length; i++)
            {
                RegisteredPolicy policy = policies[i];
                if (policy == null)
                    continue;

                try
                {
                    ParalivesOccupationAttendanceDecision decision;
                    if (policy.Kind == PolicyKind.Legacy)
                    {
                        bool? result = policy.LegacyPolicy(context);
                        if (!result.HasValue)
                            continue;

                        decision = result.Value
                            ? ParalivesOccupationAttendanceDecision.AttendNormally
                            : ParalivesOccupationAttendanceDecision.SuppressTravel;
                    }
                    else
                    {
                        decision = policy.DecisionPolicy(context);
                    }

                    if (TryApplyDecision(decision, context, out shouldAttend))
                        return true;
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce(
                        "ParalivesAttendancePolicy." + policy.Kind + "." + i,
                        "Paralives occupation attendance policy failed: " + ex.Message);
                }
            }

            return false;
        }

        private ParalivesOccupationScheduleContext CreateContext(
            global::AssetCharacter character,
            int occupationIndex,
            bool originalShouldAttend)
        {
            global::AssetCharacterOccupationData data;
            if (!_occupations.TryGetOccupationData(character, occupationIndex, out data))
                return null;

            Occupation occupation;
            _occupations.TryGetOccupation(character, occupationIndex, out occupation);

            bool isInScheduledDay = false;
            bool isInScheduledHours = false;
            try
            {
                if (global::OccupationsManager.Instance != null)
                {
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
            }
            catch
            {
                isInScheduledDay = false;
                isInScheduledHours = false;
            }

            OccupiedHours hours = data.ChosenScheduleHours;
            bool hasCharacterData = character.Data != null;

            return new ParalivesOccupationScheduleContext
            {
                Character = character,
                CharacterGuid = character.GUID,
                OccupationIndex = occupationIndex,
                OccupationData = data,
                Occupation = occupation,
                OccupationGuid = data.Occupation,
                IsKnownOccupation = occupation != null,
                OccupationType = occupation != null ? occupation.Type : default(SchoolJobTypes),
                IsSchool = occupation != null && occupation.Type == SchoolJobTypes.School,
                IsActive = data.IsActive,
                ScheduleGuid = occupation != null ? occupation.Schedule : 0UL,
                ChosenScheduledDays = data.ChosenScheduledDays,
                ScheduledStartTime = hours != null ? hours.StartTime : 0f,
                ScheduledDuration = hours != null ? hours.Duration : 0f,
                TravelDuration = occupation != null ? occupation.TravelDuration : 0f,
                OriginalShouldAttend = originalShouldAttend,
                IsInScheduledDay = isInScheduledDay,
                IsInScheduledHours = isInScheduledHours,
                CurrentOccupationIndex = hasCharacterData ? character.Data.CurrentOccupationIndex : -1,
                OccupationIndexToGoTo = hasCharacterData ? character.Data.OccupationIndexToGoTo : -1,
                TotalMinutes = global::ParaTime.TotalMinutes,
                Day = global::ParaTime.Day,
                DayOfWeek = global::ParaTime.DayOfWeek,
                MinutesOfDay = global::ParaTime.MinutesOfDay
            };
        }

        private bool TryApplyDecision(
            ParalivesOccupationAttendanceDecision decision,
            ParalivesOccupationScheduleContext context,
            out bool shouldAttend)
        {
            shouldAttend = context != null && context.OriginalShouldAttend;

            switch (decision)
            {
                case ParalivesOccupationAttendanceDecision.UseGameDefault:
                    return false;
                case ParalivesOccupationAttendanceDecision.AttendNormally:
                    shouldAttend = true;
                    return true;
                case ParalivesOccupationAttendanceDecision.SuppressTravel:
                    shouldAttend = false;
                    return true;
                case ParalivesOccupationAttendanceDecision.SkipToday:
                    shouldAttend = false;
                    TryMarkSkippedToday(context);
                    return true;
                case ParalivesOccupationAttendanceDecision.WorkRemotely:
                    shouldAttend = false;
                    return true;
                default:
                    return false;
            }
        }

        private void TryMarkSkippedToday(ParalivesOccupationScheduleContext context)
        {
            if (context == null || context.Character == null)
                return;

            try
            {
                _occupations.SuppressAttendanceToday(context.Character, context.OccupationIndex);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ParalivesAttendancePolicy.SkipToday",
                    "Failed to mark occupation skipped today: " + ex.Message);
            }
        }

        private int CountPolicies(PolicyKind kind)
        {
            int count = 0;
            for (int i = 0; i < _policies.Count; i++)
            {
                if (_policies[i] != null && _policies[i].Kind == kind)
                    count++;
            }

            return count;
        }

        private RegisteredPolicy Find(ParalivesAttendancePolicy policy)
        {
            for (int i = 0; i < _policies.Count; i++)
            {
                RegisteredPolicy entry = _policies[i];
                if (entry != null && entry.Kind == PolicyKind.Legacy && entry.LegacyPolicy == policy)
                    return entry;
            }

            return null;
        }

        private RegisteredPolicy Find(ParalivesOccupationAttendanceDecisionPolicy policy)
        {
            for (int i = 0; i < _policies.Count; i++)
            {
                RegisteredPolicy entry = _policies[i];
                if (entry != null && entry.Kind == PolicyKind.Decision && entry.DecisionPolicy == policy)
                    return entry;
            }

            return null;
        }

        private bool Unregister(RegisteredPolicy policy)
        {
            if (policy == null)
                return false;

            lock (_sync)
                return _policies.Remove(policy);
        }

        private enum PolicyKind
        {
            Legacy,
            Decision
        }

        private sealed class RegisteredPolicy
        {
            public PolicyKind Kind;
            public ParalivesAttendancePolicy LegacyPolicy;
            public ParalivesOccupationAttendanceDecisionPolicy DecisionPolicy;

            public static RegisteredPolicy ForLegacy(ParalivesAttendancePolicy policy)
            {
                return new RegisteredPolicy
                {
                    Kind = PolicyKind.Legacy,
                    LegacyPolicy = policy
                };
            }

            public static RegisteredPolicy ForDecision(ParalivesOccupationAttendanceDecisionPolicy policy)
            {
                return new RegisteredPolicy
                {
                    Kind = PolicyKind.Decision,
                    DecisionPolicy = policy
                };
            }
        }

        private sealed class Registration : IDisposable
        {
            private ParalivesAttendancePolicyRegistry _registry;
            private RegisteredPolicy _policy;

            public Registration(ParalivesAttendancePolicyRegistry registry, RegisteredPolicy policy)
            {
                _registry = registry;
                _policy = policy;
            }

            public void Dispose()
            {
                ParalivesAttendancePolicyRegistry registry = _registry;
                RegisteredPolicy policy = _policy;
                _registry = null;
                _policy = null;

                if (registry != null)
                    registry.Unregister(policy);
            }
        }
    }
}
