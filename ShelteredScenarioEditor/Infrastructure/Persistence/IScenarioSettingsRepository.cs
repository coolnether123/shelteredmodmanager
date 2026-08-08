namespace ShelteredScenarioEditor.Infrastructure.Persistence{
    internal interface IScenarioSettingsRepository
    {
        string Load();
        void Save(string xml);
    }
}
