using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ShelteredModManager.ContentPacks
{
    public enum ContentPackValidationSeverity
    {
        Warning,
        Error
    }

    public sealed class ContentPackValidationIssue
    {
        public ContentPackValidationSeverity Severity;
        public string Code;
        public string Path;
        public string Message;
    }

    public sealed class ContentPackValidationResult
    {
        private readonly List<ContentPackValidationIssue> _issues =
            new List<ContentPackValidationIssue>();

        public ReadOnlyCollection<ContentPackValidationIssue> Issues
        {
            get { return _issues.AsReadOnly(); }
        }

        public bool IsValid
        {
            get
            {
                for (int i = 0; i < _issues.Count; i++)
                {
                    if (_issues[i].Severity == ContentPackValidationSeverity.Error)
                        return false;
                }
                return true;
            }
        }

        public int ErrorCount
        {
            get { return Count(ContentPackValidationSeverity.Error); }
        }

        public int WarningCount
        {
            get { return Count(ContentPackValidationSeverity.Warning); }
        }

        public void AddError(string code, string path, string message)
        {
            Add(ContentPackValidationSeverity.Error, code, path, message);
        }

        public void AddWarning(string code, string path, string message)
        {
            Add(ContentPackValidationSeverity.Warning, code, path, message);
        }

        private void Add(ContentPackValidationSeverity severity, string code, string path, string message)
        {
            _issues.Add(new ContentPackValidationIssue
            {
                Severity = severity,
                Code = code ?? string.Empty,
                Path = path ?? string.Empty,
                Message = message ?? string.Empty
            });
        }

        private int Count(ContentPackValidationSeverity severity)
        {
            int count = 0;
            for (int i = 0; i < _issues.Count; i++)
            {
                if (_issues[i].Severity == severity)
                    count++;
            }
            return count;
        }
    }
}
