using ShelteredAPI.Scenarios.Application.Authoring;

namespace ShelteredAPI.Scenarios.Application.Commands{
    internal interface IScenarioCommandHandler
    {
        bool TryHandle(ScenarioAuthoringState state, string actionId, out bool handled, out string message);
    }
}
