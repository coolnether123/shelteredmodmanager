using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;
using UnityDebug = UnityEngine.Debug;

namespace Cortex.Host.Unity.Runtime
{
    public sealed class WindowsPathInteractionService : IPathInteractionService
    {
        private const string LogPrefix = "[Cortex.PathPicker] ";
        private const int ProcessExitCodeSuccess = 0;
        private const int ProcessExitCodeCancelled = 2;
        private const string PickerExecutableName = "Cortex.PathPicker.Host.exe";

        private sealed class PendingSelection
        {
            public PathSelectionResult Result;
            public bool IsCompleted;
        }

        private readonly object _sync = new object();
        private readonly Dictionary<string, PendingSelection> _pendingSelections = new Dictionary<string, PendingSelection>(StringComparer.OrdinalIgnoreCase);
        private readonly ICortexHostEnvironment _hostEnvironment;

        public WindowsPathInteractionService()
            : this(null)
        {
        }

        public WindowsPathInteractionService(ICortexHostEnvironment hostEnvironment)
        {
            _hostEnvironment = hostEnvironment;
        }

        public bool TryBeginSelectPath(PathSelectionRequest request, out string requestId)
        {
            requestId = string.Empty;
            var effectiveRequest = CloneRequest(request);
            var selectionId = Guid.NewGuid().ToString("N");
            var pending = new PendingSelection();

            lock (_sync)
            {
                _pendingSelections[selectionId] = pending;
            }

            UnityDebug.Log(LogPrefix + "Queueing selection request. RequestId=" + selectionId +
                ", SelectionKind=" + effectiveRequest.SelectionKind +
                ", Title=" + (effectiveRequest.Title ?? string.Empty) +
                ", InitialPath=" + (effectiveRequest.InitialPath ?? string.Empty) +
                ", SuggestedFileName=" + (effectiveRequest.SuggestedFileName ?? string.Empty) +
                ", CheckPathExists=" + effectiveRequest.CheckPathExists + ".");

            var thread = new Thread(delegate()
            {
                UnityDebug.Log(LogPrefix + "Worker thread started. RequestId=" + selectionId + ".");
                CompleteSelection(selectionId, ExecuteSelection(effectiveRequest, _hostEnvironment));
            });
            thread.IsBackground = true;

            try
            {
                thread.Start();
                requestId = selectionId;
                UnityDebug.Log(LogPrefix + "Worker thread launched. RequestId=" + selectionId + ".");
                return true;
            }
            catch (Exception ex)
            {
                UnityDebug.LogWarning(LogPrefix + "Failed to launch worker thread. RequestId=" + selectionId +
                    ", Error=" + ex + ".");
                CompleteSelection(selectionId, new PathSelectionResult
                {
                    Succeeded = false,
                    ErrorMessage = ex.Message ?? string.Empty
                });
                requestId = selectionId;
                return false;
            }
        }

        public bool TryGetCompletedSelection(string requestId, out PathSelectionResult result)
        {
            result = null;
            if (string.IsNullOrEmpty(requestId))
            {
                return false;
            }

            lock (_sync)
            {
                PendingSelection pending;
                if (!_pendingSelections.TryGetValue(requestId, out pending) || pending == null || !pending.IsCompleted)
                {
                    return false;
                }

                result = pending.Result ?? new PathSelectionResult();
                _pendingSelections.Remove(requestId);
                UnityDebug.Log(LogPrefix + "Selection result consumed. RequestId=" + requestId +
                    ", Succeeded=" + result.Succeeded +
                    ", SelectedPath=" + (result.SelectedPath ?? string.Empty) +
                    ", Error=" + (result.ErrorMessage ?? string.Empty) + ".");
                return true;
            }
        }

        public bool TryOpenPath(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return false;
            }

            try
            {
                if (Directory.Exists(normalizedPath))
                {
                    UnityDebug.Log(LogPrefix + "Opening directory. Path=" + normalizedPath + ".");
                    Process.Start("explorer.exe", "\"" + normalizedPath + "\"");
                    return true;
                }

                if (File.Exists(normalizedPath))
                {
                    UnityDebug.Log(LogPrefix + "Opening file. Path=" + normalizedPath + ".");
                    Process.Start(normalizedPath);
                    return true;
                }
            }
            catch (Exception ex)
            {
                UnityDebug.LogWarning(LogPrefix + "Open path failed. Path=" + normalizedPath +
                    ", Error=" + ex + ".");
            }

            UnityDebug.LogWarning(LogPrefix + "Open path skipped because the path did not exist. Path=" + normalizedPath + ".");
            return false;
        }

        public bool TryRevealPath(string path)
        {
            var normalizedPath = NormalizePath(path);
            if (string.IsNullOrEmpty(normalizedPath))
            {
                return false;
            }

            try
            {
                if (File.Exists(normalizedPath))
                {
                    UnityDebug.Log(LogPrefix + "Revealing file. Path=" + normalizedPath + ".");
                    Process.Start("explorer.exe", "/select,\"" + normalizedPath + "\"");
                    return true;
                }

                if (Directory.Exists(normalizedPath))
                {
                    UnityDebug.Log(LogPrefix + "Revealing directory. Path=" + normalizedPath + ".");
                    Process.Start("explorer.exe", "\"" + normalizedPath + "\"");
                    return true;
                }
            }
            catch (Exception ex)
            {
                UnityDebug.LogWarning(LogPrefix + "Reveal path failed. Path=" + normalizedPath +
                    ", Error=" + ex + ".");
            }

            UnityDebug.LogWarning(LogPrefix + "Reveal path skipped because the path did not exist. Path=" + normalizedPath + ".");
            return false;
        }

        private void CompleteSelection(string requestId, PathSelectionResult result)
        {
            lock (_sync)
            {
                PendingSelection pending;
                if (!_pendingSelections.TryGetValue(requestId, out pending) || pending == null)
                {
                    return;
                }

                pending.Result = result ?? new PathSelectionResult();
                pending.IsCompleted = true;
                UnityDebug.Log(LogPrefix + "Selection request completed. RequestId=" + requestId +
                    ", Succeeded=" + pending.Result.Succeeded +
                    ", SelectedPath=" + (pending.Result.SelectedPath ?? string.Empty) +
                    ", Error=" + (pending.Result.ErrorMessage ?? string.Empty) + ".");
            }
        }

        private PathSelectionResult ExecuteSelection(PathSelectionRequest request)
        {
            return ExecuteSelection(request, _hostEnvironment);
        }

        private PathSelectionResult ExecuteSelection(PathSelectionRequest request, ICortexHostEnvironment hostEnvironment)
        {
            var executablePath = ResolvePickerExecutablePath(hostEnvironment ?? _hostEnvironment);
            if (string.IsNullOrEmpty(executablePath))
            {
                UnityDebug.LogWarning(LogPrefix + "Picker host executable could not be resolved.");
                return new PathSelectionResult
                {
                    Succeeded = false,
                    ErrorMessage = "Cortex.PathPicker.Host.exe could not be found."
                };
            }

            var arguments = BuildPickerArguments(request);
            try
            {
                UnityDebug.Log(LogPrefix + "Launching picker host. Executable=" + executablePath +
                    ", Arguments=" + arguments + ".");

                var process = new Process();
                process.StartInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    WorkingDirectory = Path.GetDirectoryName(executablePath) ?? string.Empty,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                if (!process.Start())
                {
                    UnityDebug.LogWarning(LogPrefix + "Picker host process failed to start.");
                    return new PathSelectionResult
                    {
                        Succeeded = false,
                        ErrorMessage = "The picker host process did not start."
                    };
                }

                var standardOutput = process.StandardOutput.ReadToEnd();
                var standardError = process.StandardError.ReadToEnd();
                process.WaitForExit();

                var selectedPath = NormalizePath((standardOutput ?? string.Empty).Trim());
                var errorMessage = (standardError ?? string.Empty).Trim();
                UnityDebug.Log(LogPrefix + "Picker host exited. ExitCode=" + process.ExitCode +
                    ", SelectedPath=" + selectedPath +
                    ", Error=" + errorMessage + ".");

                if (process.ExitCode == ProcessExitCodeSuccess)
                {
                    return new PathSelectionResult
                    {
                        Succeeded = !string.IsNullOrEmpty(selectedPath),
                        SelectedPath = selectedPath
                    };
                }

                if (process.ExitCode == ProcessExitCodeCancelled)
                {
                    return new PathSelectionResult();
                }

                return new PathSelectionResult
                {
                    Succeeded = false,
                    ErrorMessage = string.IsNullOrEmpty(errorMessage)
                        ? "Picker host failed with exit code " + process.ExitCode + "."
                        : errorMessage
                };
            }
            catch (Exception ex)
            {
                UnityDebug.LogWarning(LogPrefix + "Picker host threw an exception. Error=" + ex + ".");
                return new PathSelectionResult
                {
                    Succeeded = false,
                    ErrorMessage = ex.Message ?? string.Empty
                };
            }
        }

        private static string ResolvePickerExecutablePath(ICortexHostEnvironment hostEnvironment)
        {
            var candidates = new List<string>();
            AddBundledToolCandidates(candidates, hostEnvironment != null ? hostEnvironment.HostBinPath : string.Empty);

            var assemblyLocation = typeof(WindowsPathInteractionService).Assembly.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDirectory))
                {
                    AddBundledToolCandidates(candidates, assemblyDirectory);
                }
            }

            var appBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(appBaseDirectory))
            {
                AddBundledToolCandidates(candidates, appBaseDirectory);
            }

            return BundledToolPathResolver.ResolveCandidate(candidates);
        }

        private static void AddBundledToolCandidates(IList<string> candidates, string baseDirectory)
        {
            var hostBinCandidates = EnumerateHostBinCandidates(baseDirectory);
            for (var i = 0; i < hostBinCandidates.Count; i++)
            {
                var hostBinPath = hostBinCandidates[i];
                if (string.IsNullOrEmpty(hostBinPath))
                {
                    continue;
                }

                candidates.Add(Path.Combine(hostBinPath, PickerExecutableName));
                candidates.Add(Path.Combine(Path.Combine(Path.Combine(hostBinPath, "tools"), "windows-path-picker"), PickerExecutableName));
                candidates.Add(Path.Combine(Path.Combine(hostBinPath, "decompiler"), PickerExecutableName));
            }
        }

        private static IList<string> EnumerateHostBinCandidates(string baseDirectory)
        {
            var candidates = new List<string>();
            var normalizedBaseDirectory = NormalizePath(baseDirectory);
            AddCandidate(candidates, normalizedBaseDirectory);

            if (string.IsNullOrEmpty(normalizedBaseDirectory))
            {
                return candidates;
            }

            var normalizedLeaf = GetLeafName(normalizedBaseDirectory);
            var parent = Directory.GetParent(normalizedBaseDirectory);

            if (string.Equals(normalizedLeaf, "decompiler", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedLeaf, "plugins", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedLeaf, "tools", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, parent != null ? parent.FullName : string.Empty);
            }
            else if (parent != null && string.Equals(GetLeafName(parent.FullName), "tools", StringComparison.OrdinalIgnoreCase))
            {
                var grandParent = Directory.GetParent(parent.FullName);
                AddCandidate(candidates, grandParent != null ? grandParent.FullName : string.Empty);
            }

            if (!string.Equals(normalizedLeaf, "bin", StringComparison.OrdinalIgnoreCase))
            {
                AddCandidate(candidates, Path.Combine(normalizedBaseDirectory, "bin"));
            }

            return candidates;
        }

        private static void AddCandidate(IList<string> candidates, string path)
        {
            var normalized = NormalizePath(path);
            if (string.IsNullOrEmpty(normalized))
            {
                return;
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                if (string.Equals(candidates[i], normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
            }

            candidates.Add(normalized);
        }

        private static string GetLeafName(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            return Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)) ?? string.Empty;
        }

        private static string BuildPickerArguments(PathSelectionRequest request)
        {
            var builder = new List<string>();
            builder.Add(request != null && request.SelectionKind == PathSelectionKind.OpenFile ? "file" : "folder");
            AppendArgument(builder, "--title", request != null ? request.Title : string.Empty);
            AppendArgument(builder, "--initial-path", request != null ? request.InitialPath : string.Empty);
            AppendArgument(builder, "--suggested-file-name", request != null ? request.SuggestedFileName : string.Empty);
            AppendArgument(builder, "--filter", request != null ? request.Filter : string.Empty);
            AppendArgument(builder, "--check-path-exists", request == null || request.CheckPathExists ? "true" : "false");
            AppendArgument(builder, "--restore-directory", request != null && request.RestoreDirectory ? "true" : "false");
            return string.Join(" ", builder.ToArray());
        }

        private static void AppendArgument(IList<string> arguments, string name, string value)
        {
            if (arguments == null || string.IsNullOrEmpty(name))
            {
                return;
            }

            arguments.Add(name);
            arguments.Add(QuoteArgument(value ?? string.Empty));
        }

        private static string QuoteArgument(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "\"\"";
            }

            return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        }

        private static PathSelectionRequest CloneRequest(PathSelectionRequest request)
        {
            if (request == null)
            {
                return new PathSelectionRequest();
            }

            return new PathSelectionRequest
            {
                SelectionKind = request.SelectionKind,
                Title = request.Title ?? string.Empty,
                InitialPath = request.InitialPath ?? string.Empty,
                SuggestedFileName = request.SuggestedFileName ?? string.Empty,
                Filter = request.Filter ?? string.Empty,
                CheckPathExists = request.CheckPathExists,
                RestoreDirectory = request.RestoreDirectory
            };
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path.Trim());
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
