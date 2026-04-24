namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioSettingsRepository
    {
        string Load();
        void Save(string xml);
    }
}
