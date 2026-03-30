using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Harmony.Navigation;
using Cortex.Services.Navigation.Decompiler;
using Cortex.Services.Navigation.Document;
using Cortex.Services.Navigation.Metadata;
using Cortex.Services.Navigation.Source;

namespace Cortex.Services.Navigation.Symbols
{
    internal interface ILanguageSymbolNavigationService
    {
        bool OpenTarget(CortexShellState state, LanguageSymbolNavigationRequest request, string successStatusMessage, string failureStatusMessage);
    }

    internal sealed class LanguageSymbolNavigationService : ILanguageSymbolNavigationService
    {
        private readonly ISourceLookupIndex _sourceLookupIndex;
        private readonly INavigationDocumentService _documentService;
        private readonly ISourcePreferredNavigationResolver _sourcePreferredResolver;
        private readonly ISourceNavigationLineResolver _lineResolver;
        private readonly IAssemblyMetadataNavigationService _metadataNavigationService;
        private readonly IDecompilerNavigationService _decompilerNavigationService;

        public LanguageSymbolNavigationService(
            ISourceLookupIndex sourceLookupIndex,
            INavigationDocumentService documentService,
            ISourcePreferredNavigationResolver sourcePreferredResolver,
            ISourceNavigationLineResolver lineResolver,
            IAssemblyMetadataNavigationService metadataNavigationService,
            IDecompilerNavigationService decompilerNavigationService)
        {
            _sourceLookupIndex = sourceLookupIndex;
            _documentService = documentService;
            _sourcePreferredResolver = sourcePreferredResolver;
            _lineResolver = lineResolver;
            _metadataNavigationService = metadataNavigationService;
            _decompilerNavigationService = decompilerNavigationService;
        }

        public bool OpenTarget(CortexShellState state, LanguageSymbolNavigationRequest request, string successStatusMessage, string failureStatusMessage)
        {
            if (state == null || request == null)
            {
                return false;
            }

            var displayName = !string.IsNullOrEmpty(request.SymbolDisplay) ? request.SymbolDisplay : request.MetadataName ?? string.Empty;
            var lineNumber = GetPreferredNavigationLine(request.DefinitionRange);
            if (!string.IsNullOrEmpty(request.DefinitionDocumentPath) &&
                File.Exists(request.DefinitionDocumentPath) &&
                !CortexModuleUtil.IsDecompilerDocumentPath(state, request.DefinitionDocumentPath))
            {
                if (lineNumber <= 0)
                {
                    lineNumber = _lineResolver.ResolveLine(
                        SourceNavigationLineResolver.ReadAllTextSafe(request.DefinitionDocumentPath),
                        request.SymbolKind,
                        request.MetadataName,
                        request.ContainingTypeName);
                }

                MMLog.WriteInfo("[Cortex.Navigation] Opening source symbol target. Symbol=" + displayName +
                    ", File=" + request.DefinitionDocumentPath +
                    ", Line=" + lineNumber +
                    ", HasRange=" + (request.DefinitionRange != null) + ".");
                return _documentService.OpenDocument(
                    state,
                    request.DefinitionDocumentPath,
                    lineNumber,
                    successStatusMessage,
                    failureStatusMessage) != null;
            }

            HarmonyPatchNavigationTarget harmonyTarget;
            if (HarmonyAssociatedSymbolNavigationResolver.TryResolvePreferredPatchTarget(state, request, out harmonyTarget))
            {
                MMLog.WriteInfo("[Cortex.Navigation] Redirecting symbol target to associated Harmony patch. Symbol=" + displayName +
                    ", PatchTarget=" + (harmonyTarget != null ? harmonyTarget.DisplayName ?? harmonyTarget.MethodName ?? string.Empty : string.Empty) + ".");
                return TryOpenHarmonyPatchTarget(
                    state,
                    harmonyTarget,
                    "Opened Harmony patch method.",
                    failureStatusMessage);
            }

            string assemblyPath;
            if (!_metadataNavigationService.TryResolveAssemblyPath(state, _sourceLookupIndex, request.ContainingAssemblyName, out assemblyPath))
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            string sourceDocumentPath;
            if (_sourcePreferredResolver.TryResolveFromSymbol(state, _sourceLookupIndex, request, out sourceDocumentPath))
            {
                var sourceLine = ShouldTrustSourceLineMapping(state, request.DefinitionDocumentPath, sourceDocumentPath) && lineNumber > 0
                    ? lineNumber
                    : _lineResolver.ResolveLine(
                        SourceNavigationLineResolver.ReadAllTextSafe(sourceDocumentPath),
                        request.SymbolKind,
                        request.MetadataName,
                        request.ContainingTypeName);

                MMLog.WriteInfo("[Cortex.Navigation] Opening preferred source symbol target. Symbol=" + displayName +
                    ", File=" + sourceDocumentPath +
                    ", Line=" + sourceLine +
                    ", SourcePreferred=True.");
                return _documentService.OpenDocument(
                    state,
                    sourceDocumentPath,
                    sourceLine,
                    successStatusMessage,
                    failureStatusMessage) != null;
            }

            MetadataNavigationTarget metadataTarget;
            if (!_metadataNavigationService.TryResolveMetadataTarget(
                assemblyPath,
                request.DocumentationCommentId,
                request.ContainingTypeName,
                request.SymbolKind,
                out metadataTarget) ||
                metadataTarget == null)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            if (metadataTarget.EntityKind == DecompilerEntityKind.Method)
            {
                return _decompilerNavigationService.OpenMethodTarget(
                    state,
                    assemblyPath,
                    metadataTarget.MetadataToken,
                    request.MetadataName,
                    request.ContainingTypeName,
                    request.SymbolKind,
                    false,
                    !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : "Opened decompiled definition: " + displayName,
                    failureStatusMessage);
            }

            var decompiled = _decompilerNavigationService.RequestSource(state, assemblyPath, metadataTarget.MetadataToken, metadataTarget.EntityKind, false);
            if (decompiled == null)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            var decompiledLine = _lineResolver.ResolveLine(
                !string.IsNullOrEmpty(decompiled.SourceText) ? decompiled.SourceText : SourceNavigationLineResolver.ReadAllTextSafe(decompiled.CachePath),
                request.SymbolKind,
                request.MetadataName,
                request.ContainingTypeName);

            MMLog.WriteInfo("[Cortex.Navigation] Opening decompiled symbol target. Symbol=" + displayName +
                ", CachePath=" + (decompiled.CachePath ?? string.Empty) +
                ", Line=" + decompiledLine +
                ", EntityKind=" + metadataTarget.EntityKind +
                ", MetadataToken=0x" + metadataTarget.MetadataToken.ToString("X8") + ".");
            return _documentService.OpenDecompilerResult(
                state,
                decompiled,
                decompiledLine,
                !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : "Opened decompiled definition: " + displayName,
                failureStatusMessage);
        }

        private static int GetPreferredNavigationLine(Cortex.LanguageService.Protocol.LanguageServiceRange definitionRange)
        {
            return definitionRange != null && definitionRange.StartLine > 0
                ? definitionRange.StartLine
                : 0;
        }

        private bool TryOpenHarmonyPatchTarget(
            CortexShellState state,
            HarmonyPatchNavigationTarget target,
            string successStatusMessage,
            string failureStatusMessage)
        {
            if (state == null || target == null)
            {
                MMLog.WriteInfo("[Cortex.Navigation] Harmony patch target open skipped. Reason='missing-state-or-target'.");
                return false;
            }

            if (!string.IsNullOrEmpty(target.DocumentPath) &&
                File.Exists(target.DocumentPath) &&
                !CortexModuleUtil.IsDecompilerDocumentPath(state, target.DocumentPath))
            {
                return _documentService.OpenDocument(
                    state,
                    target.DocumentPath,
                    target.Line > 0 ? target.Line : 1,
                    successStatusMessage,
                    failureStatusMessage) != null;
            }

            if (!string.IsNullOrEmpty(target.AssemblyPath) && target.MetadataToken > 0)
            {
                return _decompilerNavigationService.OpenEntityTarget(
                    state,
                    target.AssemblyPath,
                    target.MetadataToken,
                    DecompilerEntityKind.Method,
                    false,
                    successStatusMessage,
                    failureStatusMessage);
            }

            if (!string.IsNullOrEmpty(target.CachePath) && File.Exists(target.CachePath))
            {
                return _documentService.OpenDocument(
                    state,
                    target.CachePath,
                    target.Line > 0 ? target.Line : 1,
                    successStatusMessage,
                    failureStatusMessage) != null;
            }

            if (!string.IsNullOrEmpty(target.DocumentPath) && File.Exists(target.DocumentPath))
            {
                return _documentService.OpenDocument(
                    state,
                    target.DocumentPath,
                    target.Line > 0 ? target.Line : 1,
                    successStatusMessage,
                    failureStatusMessage) != null;
            }

            if (!string.IsNullOrEmpty(failureStatusMessage))
            {
                state.StatusMessage = failureStatusMessage;
            }

            MMLog.WriteInfo("[Cortex.Navigation] Harmony patch target open failed. AssemblyPath='" +
                (target.AssemblyPath ?? string.Empty) + "', MetadataToken=0x" + target.MetadataToken.ToString("X8") +
                ", DocumentPath='" + (target.DocumentPath ?? string.Empty) + "', CachePath='" + (target.CachePath ?? string.Empty) + "'.");

            return false;
        }

        private static bool ShouldTrustSourceLineMapping(CortexShellState state, string definitionDocumentPath, string sourceDocumentPath)
        {
            return !string.IsNullOrEmpty(definitionDocumentPath) &&
                !string.IsNullOrEmpty(sourceDocumentPath) &&
                File.Exists(definitionDocumentPath) &&
                !CortexModuleUtil.IsDecompilerDocumentPath(state, definitionDocumentPath) &&
                PathsEqual(definitionDocumentPath, sourceDocumentPath);
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
    }
}
