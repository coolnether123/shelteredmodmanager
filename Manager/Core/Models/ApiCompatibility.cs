using System;
using System.Collections.Generic;

namespace Manager.Core.Models
{
    public enum ApiCompatibilitySeverity
    {
        None,
        Info,
        Warning,
        Error
    }

    public class ApiRequirement
    {
        public string ApiName { get; set; }
        public string RequiredVersion { get; set; }
        public string SourceAssembly { get; set; }
        public ApiCompatibilitySeverity Severity { get; set; }
        public string Message { get; set; }

        public bool IsCompatible
        {
            get { return Severity != ApiCompatibilitySeverity.Error; }
        }

        public ApiRequirement()
        {
            ApiName = string.Empty;
            RequiredVersion = string.Empty;
            SourceAssembly = string.Empty;
            Message = string.Empty;
            Severity = ApiCompatibilitySeverity.None;
        }
    }

    public class ApiCompatibilityReport
    {
        private readonly List<ApiRequirement> _requirements = new List<ApiRequirement>();
        private readonly List<string> _messages = new List<string>();

        public IList<ApiRequirement> Requirements
        {
            get { return _requirements; }
        }

        public IList<string> Messages
        {
            get { return _messages; }
        }

        public ApiCompatibilitySeverity Severity { get; set; }
        public string RequirementSummary { get; set; }
        public string Summary { get; set; }

        public bool IsCompatible
        {
            get { return Severity != ApiCompatibilitySeverity.Error; }
        }

        public ApiCompatibilityReport()
        {
            Severity = ApiCompatibilitySeverity.None;
            RequirementSummary = string.Empty;
            Summary = string.Empty;
        }

        public void AddMessage(ApiCompatibilitySeverity severity, string message)
        {
            if (string.IsNullOrEmpty(message))
                return;

            _messages.Add(message);
            if (severity > Severity)
                Severity = severity;
        }
    }
}
