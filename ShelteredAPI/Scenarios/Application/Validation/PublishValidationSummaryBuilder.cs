using System.Collections.Generic;
using ModAPI.Scenarios;

namespace ShelteredAPI.Scenarios
{
    internal sealed class PublishValidationSummaryBuilder
    {
        public string Build(ValidationSummary summary)
        {
            if (summary == null)
                return "Validation did not run.";

            List<string> lines = new List<string>();
            ValidationIssue[] issues = summary.Issues;
            if (issues.Length == 0)
                return "Validation passed with no issues.";

            for (int i = 0; i < issues.Length; i++)
            {
                ValidationIssue issue = issues[i];
                if (issue != null)
                    lines.Add(issue.Severity + ": " + issue.Message);
            }

            return string.Join(" ", lines.ToArray());
        }
    }
}
