using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    public interface IScenarioValidationRule
    {
        void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary);
    }
}
