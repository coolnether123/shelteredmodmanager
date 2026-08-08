using ShelteredScenarioEditor.Application.Runtime;
using System;
using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredScenarioEditor.Application.Authoring{
    internal sealed class ScenarioAuthoringValidationSnapshot
    {
        public ScenarioValidationResult Result { get; private set; }
        public bool ValidationAvailable { get; private set; }
        public string UnavailableReason { get; private set; }
        public int ErrorCount { get; private set; }
        public int WarningCount { get; private set; }

        public bool IsBlocked
        {
            get { return !ValidationAvailable || ErrorCount > 0; }
        }

        public ScenarioValidationIssue[] Issues
        {
            get { return Result != null && Result.Issues != null ? Result.Issues : new ScenarioValidationIssue[0]; }
        }

        public static ScenarioAuthoringValidationSnapshot Evaluate(
            IScenarioDefinitionValidator validator,
            ScenarioDefinition definition,
            string scenarioFilePath)
        {
            ScenarioAuthoringValidationSnapshot snapshot = new ScenarioAuthoringValidationSnapshot();
            if (definition == null)
            {
                snapshot.ValidationAvailable = false;
                snapshot.UnavailableReason = "No active scenario definition.";
                return snapshot;
            }

            try
            {
                snapshot.Result = validator != null ? validator.Validate(definition, scenarioFilePath) : null;
                if (snapshot.Result == null)
                {
                    snapshot.ValidationAvailable = false;
                    snapshot.UnavailableReason = "Validation service returned no result.";
                    return snapshot;
                }

                snapshot.ValidationAvailable = true;
                ScenarioValidationIssue[] issues = snapshot.Result.Issues;
                for (int i = 0; issues != null && i < issues.Length; i++)
                {
                    ScenarioValidationIssue issue = issues[i];
                    if (issue == null)
                        continue;
                    if (issue.Severity == ScenarioIssueSeverity.Error)
                        snapshot.ErrorCount++;
                    else
                        snapshot.WarningCount++;
                }
            }
            catch (Exception ex)
            {
                snapshot.ValidationAvailable = false;
                snapshot.UnavailableReason = "Validation could not run: " + ex.Message;
            }

            return snapshot;
        }
    }
}
