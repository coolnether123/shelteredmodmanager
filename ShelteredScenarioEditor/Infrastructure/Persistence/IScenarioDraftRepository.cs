namespace ShelteredScenarioEditor.Infrastructure.Persistence{
    internal interface IScenarioDraftRepository
    {
        string ResolveDraftPath(string draftId);
    }
}
