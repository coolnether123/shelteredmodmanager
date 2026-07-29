namespace ParalivesAPI.Stable
{
    public interface IParalivesUi
    {
        bool OpenOccupationsForCharacter(ulong characterGuid);

        bool OpenOccupationsForCharacter(ulong characterGuid, int playerIndex);
    }
}
