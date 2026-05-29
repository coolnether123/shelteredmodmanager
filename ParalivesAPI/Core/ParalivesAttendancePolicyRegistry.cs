using System;
using System.Collections.Generic;
using ModAPI.Core;
using Setting;

namespace ParalivesAPI.Core
{
    public delegate bool? ParalivesAttendancePolicy(ParalivesOccupationScheduleContext context);

    public sealed class ParalivesOccupationScheduleContext
    {
        public global::AssetCharacter Character { get; internal set; }

        public ulong CharacterGuid { get; internal set; }

        public int OccupationIndex { get; internal set; }

        public global::AssetCharacterOccupationData OccupationData { get; internal set; }

        public Occupation Occupation { get; internal set; }

        public ulong OccupationGuid { get; internal set; }

        public bool IsSchool { get; internal set; }

        public bool OriginalShouldAttend { get; internal set; }

        public float TotalMinutes { get; internal set; }

        public int Day { get; internal set; }

        public int DayOfWeek { get; internal set; }

        public float MinutesOfDay { get; internal set; }
    }

    public sealed class ParalivesAttendancePolicyRegistry
    {
        private readonly object _sync = new object();
        private readonly List<ParalivesAttendancePolicy> _policies = new List<ParalivesAttendancePolicy>();
        private readonly ParalivesOccupationFacade _occupations;

        internal ParalivesAttendancePolicyRegistry(ParalivesOccupationFacade occupations)
        {
            _occupations = occupations;
        }

        public int RegisteredPolicyCount
        {
            get { lock (_sync) return _policies.Count; }
        }

        public IDisposable Register(ParalivesAttendancePolicy policy)
        {
            if (policy == null)
                throw new ArgumentNullException("policy");

            lock (_sync)
            {
                if (!_policies.Contains(policy))
                    _policies.Add(policy);
            }

            return new Registration(this, policy);
        }

        public bool Unregister(ParalivesAttendancePolicy policy)
        {
            if (policy == null)
                return false;

            lock (_sync)
                return _policies.Remove(policy);
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

            ParalivesAttendancePolicy[] policies;
            lock (_sync)
                policies = _policies.ToArray();

            if (policies.Length == 0)
                return false;

            ParalivesOccupationScheduleContext context = CreateContext(character, occupationIndex, originalShouldAttend);
            if (context == null)
                return false;

            for (int i = 0; i < policies.Length; i++)
            {
                ParalivesAttendancePolicy policy = policies[i];
                if (policy == null)
                    continue;

                try
                {
                    bool? result = policy(context);
                    if (result.HasValue)
                    {
                        shouldAttend = result.Value;
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce(
                        "ParalivesAttendancePolicy." + i,
                        "Paralives attendance policy failed: " + ex.Message);
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

            return new ParalivesOccupationScheduleContext
            {
                Character = character,
                CharacterGuid = character.GUID,
                OccupationIndex = occupationIndex,
                OccupationData = data,
                Occupation = occupation,
                OccupationGuid = data.Occupation,
                IsSchool = occupation != null && occupation.Type == SchoolJobTypes.School,
                OriginalShouldAttend = originalShouldAttend,
                TotalMinutes = global::ParaTime.TotalMinutes,
                Day = global::ParaTime.Day,
                DayOfWeek = global::ParaTime.DayOfWeek,
                MinutesOfDay = global::ParaTime.MinutesOfDay
            };
        }

        private sealed class Registration : IDisposable
        {
            private ParalivesAttendancePolicyRegistry _registry;
            private ParalivesAttendancePolicy _policy;

            public Registration(ParalivesAttendancePolicyRegistry registry, ParalivesAttendancePolicy policy)
            {
                _registry = registry;
                _policy = policy;
            }

            public void Dispose()
            {
                ParalivesAttendancePolicyRegistry registry = _registry;
                ParalivesAttendancePolicy policy = _policy;
                _registry = null;
                _policy = null;

                if (registry != null)
                    registry.Unregister(policy);
            }
        }
    }
}
