using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesOccupations
    {
        IParalivesOccupationRegistry Registry { get; }

        IParalivesOccupationEnrollment Enrollment { get; }

        IParalivesOccupationSchedules Schedules { get; }

        IParalivesOccupationTasks Tasks { get; }

        IParalivesOccupationUnlockables Unlockables { get; }

        IParalivesOccupationAttendancePolicies AttendancePolicies { get; }

        IParalivesOccupationPanelProviders PanelProviders { get; }

        bool IsSchool(ulong occupationGuid);

        int[] GetActiveOccupationIndexes(ulong characterGuid);

        ParalivesOccupationSnapshot ReadSnapshot(ulong characterGuid, int occupationIndex);

        bool TryReadSnapshot(
            ulong characterGuid,
            int occupationIndex,
            out ParalivesOccupationSnapshot snapshot);

        ParalivesOccupationSnapshot[] ReadActiveSnapshots(ulong characterGuid);
    }
}
