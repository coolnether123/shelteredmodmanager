using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class RuntimeSourceNavigationService : IRuntimeSourceNavigationService
    {
        private readonly IRuntimeSymbolResolver _symbolResolver;

        public RuntimeSourceNavigationService(IRuntimeSymbolResolver symbolResolver)
        {
            _symbolResolver = symbolResolver;
        }

        public SourceNavigationTarget Resolve(RuntimeLogEntry entry, int frameIndex, CortexProjectDefinition project, CortexSettings settings)
        {
            if (entry == null || entry.StackFrames == null || entry.StackFrames.Count == 0)
            {
                return Failure("No runtime stack frames are available for this log entry.");
            }

            if (frameIndex < 0 || frameIndex >= entry.StackFrames.Count)
            {
                return Failure("The selected stack frame is out of range.");
            }

            var frame = entry.StackFrames[frameIndex];
            var directPath = ResolveCandidatePath(project, settings, frame != null ? frame.FilePath : null);
            if (!string.IsNullOrEmpty(directPath))
            {
                var lineNumber = frame != null && frame.LineNumber > 0 ? frame.LineNumber : 1;
                return new SourceNavigationTarget
                {
                    Success = true,
                    FilePath = directPath,
                    LineNumber = lineNumber,
                    ColumnNumber = frame != null ? frame.ColumnNumber : 0,
                    StatusMessage = "Resolved runtime frame to source."
                };
            }

            if (_symbolResolver == null)
            {
                return Failure("No runtime symbol resolver is available.");
            }

            return _symbolResolver.Resolve(frame, project, settings) ?? Failure("Runtime symbol resolution failed.");
        }

        private static SourceNavigationTarget Failure(string message)
        {
            return new SourceNavigationTarget
            {
                Success = false,
                FilePath = string.Empty,
                LineNumber = 0,
                ColumnNumber = 0,
                StatusMessage = message ?? string.Empty
            };
        }

        private static string ResolveCandidatePath(CortexProjectDefinition project, CortexSettings settings, string rawPath)
        {
            if (string.IsNullOrEmpty(rawPath))
            {
                return string.Empty;
            }

            rawPath = rawPath.Trim().Trim('"');
            if (Path.IsPathRooted(rawPath) && File.Exists(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            if (File.Exists(rawPath))
            {
                return Path.GetFullPath(rawPath);
            }

            var searchRoots = BuildSearchRoots(project, settings);
            for (var i = 0; i < searchRoots.Count; i++)
            {
                var sourceRoot = searchRoots[i];
                var combined = Path.Combine(sourceRoot, rawPath);
                if (File.Exists(combined))
                {
                    return Path.GetFullPath(combined);
                }

                var fileName = Path.GetFileName(rawPath);
                if (string.IsNullOrEmpty(fileName))
                {
                    continue;
                }

                try
                {
                    var files = Directory.GetFiles(sourceRoot, fileName, SearchOption.AllDirectories);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
                catch
                {
                }
            }

            return string.Empty;
        }

        private static List<string> BuildSearchRoots(CortexProjectDefinition project, CortexSettings settings)
        {
            var roots = new List<string>();
            AddRoot(roots, project != null ? project.SourceRootPath : string.Empty);
            AddRoot(roots, project != null ? Path.GetDirectoryName(project.ProjectFilePath) : string.Empty);
            AddRoot(roots, settings != null ? settings.WorkspaceRootPath : string.Empty);
            AddRoot(roots, settings != null ? settings.ModsRootPath : string.Empty);

            var rawRoots = settings != null ? settings.AdditionalSourceRoots : string.Empty;
            if (!string.IsNullOrEmpty(rawRoots))
            {
                var segments = rawRoots.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length; i++)
                {
                    AddRoot(roots, segments[i]);
                }
            }

            return roots;
        }

        private static void AddRoot(List<string> roots, string root)
        {
            if (roots == null || string.IsNullOrEmpty(root))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(root.Trim());
                if (Directory.Exists(fullPath) && !roots.Contains(fullPath))
                {
                    roots.Add(fullPath);
                }
            }
            catch
            {
            }
        }
    }
}
