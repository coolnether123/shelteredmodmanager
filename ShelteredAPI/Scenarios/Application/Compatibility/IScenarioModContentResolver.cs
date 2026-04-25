namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioModContentResolver
    {
        bool TryResolveOwner(string contentId, out string modId, out string version);
        bool IsLoaded(string modId);
        string GetLoadedVersion(string modId);
    }
}
