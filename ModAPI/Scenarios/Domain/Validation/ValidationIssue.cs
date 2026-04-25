namespace ModAPI.Scenarios
{
    public sealed class ValidationIssue
    {
        public ValidationIssue(ScenarioIssueSeverity severity, string code, string message)
        {
            Severity = severity;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ScenarioIssueSeverity Severity { get; private set; }
        public string Code { get; private set; }
        public string Message { get; private set; }
    }
}
