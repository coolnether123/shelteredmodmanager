namespace ModAPI.Saves
{
    /// <summary>
    /// Defines the common contract for a save registry, allowing the PlatformSaveProxy
    /// to interact with both ExpandedVanillaSaves and ScenarioSaves in a generic way.
    /// </summary>
    internal interface ISaveApi
    {
        SaveEntry Get(string saveId);
        SaveEntry Overwrite(string saveId, SaveOverwriteOptions opts, byte[] xmlBytes);
        SaveEntry[] ListSaves(int page, int pageSize);
    }
}