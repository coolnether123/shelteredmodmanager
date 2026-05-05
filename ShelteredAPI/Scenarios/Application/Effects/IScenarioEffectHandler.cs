using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Effects;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Application.Effects{
    internal interface IScenarioEffectHandler
    {
        bool CanHandle(ScenarioEffectKind kind);
        bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message);
    }
}
