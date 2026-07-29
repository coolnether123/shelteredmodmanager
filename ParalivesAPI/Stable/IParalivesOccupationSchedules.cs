using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesOccupationSchedules
    {
        int RegisteredScheduleCount { get; }

        void RegisterSchedule(ParalivesOccupationScheduleDefinition definition);

        bool TryRegisterSchedule(ParalivesOccupationScheduleDefinition definition, out string message);

        bool EnsureRegistered();

        bool TryReadSchedule(ulong scheduleGuid, out ParalivesOccupationScheduleTypeSnapshot snapshot);

        ParalivesOccupationScheduleTypeSnapshot ReadSchedule(ulong scheduleGuid);

        ParalivesOccupationScheduleTypeSnapshot[] ReadSchedules();

        bool TryReadAssignedSchedule(
            ulong characterGuid,
            int occupationIndex,
            out ParalivesAssignedOccupationScheduleSnapshot snapshot);

        ParalivesAssignedOccupationScheduleSnapshot ReadAssignedSchedule(
            ulong characterGuid,
            int occupationIndex);
    }
}
