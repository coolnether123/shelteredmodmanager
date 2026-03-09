using System;
using System.Collections.Generic;

namespace Cortex.Core.Models
{
    public enum BuildDiagnosticSeverity
    {
        Info,
        Warning,
        Error
    }

    public sealed class BuildCommand
    {
        public string FileName;
        public string Arguments;
        public string WorkingDirectory;
        public string OutputAssemblyPath;
        public string OutputPdbPath;
        public int TimeoutMs;
        public readonly List<BuildCommandStep> Steps = new List<BuildCommandStep>();
    }

    public sealed class BuildCommandStep
    {
        public string FileName;
        public string Arguments;
        public string WorkingDirectory;
        public int TimeoutMs;
    }

    public sealed class BuildRequest
    {
        public CortexProjectDefinition Project;
        public bool Clean;
        public string Configuration;
    }

    public sealed class BuildDiagnostic
    {
        public string FilePath;
        public int Line;
        public int Column;
        public string Code;
        public string Message;
        public BuildDiagnosticSeverity Severity;
        public string RawText;
    }

    public sealed class BuildResult
    {
        public bool Success;
        public int ExitCode;
        public string Command;
        public string Arguments;
        public List<string> OutputLines;
        public List<BuildDiagnostic> Diagnostics;
        public DateTime StartedUtc;
        public TimeSpan Duration;
        public string OutputAssemblyPath;
        public string OutputPdbPath;
        public bool OutputAssemblyUpdated;
        public bool OutputPdbPresent;
        public bool TimedOut;

        public BuildResult()
        {
            OutputLines = new List<string>();
            Diagnostics = new List<BuildDiagnostic>();
            StartedUtc = DateTime.UtcNow;
        }
    }
}
