namespace ShelteredScenarioEditor.Infrastructure.Persistence{
    internal interface IScenarioLayoutRepository
    {
        string Load();
        void Save(string xml);
    }
}
