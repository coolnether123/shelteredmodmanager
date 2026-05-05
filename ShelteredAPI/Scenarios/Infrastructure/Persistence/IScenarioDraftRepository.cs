namespace ShelteredAPI.Scenarios.Infrastructure.Persistence{
    internal interface IScenarioDraftRepository
    {
        string ResolveDraftPath(string draftId);
    }
}
