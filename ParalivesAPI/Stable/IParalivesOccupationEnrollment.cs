using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesOccupationEnrollment
    {
        bool TryGetActive(
            ulong characterGuid,
            ulong occupationGuid,
            out ParalivesOccupationSnapshot snapshot);

        bool TryGetActiveByKind(
            ulong characterGuid,
            ParalivesOccupationKind occupationKind,
            out ParalivesOccupationSnapshot snapshot);

        ParalivesOccupationEnrollmentResult TryEnroll(
            ulong characterGuid,
            ulong occupationGuid,
            ParalivesOccupationEnrollmentOptions options);

        ParalivesOccupationEnrollmentResult TryUnenroll(
            ulong characterGuid,
            int occupationIndex);

        ParalivesOccupationEnrollmentResult TrySwap(
            ulong characterGuid,
            ulong fromOccupationGuid,
            ulong toOccupationGuid,
            out ParalivesOccupationRestoreToken restoreToken);

        ParalivesOccupationEnrollmentResult TryRestore(
            ulong characterGuid,
            ParalivesOccupationRestoreToken restoreToken);
    }
}
