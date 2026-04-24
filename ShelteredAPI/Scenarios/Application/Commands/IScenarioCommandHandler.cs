namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioCommandHandler
    {
        bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message);
    }
}
