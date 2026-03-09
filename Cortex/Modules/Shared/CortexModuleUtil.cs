using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Modules.Shared
{
    internal static class CortexModuleUtil
    {
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

        public static DecompilerResponse RequestDecompilerSource(
            ISourceReferenceService sourceReferenceService,
            CortexShellState state,
            string assemblyPath,
            int metadataToken,
            DecompilerEntityKind entityKind,
            bool ignoreCache)
        {
            if (sourceReferenceService == null || state == null || string.IsNullOrEmpty(assemblyPath) || metadataToken <= 0)
            {
                return null;
            }

            state.LastReferenceResult = sourceReferenceService.GetSource(new DecompilerRequest
            {
                AssemblyPath = assemblyPath,
                MetadataToken = metadataToken,
                IgnoreCache = ignoreCache,
                EntityKind = entityKind
            });
            return state.LastReferenceResult;
        }

        public static bool OpenDecompilerResult(IDocumentService documentService, CortexShellState state, DecompilerResponse response)
        {
            if (documentService == null || state == null || response == null || string.IsNullOrEmpty(response.CachePath) || !File.Exists(response.CachePath))
            {
                return false;
            }

            OpenDocument(documentService, state, response.CachePath, 1);
            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            return true;
        }

        public static bool TryResolveSourceLocation(ISourcePathResolver sourcePathResolver, string text, CortexProjectDefinition project, CortexSettings settings, out string filePath, out int lineNumber)
        {
            filePath = string.Empty;
            lineNumber = 0;
            if (sourcePathResolver == null || string.IsNullOrEmpty(text))
            {
                return false;
            }

            var location = sourcePathResolver.ResolveTextLocation(text, project, settings);
            if (location != null && location.Success)
            {
                filePath = location.ResolvedPath ?? string.Empty;
                lineNumber = location.LineNumber;
                return !string.IsNullOrEmpty(filePath);
            }

            return false;
        }

        public static string BuildSourceResolutionExplanation(ISourcePathResolver sourcePathResolver, RuntimeLogEntry entry, CortexProjectDefinition project, CortexSettings settings)
        {
            if (entry == null)
            {
                return "No log entry is selected.";
            }

            string filePath;
            int lineNumber;
            if (TryResolveSourceLocation(sourcePathResolver, entry.Message, project, settings, out filePath, out lineNumber))
            {
                return "Resolved source from an embedded file marker: " + filePath + " @ line " + lineNumber + ".";
            }

            if (entry.StackFrames != null && entry.StackFrames.Count > 0)
            {
                return "No file marker was embedded in the log message. Cortex did capture runtime stack frames for this entry, so use the frame list below to inspect likely methods and open the best match.";
            }

            var builder = new System.Text.StringBuilder();
            builder.Append("Cortex could not resolve an exact function from this entry. ");
            if (!string.IsNullOrEmpty(entry.Source))
            {
                builder.Append("The source label '");
                builder.Append(entry.Source);
                builder.Append("' is only a logger/channel name here, not a file or method signature. ");
            }

            builder.Append("The message does not contain a compiler-style file marker or stack-trace path, and no structured runtime frames were captured. ");

            System.Collections.Generic.IList<string> roots = sourcePathResolver != null
                ? sourcePathResolver.GetSearchRoots(project, settings)
                : new System.Collections.Generic.List<string>();
            if (roots.Count > 0)
            {
                builder.Append("Search roots checked: ");
                builder.Append(string.Join(", ", new System.Collections.Generic.List<string>(roots).ToArray()));
                builder.Append(". ");
            }
            else
            {
                builder.Append("No source search roots are configured yet. ");
            }

            builder.Append("For exact navigation, include a stack trace or a file:line marker in the log message.");
            return builder.ToString();
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
