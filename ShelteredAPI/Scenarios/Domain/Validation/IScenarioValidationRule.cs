using ModAPI.Scenarios;

using ShelteredAPI.Scenarios.Definitions;
namespace ShelteredAPI.Scenarios.Domain.Validation{
    internal interface IScenarioValidationRule
    {
        void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary);
    }
}
