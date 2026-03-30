using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Navigation.Decompiler;
using Cortex.Services.Navigation.Document;
using Cortex.Services.Navigation.Metadata;
using Cortex.Services.Navigation.Runtime;
using Cortex.Services.Navigation.Source;
using Cortex.Services.Navigation.Symbols;

namespace Cortex.Services.Navigation
{
    public sealed class CortexNavigationService : ICortexNavigationService
    {
        private readonly INavigationDocumentService _documentService;
        private readonly IDecompilerNavigationService _decompilerNavigationService;
        private readonly ILanguageSymbolNavigationService _languageSymbolNavigationService;
        private readonly IRuntimeNavigationTargetService _runtimeNavigationTargetService;

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
            : this(
                CreateDocumentService(documentService),
                CreateDecompilerNavigationService(documentService, sourceReferenceService, sourceLookupIndex),
                CreateLanguageSymbolNavigationService(documentService, sourceReferenceService, sourceLookupIndex),
                new RuntimeNavigationTargetService(runtimeSourceNavigationService, CreateDocumentService(documentService)))
        {
        }

        internal CortexNavigationService(
            INavigationDocumentService documentService,
            IDecompilerNavigationService decompilerNavigationService,
            ILanguageSymbolNavigationService languageSymbolNavigationService,
            IRuntimeNavigationTargetService runtimeNavigationTargetService)
        {
            _documentService = documentService;
            _decompilerNavigationService = decompilerNavigationService;
            _languageSymbolNavigationService = languageSymbolNavigationService;
            _runtimeNavigationTargetService = runtimeNavigationTargetService;
        }

        public DocumentSession OpenDocument(CortexShellState state, string filePath, int highlightedLine, string successStatusMessage, string failureStatusMessage)
        {
            return _documentService.OpenDocument(state, filePath, highlightedLine, successStatusMessage, failureStatusMessage);
        }

        public void PreloadDocument(CortexShellState state, string filePath)
        {
            _documentService.PreloadDocument(state, filePath);
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
            return _decompilerNavigationService.RequestSource(state, assemblyPath, metadataToken, entityKind, ignoreCache);
        }

        public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, string successStatusMessage, string failureStatusMessage)
        {
            return OpenDecompilerResult(state, response, 1, successStatusMessage, failureStatusMessage);
        }

        public bool OpenDecompilerResult(CortexShellState state, DecompilerResponse response, int highlightedLine, string successStatusMessage, string failureStatusMessage)
        {
            return _documentService.OpenDecompilerResult(state, response, highlightedLine, successStatusMessage, failureStatusMessage);
        }

        public bool DecompileAndOpen(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage)
        {
            return _decompilerNavigationService.OpenEntityTarget(state, assemblyPath, metadataToken, entityKind, ignoreCache, successStatusMessage, failureStatusMessage);
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
            return _decompilerNavigationService.OpenMethodTarget(
                state,
                assemblyPath,
                methodMetadataToken,
                metadataName,
                containingTypeName,
                symbolKind,
                ignoreCache,
                successStatusMessage,
                failureStatusMessage);
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
            return _decompilerNavigationService.RequestMethodView(
                state,
                assemblyPath,
                methodMetadataToken,
                metadataName,
                containingTypeName,
                symbolKind,
                ignoreCache,
                out highlightedLine);
        }

        public SourceNavigationTarget ResolveRuntimeTarget(RuntimeLogEntry entry, int frameIndex, CortexShellState state)
        {
            return _runtimeNavigationTargetService.ResolveTarget(entry, frameIndex, state);
        }

        public bool OpenRuntimeTarget(CortexShellState state, SourceNavigationTarget target, string successStatusMessage, string failureStatusMessage)
        {
            return _runtimeNavigationTargetService.OpenTarget(state, target, successStatusMessage, failureStatusMessage);
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
            return _languageSymbolNavigationService.OpenTarget(
                state,
                new LanguageSymbolNavigationRequest
                {
                    SymbolDisplay = symbolDisplay ?? string.Empty,
                    SymbolKind = symbolKind ?? string.Empty,
                    MetadataName = metadataName ?? string.Empty,
                    ContainingTypeName = containingTypeName ?? string.Empty,
                    ContainingAssemblyName = containingAssemblyName ?? string.Empty,
                    DocumentationCommentId = documentationCommentId ?? string.Empty,
                    DefinitionDocumentPath = definitionDocumentPath ?? string.Empty,
                    DefinitionRange = definitionRange
                },
                successStatusMessage,
                failureStatusMessage);
        }

        private static INavigationDocumentService CreateDocumentService(IDocumentService documentService)
        {
            return new NavigationDocumentService(documentService);
        }

        private static IDecompilerNavigationService CreateDecompilerNavigationService(IDocumentService documentService, ISourceReferenceService sourceReferenceService, ISourceLookupIndex sourceLookupIndex)
        {
            var navigationDocumentService = CreateDocumentService(documentService);
            var metadataNavigationService = new AssemblyMetadataNavigationService();
            var sourcePreferredResolver = new SourcePreferredNavigationResolver();
            var lineResolver = new SourceNavigationLineResolver();
            return new DecompilerNavigationService(
                sourceReferenceService,
                sourceLookupIndex,
                navigationDocumentService,
                metadataNavigationService,
                sourcePreferredResolver,
                lineResolver);
        }

        private static ILanguageSymbolNavigationService CreateLanguageSymbolNavigationService(IDocumentService documentService, ISourceReferenceService sourceReferenceService, ISourceLookupIndex sourceLookupIndex)
        {
            var navigationDocumentService = CreateDocumentService(documentService);
            var metadataNavigationService = new AssemblyMetadataNavigationService();
            var sourcePreferredResolver = new SourcePreferredNavigationResolver();
            var lineResolver = new SourceNavigationLineResolver();
            var decompilerNavigationService = new DecompilerNavigationService(
                sourceReferenceService,
                sourceLookupIndex,
                navigationDocumentService,
                metadataNavigationService,
                sourcePreferredResolver,
                lineResolver);
            return new LanguageSymbolNavigationService(
                sourceLookupIndex,
                navigationDocumentService,
                sourcePreferredResolver,
                lineResolver,
                metadataNavigationService,
                decompilerNavigationService);
        }
    }
}
