using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Domain.Validation
{
    internal sealed class LaunchSetupValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            ScenarioLaunchSetupDefinition setup = definition != null ? definition.LaunchSetup : null;
            if (setup == null || setup.Categories == null)
                return;

            for (int i = 0; i < setup.Categories.Count; i++)
            {
                ScenarioDifficultyCategoryDefinition category = setup.Categories[i];
                if (category == null || !ScenarioDifficultyCategoryIds.IsKnown(category.Id))
                {
                    summary.AddWarning("launch-setup-unknown-category", "Unknown launch difficulty category '"
                        + (category != null ? category.Id : string.Empty) + "' will be ignored at runtime.");
                    continue;
                }

                int maximum = category.Id == ScenarioDifficultyCategoryIds.MapSize ? 2
                    : category.Id == ScenarioDifficultyCategoryIds.Fog ? 1 : 3;
                if (category.AuthoredValue < 0 || category.AuthoredValue > maximum)
                    summary.AddWarning("launch-setup-value-range", "Launch difficulty category '" + category.Id
                        + "' has value " + category.AuthoredValue + "; runtime will clamp it to 0-" + maximum + ".");
            }
        }
    }
}
