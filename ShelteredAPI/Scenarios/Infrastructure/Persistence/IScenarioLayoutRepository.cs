namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioLayoutRepository
    {
        string Load();
        void Save(string xml);
    }
}
