using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioEffectHandler
    {
        bool CanHandle(ScenarioEffectKind kind);
        bool Handle(ScenarioDefinition definition, ScenarioEffectDefinition effect, ScenarioRuntimeState state, out string message);
    }
}
