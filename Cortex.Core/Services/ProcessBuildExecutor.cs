using System;
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

            var process = new Process();
            process.StartInfo = new ProcessStartInfo(command.FileName, command.Arguments)
            {
                WorkingDirectory = command.WorkingDirectory,
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

            result.StartedUtc = DateTime.UtcNow;
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            var timeoutMs = command.TimeoutMs > 0 ? command.TimeoutMs : 300000;
            if (!process.WaitForExit(timeoutMs))
            {
                try { process.Kill(); } catch { }
                result.ExitCode = -1;
                result.TimedOut = true;
                result.Duration = DateTime.UtcNow - result.StartedUtc;
                result.OutputLines.Add("Build timed out after " + timeoutMs + "ms.");
                result.Diagnostics.AddRange(Parse(result.OutputLines));
                result.Success = false;
                return result;
            }

            result.ExitCode = process.ExitCode;
            result.Duration = DateTime.UtcNow - result.StartedUtc;
            result.Diagnostics.AddRange(Parse(result.OutputLines));

            var afterAssemblyWrite = File.Exists(command.OutputAssemblyPath) ? (DateTime?)File.GetLastWriteTimeUtc(command.OutputAssemblyPath) : null;
            result.OutputAssemblyUpdated = afterAssemblyWrite.HasValue && (!beforeAssemblyWrite.HasValue || afterAssemblyWrite.Value > beforeAssemblyWrite.Value);
            result.OutputPdbPresent = !string.IsNullOrEmpty(command.OutputPdbPath) && File.Exists(command.OutputPdbPath);
            result.Success = result.ExitCode == 0 && result.OutputAssemblyUpdated;
            return result;
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
