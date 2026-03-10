using System;
using System.IO;
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
            var opened = CortexModuleUtil.OpenDocument(_documentService, state, filePath, highlightedLine);
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
            var opened = CortexModuleUtil.OpenDecompilerResult(_documentService, state, response);
            if (opened)
            {
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
            var lineNumber = definitionRange != null ? definitionRange.StartLine : 0;
            if (!string.IsNullOrEmpty(definitionDocumentPath) && File.Exists(definitionDocumentPath))
            {
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

            return DecompileAndOpen(
                state,
                assemblyPath,
                metadataToken,
                entityKind,
                false,
                !string.IsNullOrEmpty(successStatusMessage) ? successStatusMessage : "Opened decompiled definition: " + displayName,
                failureStatusMessage);
        }

    }
}
