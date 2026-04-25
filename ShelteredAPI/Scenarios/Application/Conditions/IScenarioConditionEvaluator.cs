using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioConditionEvaluator
    {
        bool CanEvaluate(ScenarioConditionKind kind);
        bool IsSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason);
    }
}
