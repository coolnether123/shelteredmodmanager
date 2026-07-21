using System;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Domain.Validation
{
    internal sealed class ScenarioMetadataValidationRule : IScenarioValidationRule
    {
        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            if (definition == null || summary == null)
                return;

            string title = Trim(definition.DisplayName);
            if (string.IsNullOrEmpty(title))
                summary.AddError("metadata.title.required", "Scenario metadata needs a title before export.");
            else if (string.Equals(title, ScenarioMetadataDefaults.DefaultTitle, StringComparison.OrdinalIgnoreCase))
                summary.AddWarning("metadata.title.placeholder", "Scenario metadata still uses the placeholder title 'Untitled Scenario'.");

            if (string.Equals(Trim(definition.Author), ScenarioMetadataDefaults.DefaultAuthor, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(Trim(definition.Author)))
                summary.AddWarning("metadata.author.placeholder", "Scenario metadata still lists the author as 'unknown'.");
            if (string.IsNullOrEmpty(Trim(definition.Description)))
                summary.AddWarning("metadata.description.empty", "Scenario metadata has no description for people you share it with.");
            if (string.Equals(Trim(definition.Version), ScenarioMetadataDefaults.DefaultVersion, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrEmpty(Trim(definition.Version)))
                summary.AddWarning("metadata.version.default", "Scenario metadata still uses the default version 0.1.0.");
        }

        private static string Trim(string value)
        {
            return value == null ? string.Empty : value.Trim();
        }
    }
}
