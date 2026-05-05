using System.Collections.Generic;

namespace ModAPI.Scenarios
{
    /// <summary>
    /// Severity for a scenario validation issue.
    /// Errors block publication or loading; warnings describe risky but allowed content.
    /// </summary>
    public enum ScenarioIssueSeverity
    {
        Warning = 0,
        Error = 1
    }

    /// <summary>
    /// One validation message produced while checking a scenario definition.
    /// </summary>
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

    /// <summary>
    /// Mutable validation result used by validators to collect warnings and errors.
    /// Callers should inspect <see cref="IsValid"/> before launching or publishing a scenario.
    /// </summary>
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
