using ParalivesAPI.Core;

namespace ParalivesAPI.Stable
{
    public interface IParalivesOccupationUnlockables
    {
        ParalivesOccupationUnlockableReadResult ReadUnlockables(ulong characterGuid, int occupationIndex);

        ParalivesOccupationUnlockableReadResult ReadExpertises(ulong characterGuid, int occupationIndex);

        ParalivesOccupationUnlockableReadResult ReadExtras(ulong characterGuid, int occupationIndex);

        ParalivesOccupationUnlockableReadResult ReadPendingUpgrades(ulong characterGuid, int occupationIndex);

        ParalivesOccupationUnlockableMutationResult SetExpertiseLevel(
            ulong characterGuid,
            int occupationIndex,
            ulong unlockableGuid,
            int level);

        ParalivesOccupationUnlockableMutationResult GrantExtra(
            ulong characterGuid,
            int occupationIndex,
            ulong unlockableGuid);

        ParalivesOccupationUnlockableMutationResult RemoveExpertise(
            ulong characterGuid,
            int occupationIndex,
            ulong unlockableGuid);

        ParalivesOccupationUnlockableMutationResult ClearPendingUpgrades(ulong characterGuid, int occupationIndex);

        ParalivesOccupationUnlockableMutationResult CompletePendingUpgrade(
            ulong characterGuid,
            int occupationIndex,
            ulong unlockableGuid);
    }
}
