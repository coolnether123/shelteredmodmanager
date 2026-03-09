using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class ProcessBuildExecutor : IBuildExecutor, IBuildOutputParser
    {
        private static readonly Regex DiagnosticRegex = CreateDiagnosticRegex();

        private static Regex CreateDiagnosticRegex()
        {
            const string pattern = @"^(.*)\((\d+),(\d+)\):\s+(warning|error)\s+([A-Z0-9]+):\s+(.*)$";
            try
            {
                return new Regex(pattern, RegexOptions.IgnoreCase);
            }
            catch (ArgumentOutOfRangeException)
            {
                return new Regex(pattern);
            }
        }

        public BuildResult Execute(BuildCommand command)
        {
            if (command == null)
            {
                return new BuildResult { Success = false };
            }

            var result = new BuildResult();
            result.Command = command.FileName;
            result.Arguments = command.Arguments;
            result.OutputAssemblyPath = command.OutputAssemblyPath;
            result.OutputPdbPath = command.OutputPdbPath;

            DateTime? beforeAssemblyWrite = File.Exists(command.OutputAssemblyPath) ? (DateTime?)File.GetLastWriteTimeUtc(command.OutputAssemblyPath) : null;
            var steps = BuildSteps(command);
            if (steps.Count == 0)
            {
                result.Success = false;
                result.OutputLines.Add("Build command did not include an executable step.");
                return result;
            }

            result.StartedUtc = DateTime.UtcNow;
            for (var i = 0; i < steps.Count; i++)
            {
                if (!ExecuteStep(steps[i], result, command.TimeoutMs))
                {
                    result.Duration = DateTime.UtcNow - result.StartedUtc;
                    result.Diagnostics.AddRange(Parse(result.OutputLines));
                    result.Success = false;
                    return result;
                }
            }

            result.Duration = DateTime.UtcNow - result.StartedUtc;
            result.Diagnostics.AddRange(Parse(result.OutputLines));

            var afterAssemblyWrite = File.Exists(command.OutputAssemblyPath) ? (DateTime?)File.GetLastWriteTimeUtc(command.OutputAssemblyPath) : null;
            result.OutputAssemblyUpdated = afterAssemblyWrite.HasValue && (!beforeAssemblyWrite.HasValue || afterAssemblyWrite.Value > beforeAssemblyWrite.Value);
            result.OutputPdbPresent = !string.IsNullOrEmpty(command.OutputPdbPath) && File.Exists(command.OutputPdbPath);
            result.Success = result.ExitCode == 0 && result.OutputAssemblyUpdated;
            return result;
        }

        private static List<BuildCommandStep> BuildSteps(BuildCommand command)
        {
            var steps = new List<BuildCommandStep>();
            if (command == null)
            {
                return steps;
            }

            if (command.Steps != null && command.Steps.Count > 0)
            {
                for (var i = 0; i < command.Steps.Count; i++)
                {
                    if (command.Steps[i] != null)
                    {
                        steps.Add(command.Steps[i]);
                    }
                }

                return steps;
            }

            steps.Add(new BuildCommandStep
            {
                FileName = command.FileName,
                Arguments = command.Arguments,
                WorkingDirectory = command.WorkingDirectory,
                TimeoutMs = command.TimeoutMs
            });
            return steps;
        }

        private bool ExecuteStep(BuildCommandStep step, BuildResult result, int defaultTimeoutMs)
        {
            if (step == null || string.IsNullOrEmpty(step.FileName))
            {
                return false;
            }

            result.Command = step.FileName;
            result.Arguments = step.Arguments;
            result.OutputLines.Add("> " + step.FileName + " " + (step.Arguments ?? string.Empty));

            var process = new Process();
            process.StartInfo = new ProcessStartInfo(step.FileName, step.Arguments ?? string.Empty)
            {
                WorkingDirectory = !string.IsNullOrEmpty(step.WorkingDirectory) ? step.WorkingDirectory : string.Empty,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    result.OutputLines.Add(e.Data);
                }
            };

            process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs e)
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    result.OutputLines.Add(e.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutMs = step.TimeoutMs > 0 ? step.TimeoutMs : (defaultTimeoutMs > 0 ? defaultTimeoutMs : 300000);
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                result.ExitCode = -1;
                result.TimedOut = true;
                result.OutputLines.Add("Build timed out after " + timeoutMs + "ms.");
                return false;
            }

            process.WaitForExit();
            result.ExitCode = process.ExitCode;
            return result.ExitCode == 0;
        }

        public System.Collections.Generic.IList<BuildDiagnostic> Parse(System.Collections.Generic.IList<string> outputLines)
        {
            var diagnostics = new System.Collections.Generic.List<BuildDiagnostic>();
            if (outputLines == null)
            {
                return diagnostics;
            }

            for (var i = 0; i < outputLines.Count; i++)
            {
                var line = outputLines[i] ?? string.Empty;
                var match = DiagnosticRegex.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                diagnostics.Add(new BuildDiagnostic
                {
                    FilePath = match.Groups[1].Value,
                    Line = ParseInt(match.Groups[2].Value),
                    Column = ParseInt(match.Groups[3].Value),
                    Severity = string.Equals(match.Groups[4].Value, "warning", StringComparison.OrdinalIgnoreCase)
                        ? BuildDiagnosticSeverity.Warning
                        : BuildDiagnosticSeverity.Error,
                    Code = match.Groups[5].Value,
                    Message = match.Groups[6].Value,
                    RawText = line
                });
            }

            return diagnostics;
        }

        private static int ParseInt(string value)
        {
            int parsed;
            return int.TryParse(value, out parsed) ? parsed : 0;
        }
    }
}
