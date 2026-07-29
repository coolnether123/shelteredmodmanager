namespace ParalivesAPI.Stable
{
    public interface IParalivesCharacters
    {
        ulong[] GetCurrentHouseholdCharacterGuids();

        string GetDisplayName(ulong characterGuid);

        bool IsInCurrentHousehold(ulong characterGuid);

        bool IsAvailableForGameplay(ulong characterGuid);
    }
}
