using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
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
                CompleteSelection(selectionId, ExecuteSelection(effectiveRequest));
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

        private static PathSelectionResult ExecuteSelection(PathSelectionRequest request)
        {
            var executablePath = ResolvePickerExecutablePath();
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

        private static string ResolvePickerExecutablePath()
        {
            var candidates = new List<string>();
            var assemblyLocation = typeof(WindowsPathInteractionService).Assembly.Location;
            if (!string.IsNullOrEmpty(assemblyLocation))
            {
                var assemblyDirectory = Path.GetDirectoryName(assemblyLocation);
                if (!string.IsNullOrEmpty(assemblyDirectory))
                {
                    candidates.Add(Path.Combine(assemblyDirectory, PickerExecutableName));
                }
            }

            var appBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            if (!string.IsNullOrEmpty(appBaseDirectory))
            {
                candidates.Add(Path.Combine(appBaseDirectory, PickerExecutableName));
                candidates.Add(Path.Combine(Path.Combine(appBaseDirectory, "decompiler"), PickerExecutableName));
            }

            for (var i = 0; i < candidates.Count; i++)
            {
                try
                {
                    var candidate = Path.GetFullPath(candidates[i]);
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
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
