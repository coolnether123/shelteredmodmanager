using ModAPI.Scenarios;
using ShelteredAPI.Scenarios.Definitions;

namespace ShelteredAPI.Scenarios.Domain.Validation{
    internal sealed class ScenarioStoryFlowValidationRule : IScenarioValidationRule
    {
        private readonly ScenarioStoryFlowValidationAnalyzer _analyzer;

        public ScenarioStoryFlowValidationRule()
            : this(new ScenarioStoryFlowValidationAnalyzer())
        {
        }

        public ScenarioStoryFlowValidationRule(ScenarioStoryFlowValidationAnalyzer analyzer)
        {
            _analyzer = analyzer ?? new ScenarioStoryFlowValidationAnalyzer();
        }

        public void Validate(ScenarioDefinition definition, string scenarioFilePath, ValidationSummary summary)
        {
            if (summary == null)
                return;

            ScenarioStoryFlowIssue[] issues = _analyzer.Analyze(definition);
            for (int i = 0; issues != null && i < issues.Length; i++)
            {
                ScenarioStoryFlowIssue issue = issues[i];
                if (issue == null)
                    continue;
                if (issue.Severity == ScenarioIssueSeverity.Error)
                    summary.AddError(issue.Code, issue.Message);
                else
                    summary.AddWarning(issue.Code, issue.Message);
            }
        }
    }
}
