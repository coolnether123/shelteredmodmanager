using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public sealed class ValidationSummary
    {
        private readonly List<ValidationIssue> _issues = new List<ValidationIssue>();

        public ValidationIssue[] Issues
        {
            get { return _issues.ToArray(); }
        }

        public bool IsValid
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i] != null && _issues[i].Severity == ScenarioIssueSeverity.Error)
                        return false;
                }

                return true;
            }
        }

        public void AddError(string code, string message)
        {
            _issues.Add(new ValidationIssue(ScenarioIssueSeverity.Error, code, message));
        }

        public void AddWarning(string code, string message)
        {
            _issues.Add(new ValidationIssue(ScenarioIssueSeverity.Warning, code, message));
        }

        public ScenarioValidationResult ToLegacyResult()
        {
            ScenarioValidationResult result = new ScenarioValidationResult();
            for (int i = 0; i < _issues.Count; i++)
            {
                ValidationIssue issue = _issues[i];
                if (issue == null)
                    continue;

                if (issue.Severity == ScenarioIssueSeverity.Error)
                    result.AddError(issue.Message);
                else
                    result.AddWarning(issue.Message);
            }

            return result;
        }
    }
}
