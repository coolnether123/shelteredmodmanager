using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Modules.Shared
{
    internal static class CortexModuleUtil
    {
        private static readonly Regex StackTraceLocationRegex = new Regex(@" in (?<path>.*):line (?<line>\d+)", RegexOptions.IgnoreCase);
        private static readonly Regex CompilerLocationRegex = new Regex(@"^(?<path>.*)\((?<line>\d+)(,(?<column>\d+))?\):", RegexOptions.IgnoreCase);

        public static DocumentSession OpenDocument(IDocumentService documentService, CortexShellState state, string filePath, int highlightedLine)
        {
            if (documentService == null || state == null || string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(filePath);
            }
            catch
            {
                return null;
            }

            for (var i = 0; i < state.Documents.OpenDocuments.Count; i++)
            {
                if (string.Equals(state.Documents.OpenDocuments[i].FilePath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    state.Documents.ActiveDocument = state.Documents.OpenDocuments[i];
                    state.Documents.ActiveDocumentPath = fullPath;
                    state.Documents.ActiveDocument.HighlightedLine = highlightedLine;
                    return state.Documents.ActiveDocument;
                }
            }

            DocumentSession session;
            try
            {
                session = documentService.Open(fullPath);
            }
            catch
            {
                return null;
            }

            if (session == null)
            {
                return null;
            }

            session.HighlightedLine = highlightedLine;
            state.Documents.OpenDocuments.Add(session);
            state.Documents.ActiveDocument = session;
            state.Documents.ActiveDocumentPath = fullPath;
            return session;
        }

        public static void CloseDocument(CortexShellState state, string filePath)
        {
            if (state == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            for (var i = state.Documents.OpenDocuments.Count - 1; i >= 0; i--)
            {
                if (string.Equals(state.Documents.OpenDocuments[i].FilePath, filePath, StringComparison.OrdinalIgnoreCase))
                {
                    state.Documents.OpenDocuments.RemoveAt(i);
                }
            }

            if (state.Documents.ActiveDocument != null && string.Equals(state.Documents.ActiveDocument.FilePath, filePath, StringComparison.OrdinalIgnoreCase))
            {
                state.Documents.ActiveDocument = state.Documents.OpenDocuments.Count > 0 ? state.Documents.OpenDocuments[0] : null;
                state.Documents.ActiveDocumentPath = state.Documents.ActiveDocument != null ? state.Documents.ActiveDocument.FilePath : string.Empty;
            }
        }

        public static string GetDocumentDisplayName(DocumentSession session)
        {
            if (session == null)
            {
                return "Unknown";
            }

            var name = Path.GetFileName(session.FilePath);
            if (string.IsNullOrEmpty(name))
            {
                name = session.FilePath ?? "Untitled";
            }

            return session.IsDirty ? name + "*" : name;
        }

        public static bool TryResolveSourceLocation(string text, CortexProjectDefinition project, CortexSettings settings, out string filePath, out int lineNumber)
        {
            filePath = string.Empty;
            lineNumber = 0;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var match = StackTraceLocationRegex.Match(text);
            if (match.Success)
            {
                lineNumber = ParseInt(match.Groups["line"].Value);
                filePath = ResolveCandidatePath(project, settings, match.Groups["path"].Value);
                return !string.IsNullOrEmpty(filePath);
            }

            match = CompilerLocationRegex.Match(text);
            if (match.Success)
            {
                lineNumber = ParseInt(match.Groups["line"].Value);
                filePath = ResolveCandidatePath(project, settings, match.Groups["path"].Value);
                return !string.IsNullOrEmpty(filePath);
            }

            return false;
        }

        public static string BuildSourceResolutionExplanation(RuntimeLogEntry entry, CortexProjectDefinition project, CortexSettings settings)
        {
            if (entry == null)
            {
                return "No log entry is selected.";
            }

            string filePath;
            int lineNumber;
            if (TryResolveSourceLocation(entry.Message, project, settings, out filePath, out lineNumber))
            {
                return "Resolved source from an embedded file marker: " + filePath + " @ line " + lineNumber + ".";
            }

            if (entry.StackFrames != null && entry.StackFrames.Count > 0)
            {
                return "No file marker was embedded in the log message. Cortex did capture runtime stack frames for this entry, so use the frame list below to inspect likely methods and open the best match.";
            }

            var builder = new StringBuilder();
            builder.Append("Cortex could not resolve an exact function from this entry. ");
            if (!string.IsNullOrEmpty(entry.Source))
            {
                builder.Append("The source label '");
                builder.Append(entry.Source);
                builder.Append("' is only a logger/channel name here, not a file or method signature. ");
            }

            builder.Append("The message does not contain a compiler-style file marker or stack-trace path, and no structured runtime frames were captured. ");

            var roots = BuildSearchRoots(project, settings);
            if (roots.Count > 0)
            {
                builder.Append("Search roots checked: ");
                builder.Append(string.Join(", ", roots.ToArray()));
                builder.Append(". ");
            }
            else
            {
                builder.Append("No source search roots are configured yet. ");
            }

            builder.Append("For exact navigation, include a stack trace or a file:line marker in the log message.");
            return builder.ToString();
        }

        public static string ResolveCandidatePath(CortexProjectDefinition project, CortexSettings settings, string rawPath)
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
                if (!string.IsNullOrEmpty(fileName))
                {
                    var files = FindFilesSafe(sourceRoot, fileName);
                    if (files.Length > 0)
                    {
                        return files[0];
                    }
                }
            }

            return string.Empty;
        }

        private static List<string> BuildSearchRoots(CortexProjectDefinition project, CortexSettings settings)
        {
            var roots = new List<string>();
            AddRoot(roots, project != null ? project.SourceRootPath : string.Empty);

            var rawRoots = settings != null ? settings.AdditionalSourceRoots : string.Empty;
            if (!string.IsNullOrEmpty(rawRoots))
            {
                var segments = rawRoots.Split(new[] { ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
                for (var i = 0; i < segments.Length; i++)
                {
                    AddRoot(roots, segments[i]);
                }
            }

            AddRoot(roots, settings != null ? settings.WorkspaceRootPath : string.Empty);
            AddRoot(roots, settings != null ? settings.ModsRootPath : string.Empty);
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

        public static string[] FindFilesSafe(string rootPath, string pattern)
        {
            try
            {
                if (!string.IsNullOrEmpty(rootPath) && Directory.Exists(rootPath))
                {
                    return Directory.GetFiles(rootPath, pattern, SearchOption.AllDirectories);
                }
            }
            catch
            {
            }

            return new string[0];
        }

        public static int ParseInt(string raw)
        {
            int value;
            return int.TryParse(raw, out value) ? value : 0;
        }

        public static string[] SplitLines(string text)
        {
            return (text ?? string.Empty).Replace("\r\n", "\n").Split('\n');
        }
    }
}
