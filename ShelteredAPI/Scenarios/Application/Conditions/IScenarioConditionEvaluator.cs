using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Domain.Conditions;
using ShelteredAPI.Scenarios.Domain.Runtime;
namespace ShelteredAPI.Scenarios.Application.Conditions{
    internal interface IScenarioConditionEvaluator
    {
        bool CanEvaluate(ScenarioConditionKind kind);
        bool IsSatisfied(ScenarioDefinition definition, ScenarioConditionRef condition, ScenarioRuntimeState state, out string reason);
    }
}
