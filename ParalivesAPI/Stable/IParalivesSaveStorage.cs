namespace ParalivesAPI.Stable
{
    public interface IParalivesSaveStorage
    {
        bool TryGetCurrentSaveId(out string saveId);

        bool TryGetCurrentSaveDisplayName(out string displayName);

        bool TryGetModStoragePath(string modId, out string path);
    }
}
