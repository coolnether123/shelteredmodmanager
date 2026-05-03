using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    public enum ScenarioIssueSeverity
    {
        Warning = 0,
        Error = 1
    }

    public sealed class ScenarioValidationIssue
    {
        public ScenarioValidationIssue(ScenarioIssueSeverity severity, string message)
        {
            Severity = severity;
            Message = message ?? string.Empty;
        }

        public ScenarioIssueSeverity Severity { get; private set; }
        public string Message { get; private set; }
    }

    public sealed class ScenarioValidationResult
    {
        private readonly List<ScenarioValidationIssue> _issues = new List<ScenarioValidationIssue>();

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

        public ScenarioValidationIssue[] Issues
        {
            get { return _issues.ToArray(); }
        }

        public void AddError(string message)
        {
            _issues.Add(new ScenarioValidationIssue(ScenarioIssueSeverity.Error, message));
        }

        public void AddWarning(string message)
        {
            _issues.Add(new ScenarioValidationIssue(ScenarioIssueSeverity.Warning, message));
        }
    }
}
