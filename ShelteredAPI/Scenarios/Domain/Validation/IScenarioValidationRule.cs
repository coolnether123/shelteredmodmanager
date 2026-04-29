using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal interface IScenarioValidationRule
    {
        void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary);
    }
}
