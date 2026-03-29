using System;
using System.IO;
using System.Text.RegularExpressions;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
namespace Cortex.Services
{
    public sealed class CortexNavigationService
    {
        private readonly IDocumentService _documentService;
        private readonly ISourceReferenceService _sourceReferenceService;
        private readonly IRuntimeSourceNavigationService _runtimeSourceNavigationService;
        private readonly ISourceLookupIndex _sourceLookupIndex;
        private readonly SourcePreferredDocumentResolver _sourcePreferredDocumentResolver = new SourcePreferredDocumentResolver();

        public CortexNavigationService(
            IDocumentService documentService,
            ISourceReferenceService sourceReferenceService,
            IRuntimeSourceNavigationService runtimeSourceNavigationService)
            : this(documentService, sourceReferenceService, runtimeSourceNavigationService, null)
        {
        }

        public CortexNavigationService(
            IDocumentService documentService,
            ISourceReferenceService sourceReferenceService,
            IRuntimeSourceNavigationService runtimeSourceNavigationService,
            ISourceLookupIndex sourceLookupIndex)
        {
            _documentService = documentService;
            _sourceReferenceService = sourceReferenceService;
            _runtimeSourceNavigationService = runtimeSourceNavigationService;
            _sourceLookupIndex = sourceLookupIndex;
        }

        public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage)
        {
            var kind = CortexModuleUtil.IsDecompilerDocumentPath(state, filePath)
                ? DocumentKind.DecompiledCode
                : DocumentKind.Unknown;
            var opened = CortexModuleUtil.OpenDocument(_documentService, state, filePath, highlightedLine, kind);
            if (opened != null)
            {
                state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
                if (!string.IsNullOrEmpty(successStatusMessage))
                {
                    state.StatusMessage = successStatusMessage;
                }

                return opened;
            }

            if (!string.IsNullOrEmpty(failureStatusMessage))
            {
                state.StatusMessage = failureStatusMessage;
            }

            return null;
        }

        public void PreloadDocument(CortexShellState state, string filePath)
        {
            if (_documentService == null || state == null || string.IsNullOrEmpty(filePath))
            {
                return;
            }

            if (CortexModuleUtil.FindOpenDocument(state, filePath) != null)
            {
                return;
            }

            _documentService.Preload(filePath);
        }

        public void PreloadHoverResponseTarget(CortexShellState state, LanguageServiceHoverResponse response)
        {
            if (response == null)
            {
                return;
            }

            PreloadDocument(state, response.DefinitionDocumentPath);
        }

        public void PreloadHoverDisplayPartTarget(CortexShellState state, LanguageServiceHoverDisplayPart part)
        {
            if (part == null || !part.IsInteractive)
            {
                return;
            }

            PreloadDocument(state, part.DefinitionDocumentPath);
        }

        public void PreloadHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target)
        {
            if (target == null)
            {
                return;
            }

            PreloadDocument(state, target.DefinitionDocumentPath);
        }

        public DecompilerResponse RequestDecompilerSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache)
        {
            return CortexModuleUtil.RequestDecompilerSource(
                _sourceReferenceService,
                state,
                assemblyPath,
                metadataToken,
                entityKind,
                ignoreCache);
        }

        public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, string successStatusMessage, string failureStatusMessage)
        {
            return OpenDecompilerResult(state, response, 1, successStatusMessage, failureStatusMessage);
        }

        public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, int highlightedLine, string successStatusMessage, string failureStatusMessage)
        {
            var lineNumber = highlightedLine > 0 ? highlightedLine : 1;
            var opened = response != null && _documentService != null && state != null
                ? CortexModuleUtil.OpenDocument(_documentService, state, response.CachePath, lineNumber, DocumentKind.DecompiledCode)
                : null;
            if (opened != null)
            {
                state.Workbench.RequestedContainerId = CortexWorkbenchIds.EditorContainer;
                if (!string.IsNullOrEmpty(successStatusMessage))
                {
                    state.StatusMessage = successStatusMessage;
                }

                return true;
            }

            if (!string.IsNullOrEmpty(failureStatusMessage))
            {
                state.StatusMessage = failureStatusMessage;
            }

            return false;
        }

        public bool DecompileAndOpen(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage)
        {
            if (entityKind == DecompilerEntityKind.Method)
            {
                return OpenDecompilerMethodTarget(
                    state,
                    assemblyPath,
                    metadataToken,
                    string.Empty,
                    string.Empty,
                    string.Empty,
                    ignoreCache,
                    successStatusMessage,
                    failureStatusMessage);
            }

            if (entityKind == DecompilerEntityKind.Type)
            {
                string fullTypeName;
                if (MetadataNavigationResolver.TryResolveTypeNavigationTarget(assemblyPath, metadataToken, out fullTypeName))
                {
                    string sourceDocumentPath;
                    if (_sourcePreferredDocumentResolver.TryResolveFromTypeName(state, _sourceLookupIndex, fullTypeName, out sourceDocumentPath))
                    {
                        var sourceLine = ResolveSourceNavigationLine(
                            ReadAllTextSafe(sourceDocumentPath),
                            "NamedType",
                            GetTypeLeafName(fullTypeName),
                            fullTypeName);
                        return OpenDocument(
                            state,
                            sourceDocumentPath,
                            sourceLine,
                            successStatusMessage,
                            failureStatusMessage) != null;
                    }
                }
            }

            var response = RequestDecompilerSource(state, assemblyPath, metadataToken, entityKind, ignoreCache);
            if (response == null)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            return OpenDecompilerResult(state, response, successStatusMessage, failureStatusMessage);
        }

        public bool OpenDecompilerMethodTarget(
            CortexShellState state,
            string assemblyPath,
            int methodMetadataToken,
            string metadataName,
            string containingTypeName,
            string symbolKind,
            bool ignoreCache,
            string successStatusMessage,
            string failureStatusMessage)
        {
            int declaringTypeMetadataToken;
            string resolvedMethodName;
            string resolvedContainingTypeName;
            string resolvedSymbolKind;
            if (!MetadataNavigationResolver.TryResolveMethodNavigationTarget(
                assemblyPath,
                methodMetadataToken,
                out declaringTypeMetadataToken,
                out resolvedMethodName,
                out resolvedContainingTypeName,
                out resolvedSymbolKind))
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            var effectiveMetadataName = !string.IsNullOrEmpty(metadataName) ? metadataName : resolvedMethodName;
            var effectiveContainingTypeName = !string.IsNullOrEmpty(containingTypeName) ? containingTypeName : resolvedContainingTypeName;
            var effectiveSymbolKind = !string.IsNullOrEmpty(symbolKind) ? symbolKind : resolvedSymbolKind;
            string sourceDocumentPath;
            if (_sourcePreferredDocumentResolver.TryResolveFromSymbol(
                state,
                _sourceLookupIndex,
                string.Empty,
                string.Empty,
                effectiveContainingTypeName,
                effectiveMetadataName,
                effectiveSymbolKind,
                out sourceDocumentPath))
            {
                var sourceLine = ResolveSourceNavigationLine(
                    ReadAllTextSafe(sourceDocumentPath),
                    effectiveSymbolKind,
                    effectiveMetadataName,
                    effectiveContainingTypeName);
                return OpenDocument(state, sourceDocumentPath, sourceLine, successStatusMessage, failureStatusMessage) != null;
            }

            int highlightedLine;
            var decompiled = RequestDecompilerMethodView(
                state,
                assemblyPath,
                methodMetadataToken,
                effectiveMetadataName,
                effectiveContainingTypeName,
                effectiveSymbolKind,
                ignoreCache,
                out highlightedLine);
            if (decompiled == null)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            return OpenDecompilerResult(state, decompiled, highlightedLine, successStatusMessage, failureStatusMessage);
        }

        public DecompilerResponse RequestDecompilerMethodView(
            CortexShellState state,
            string assemblyPath,
            int methodMetadataToken,
            string metadataName,
            string containingTypeName,
            string symbolKind,
            bool ignoreCache,
            out int highlightedLine)
        {
            highlightedLine = 1;
            if (state == null || string.IsNullOrEmpty(assemblyPath) || methodMetadataToken <= 0)
            {
                return null;
            }

            int declaringTypeMetadataToken;
            string resolvedMethodName;
            string resolvedContainingTypeName;
            string resolvedSymbolKind;
            if (!MetadataNavigationResolver.TryResolveMethodNavigationTarget(
                assemblyPath,
                methodMetadataToken,
                out declaringTypeMetadataToken,
                out resolvedMethodName,
                out resolvedContainingTypeName,
                out resolvedSymbolKind))
            {
                return null;
            }

            var decompiled = RequestDecompilerSource(state, assemblyPath, declaringTypeMetadataToken, DecompilerEntityKind.Type, ignoreCache);
            if (decompiled == null)
            {
                return null;
            }

            var effectiveMetadataName = !string.IsNullOrEmpty(metadataName) ? metadataName : resolvedMethodName;
            var effectiveContainingTypeName = !string.IsNullOrEmpty(containingTypeName) ? containingTypeName : resolvedContainingTypeName;
            var effectiveSymbolKind = !string.IsNullOrEmpty(symbolKind) ? symbolKind : resolvedSymbolKind;
            highlightedLine = ResolveSourceNavigationLine(
                !string.IsNullOrEmpty(decompiled.SourceText) ? decompiled.SourceText : ReadAllTextSafe(decompiled.CachePath),
                effectiveSymbolKind,
                effectiveMetadataName,
                effectiveContainingTypeName);

            MMLog.WriteInfo("[Cortex.Navigation] Prepared decompiled method view via declaring type. CachePath=" + (decompiled.CachePath ?? string.Empty) +
                ", Line=" + highlightedLine +
                ", MethodToken=0x" + methodMetadataToken.ToString("X8") +
                ", DeclaringTypeToken=0x" + declaringTypeMetadataToken.ToString("X8") + ".");

            return decompiled;
        }

        public SourceNavigationTarget ResolveRuntimeTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state)
        {
            return _runtimeSourceNavigationService != null
                ? _runtimeSourceNavigationService.Resolve(entry, frameIndex, state.SelectedProject, state.Settings)
                : null;
        }

        public bool OpenRuntimeTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage)
        {
            if (target == null || !target.Success || string.IsNullOrEmpty(target.FilePath))
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }
                else if (target != null && !string.IsNullOrEmpty(target.StatusMessage))
                {
                    state.StatusMessage = target.StatusMessage;
                }

                return false;
            }

            return OpenDocument(
                state,
                target.FilePath,
                target.LineNumber,
                !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : target.StatusMessage,
                failureStatusMessage) != null;
        }

        public bool OpenHoverDisplayPart(CortexShellState state, LanguageServiceHoverDisplayPart part, string successStatusMessage, string failureStatusMessage)
        {
            if (part == null || !part.IsInteractive)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            return OpenLanguageSymbolTarget(
                state,
                part.SymbolDisplay,
                part.SymbolKind,
                part.MetadataName,
                part.ContainingTypeName,
                part.ContainingAssemblyName,
                part.DocumentationCommentId,
                part.DefinitionDocumentPath,
                part.DefinitionRange,
                successStatusMessage,
                failureStatusMessage);
        }

        public bool OpenHoverNavigationTarget(CortexShellState state, EditorHoverNavigationTarget target, string successStatusMessage, string failureStatusMessage)
        {
            if (target == null || target.Kind == EditorHoverNavigationKind.None)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            return OpenLanguageSymbolTarget(
                state,
                target.SymbolDisplay,
                target.SymbolKind,
                target.MetadataName,
                target.ContainingTypeName,
                target.ContainingAssemblyName,
                target.DocumentationCommentId,
                target.DefinitionDocumentPath,
                target.DefinitionRange,
                successStatusMessage,
                failureStatusMessage);
        }

        public bool OpenLanguageSymbolTarget(
            CortexShellState state,
            string symbolDisplay,
            string symbolKind,
            string metadataName,
            string containingTypeName,
            string containingAssemblyName,
            string documentationCommentId,
            string definitionDocumentPath,
            LanguageServiceRange definitionRange,
            string successStatusMessage,
            string failureStatusMessage)
        {
            var displayName = !string.IsNullOrEmpty(symbolDisplay) ? symbolDisplay : metadataName ?? string.Empty;
            var lineNumber = GetPreferredNavigationLine(definitionRange);
            if (!string.IsNullOrEmpty(definitionDocumentPath) &&
                File.Exists(definitionDocumentPath) &&
                !CortexModuleUtil.IsDecompilerDocumentPath(state, definitionDocumentPath))
            {
                if (lineNumber <= 0)
                {
                    lineNumber = ResolveSourceNavigationLine(
                        ReadAllTextSafe(definitionDocumentPath),
                        symbolKind,
                        metadataName,
                        containingTypeName);
                }

                MMLog.WriteInfo("[Cortex.Navigation] Opening source symbol target. Symbol=" + displayName +
                    ", File=" + definitionDocumentPath +
                    ", Line=" + lineNumber +
                    ", HasRange=" + (definitionRange != null) + ".");
                return OpenDocument(
                    state,
                    definitionDocumentPath,
                    lineNumber,
                    successStatusMessage,
                    failureStatusMessage) != null;
            }

            string assemblyPath;
            if (!MetadataNavigationResolver.TryResolveAssemblyPath(state, _sourceLookupIndex, containingAssemblyName, out assemblyPath))
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            string sourceDocumentPath;
            if (_sourcePreferredDocumentResolver.TryResolveFromSymbol(
                state,
                _sourceLookupIndex,
                definitionDocumentPath,
                documentationCommentId,
                containingTypeName,
                metadataName,
                symbolKind,
                out sourceDocumentPath))
            {
                var sourceLine = ShouldTrustSourceLineMapping(state, definitionDocumentPath, sourceDocumentPath) && lineNumber > 0
                    ? lineNumber
                    : ResolveSourceNavigationLine(
                        ReadAllTextSafe(sourceDocumentPath),
                        symbolKind,
                        metadataName,
                        containingTypeName);

                MMLog.WriteInfo("[Cortex.Navigation] Opening preferred source symbol target. Symbol=" + displayName +
                    ", File=" + sourceDocumentPath +
                    ", Line=" + sourceLine +
                    ", SourcePreferred=True.");
                return OpenDocument(
                    state,
                    sourceDocumentPath,
                    sourceLine,
                    successStatusMessage,
                    failureStatusMessage) != null;
            }

            int metadataToken;
            DecompilerEntityKind entityKind;
            if (!MetadataNavigationResolver.TryResolveMetadataTarget(assemblyPath, documentationCommentId, containingTypeName, symbolKind, out metadataToken, out entityKind))
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            if (entityKind == DecompilerEntityKind.Method)
            {
                return OpenDecompilerMethodTarget(
                    state,
                    assemblyPath,
                    metadataToken,
                    metadataName,
                    containingTypeName,
                    symbolKind,
                    false,
                    !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : "Opened decompiled definition: " + displayName,
                    failureStatusMessage);
            }

            var decompiled = RequestDecompilerSource(state, assemblyPath, metadataToken, entityKind, false);
            if (decompiled == null)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            var decompiledLine = ResolveSourceNavigationLine(
                !string.IsNullOrEmpty(decompiled.SourceText) ? decompiled.SourceText : ReadAllTextSafe(decompiled.CachePath),
                symbolKind,
                metadataName,
                containingTypeName);

            MMLog.WriteInfo("[Cortex.Navigation] Opening decompiled symbol target. Symbol=" + displayName +
                ", CachePath=" + (decompiled.CachePath ?? string.Empty) +
                ", Line=" + decompiledLine +
                ", EntityKind=" + entityKind +
                ", MetadataToken=0x" + metadataToken.ToString("X8") + ".");
            return OpenDecompilerResult(
                state,
                decompiled,
                decompiledLine,
                !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : "Opened decompiled definition: " + displayName,
                failureStatusMessage);
        }

        private static int GetPreferredNavigationLine(LanguageServiceRange definitionRange)
        {
            return definitionRange != null && definitionRange.StartLine > 0
                ? definitionRange.StartLine
                : 0;
        }

        private static bool ShouldTrustSourceLineMapping(CortexShellState state, string definitionDocumentPath, string sourceDocumentPath)
        {
            return !string.IsNullOrEmpty(definitionDocumentPath) &&
                !string.IsNullOrEmpty(sourceDocumentPath) &&
                File.Exists(definitionDocumentPath) &&
                !CortexModuleUtil.IsDecompilerDocumentPath(state, definitionDocumentPath) &&
                PathsEqual(definitionDocumentPath, sourceDocumentPath);
        }

        private static int ResolveSourceNavigationLine(string sourceText, string symbolKind, string metadataName, string containingTypeName)
        {
            var lines = CortexModuleUtil.SplitLines(sourceText);
            if (lines == null || lines.Length == 0)
            {
                return 1;
            }

            var symbolName = GetNavigationSymbolName(metadataName, containingTypeName);
            if (string.IsNullOrEmpty(symbolName))
            {
                return 1;
            }

            var declarationPattern = BuildDeclarationPattern(symbolKind, symbolName, containingTypeName);
            if (!string.IsNullOrEmpty(declarationPattern))
            {
                var declarationLine = FindPatternLine(lines, declarationPattern);
                if (declarationLine > 0)
                {
                    return declarationLine;
                }
            }

            var symbolPattern = "\\b" + Regex.Escape(symbolName) + "\\b";
            var symbolLine = FindPatternLine(lines, symbolPattern);
            return symbolLine > 0 ? symbolLine : 1;
        }

        private static string GetTypeLeafName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return string.Empty;
            }

            var normalized = fullTypeName.Replace('+', '.');
            var lastDot = normalized.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < normalized.Length
                ? normalized.Substring(lastDot + 1)
                : normalized;
        }

        private static int FindPatternLine(string[] lines, string pattern)
        {
            if (lines == null || lines.Length == 0 || string.IsNullOrEmpty(pattern))
            {
                return 0;
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i] ?? string.Empty;
                if (Regex.IsMatch(line, pattern))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static bool PathsEqual(string left, string right)
        {
            if (string.IsNullOrEmpty(left) || string.IsNullOrEmpty(right))
            {
                return false;
            }

            try
            {
                return string.Equals(
                    Path.GetFullPath(left),
                    Path.GetFullPath(right),
                    StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string GetNavigationSymbolName(string metadataName, string containingTypeName)
        {
            if (!string.IsNullOrEmpty(metadataName))
            {
                return metadataName;
            }

            if (string.IsNullOrEmpty(containingTypeName))
            {
                return string.Empty;
            }

            var lastDot = containingTypeName.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < containingTypeName.Length
                ? containingTypeName.Substring(lastDot + 1)
                : containingTypeName;
        }

        private static string BuildDeclarationPattern(string symbolKind, string symbolName, string containingTypeName)
        {
            var normalizedKind = (symbolKind ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(symbolName))
            {
                return string.Empty;
            }

            if (IsTypeLikeSymbol(normalizedKind))
            {
                return "\\b(class|struct|interface|enum|delegate|record)\\s+" + Regex.Escape(symbolName) + "\\b";
            }

            if (string.Equals(normalizedKind, "Method", StringComparison.OrdinalIgnoreCase))
            {
                return "\\b" + Regex.Escape(symbolName) + "\\s*(<[^>]+>\\s*)?\\(";
            }

            if (string.Equals(normalizedKind, "Constructor", StringComparison.OrdinalIgnoreCase))
            {
                var typeName = !string.IsNullOrEmpty(containingTypeName)
                    ? GetNavigationSymbolName(string.Empty, containingTypeName)
                    : GetNavigationSymbolName(symbolName, containingTypeName);
                return "\\b" + Regex.Escape(typeName) + "\\s*\\(";
            }

            if (string.Equals(normalizedKind, "Property", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedKind, "Event", StringComparison.OrdinalIgnoreCase))
            {
                return "\\b" + Regex.Escape(symbolName) + "\\b[^\\n]*\\{";
            }

            if (string.Equals(normalizedKind, "Field", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedKind, "EnumMember", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedKind, "Constant", StringComparison.OrdinalIgnoreCase))
            {
                return "\\b" + Regex.Escape(symbolName) + "\\b\\s*(=|;|,)";
            }

            return "\\b" + Regex.Escape(symbolName) + "\\b";
        }

        private static bool IsTypeLikeSymbol(string symbolKind)
        {
            return string.Equals(symbolKind, "NamedType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Class", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Struct", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Interface", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Enum", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Delegate", StringComparison.OrdinalIgnoreCase);
        }

        private static string ReadAllTextSafe(string filePath)
        {
            try
            {
                return !string.IsNullOrEmpty(filePath) && File.Exists(filePath)
                    ? File.ReadAllText(filePath)
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

    }
}
