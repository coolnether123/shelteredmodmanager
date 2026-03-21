using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
namespace Cortex.Modules.Shared
{
    internal static class CortexModuleUtil
    {
        public static bool IsDecompilerDocumentPath(CortexShellState state, string filePath)
        {
            string reason;
            return TryGetDecompilerDocumentPathReason(state, filePath, out reason);
        }

        private static bool TryGetDecompilerDocumentPathReason(CortexShellState state, string filePath, out string reason)
        {
            reason = string.Empty;
            if (string.IsNullOrEmpty(filePath))
            {
                return false;
            }

            try
            {
                var fullPath = Path.GetFullPath(filePath);
                if (state != null && state.Settings != null && !string.IsNullOrEmpty(state.Settings.DecompilerCachePath))
                {
                    var cacheRoot = Path.GetFullPath(state.Settings.DecompilerCachePath)
                        .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                        Path.DirectorySeparatorChar;
                    if (fullPath.StartsWith(cacheRoot, StringComparison.OrdinalIgnoreCase))
                    {
                        reason = "settings-cache-root";
                        return true;
                    }
                }

                if (fullPath.IndexOf(Path.DirectorySeparatorChar + "cortex_cache" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    fullPath.IndexOf(Path.AltDirectorySeparatorChar + "cortex_cache" + Path.AltDirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    reason = "cortex-cache-segment";
                    return true;
                }

                var runtimeCacheRoot = Path.Combine(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "ModAPI"),
                    "Cache");
                if (string.IsNullOrEmpty(runtimeCacheRoot))
                {
                    return false;
                }

                runtimeCacheRoot = Path.GetFullPath(runtimeCacheRoot)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                if (fullPath.StartsWith(runtimeCacheRoot, StringComparison.OrdinalIgnoreCase))
                {
                    reason = "legacy-modapi-cache-root";
                    return true;
                }

                return false;
            }
            catch
            {
                reason = "path-normalization-failed";
                return false;
            }
        }

        public static DocumentSession FindOpenDocument(CortexShellState state, string filePath)
        {
            if (state == null || string.IsNullOrEmpty(filePath))
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
                var session = state.Documents.OpenDocuments[i];
                if (session != null && string.Equals(session.FilePath, fullPath, StringComparison.OrdinalIgnoreCase))
                {
                    return session;
                }
            }

            return null;
        }

        public static DocumentSession OpenDocument(IDocumentService documentService, CortexShellState state, string filePath, int highlightedLine)
        {
            var kind = IsDecompilerDocumentPath(state, filePath) ? DocumentKind.DecompiledCode : DocumentKind.Unknown;
            return OpenDocument(documentService, state, filePath, highlightedLine, kind);
        }

        public static DocumentSession OpenDocument(IDocumentService documentService, CortexShellState state, string filePath, int highlightedLine, DocumentKind documentKind)
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

            var existing = FindOpenDocument(state, fullPath);
            if (existing != null)
            {
                ApplyDocumentMetadata(existing, fullPath, documentKind, state);
                state.Documents.ActiveDocument = existing;
                state.Documents.ActiveDocumentPath = fullPath;
                state.Documents.ActiveDocument.HighlightedLine = highlightedLine;
                LogOpenedDocument(existing, highlightedLine, true, state);
                return state.Documents.ActiveDocument;
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

            ApplyDocumentMetadata(session, fullPath, documentKind, state);
            session.HighlightedLine = highlightedLine;
            state.Documents.OpenDocuments.Add(session);
            state.Documents.ActiveDocument = session;
            state.Documents.ActiveDocumentPath = fullPath;
            LogOpenedDocument(session, highlightedLine, false, state);
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

            OpenDocument(documentService, state, response.CachePath, 1, DocumentKind.DecompiledCode);
            state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
            return true;
        }

        private static void ApplyDocumentMetadata(DocumentSession session, string fullPath, DocumentKind documentKind, CortexShellState state)
        {
            if (session == null)
            {
                return;
            }

            session.FilePath = fullPath ?? session.FilePath;
            switch (ResolveDocumentKind(fullPath, documentKind, state))
            {
                case DocumentKind.DecompiledCode:
                    session.Kind = DocumentKind.DecompiledCode;
                    session.IsReadOnly = true;
                    break;
                case DocumentKind.SourceCode:
                    session.Kind = DocumentKind.SourceCode;
                    session.IsReadOnly = false;
                    break;
                case DocumentKind.Log:
                    session.Kind = DocumentKind.Log;
                    session.IsReadOnly = true;
                    break;
                case DocumentKind.Text:
                    session.Kind = DocumentKind.Text;
                    session.IsReadOnly = true;
                    break;
                default:
                    session.Kind = DocumentKind.Unknown;
                    session.IsReadOnly = true;
                    break;
            }
        }

        private static void LogOpenedDocument(DocumentSession session, int highlightedLine, bool existing, CortexShellState state)
        {
            if (session == null)
            {
                return;
            }

            string decompilerReason;
            var decompilerMatch = TryGetDecompilerDocumentPathReason(state, session.FilePath, out decompilerReason);
            MMLog.WriteDebug("[Cortex.Documents] " +
                (existing ? "Activated" : "Opened") +
                " document. Path=" + (session.FilePath ?? string.Empty) +
                ", Kind=" + session.Kind +
                ", ReadOnly=" + session.IsReadOnly +
                ", SupportsEditing=" + session.SupportsEditing +
                ", SupportsSaving=" + session.SupportsSaving +
                ", HighlightedLine=" + highlightedLine +
                ", DecompiledMatch=" + decompilerMatch +
                ", MatchReason=" + (decompilerReason ?? string.Empty) + ".");
        }

        private static DocumentKind ResolveDocumentKind(string filePath, DocumentKind preferredKind, CortexShellState state)
        {
            if (preferredKind != DocumentKind.Unknown)
            {
                return preferredKind;
            }

            if (IsDecompilerDocumentPath(state, filePath))
            {
                return DocumentKind.DecompiledCode;
            }

            var extension = !string.IsNullOrEmpty(filePath)
                ? Path.GetExtension(filePath) ?? string.Empty
                : string.Empty;
            if (string.Equals(extension, ".cs", StringComparison.OrdinalIgnoreCase))
            {
                return DocumentKind.SourceCode;
            }

            if (string.Equals(extension, ".log", StringComparison.OrdinalIgnoreCase))
            {
                return DocumentKind.Log;
            }

            if (string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
            {
                return DocumentKind.Text;
            }

            return DocumentKind.Unknown;
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
