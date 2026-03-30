using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Navigation.Document;
using Cortex.Services.Navigation.Metadata;
using Cortex.Services.Navigation.Source;

namespace Cortex.Services.Navigation.Decompiler
{
    internal interface IDecompilerNavigationService
    {
        DecompilerResponse RequestSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache);
        bool OpenEntityTarget(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage);
        bool OpenMethodTarget(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage);
        DecompilerResponse RequestMethodView(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, out int highlightedLine);
    }

    internal sealed class DecompilerNavigationService : IDecompilerNavigationService
    {
        private readonly ISourceReferenceService _sourceReferenceService;
        private readonly ISourceLookupIndex _sourceLookupIndex;
        private readonly INavigationDocumentService _documentService;
        private readonly IAssemblyMetadataNavigationService _metadataNavigationService;
        private readonly ISourcePreferredNavigationResolver _sourcePreferredResolver;
        private readonly ISourceNavigationLineResolver _lineResolver;

        public DecompilerNavigationService(
            ISourceReferenceService sourceReferenceService,
            ISourceLookupIndex sourceLookupIndex,
            INavigationDocumentService documentService,
            IAssemblyMetadataNavigationService metadataNavigationService,
            ISourcePreferredNavigationResolver sourcePreferredResolver,
            ISourceNavigationLineResolver lineResolver)
        {
            _sourceReferenceService = sourceReferenceService;
            _sourceLookupIndex = sourceLookupIndex;
            _documentService = documentService;
            _metadataNavigationService = metadataNavigationService;
            _sourcePreferredResolver = sourcePreferredResolver;
            _lineResolver = lineResolver;
        }

        public DecompilerResponse RequestSource(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache)
        {
            return CortexModuleUtil.RequestDecompilerSource(
                _sourceReferenceService,
                state,
                assemblyPath,
                metadataToken,
                entityKind,
                ignoreCache);
        }

        public bool OpenEntityTarget(CortexShellState state, string assemblyPath, int metadataToken, DecompilerEntityKind entityKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage)
        {
            if (entityKind == DecompilerEntityKind.Method)
            {
                return OpenMethodTarget(
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
                if (_metadataNavigationService.TryResolveTypeNavigationTarget(assemblyPath, metadataToken, out fullTypeName))
                {
                    string sourceDocumentPath;
                    if (_sourcePreferredResolver.TryResolveFromTypeName(state, _sourceLookupIndex, fullTypeName, out sourceDocumentPath))
                    {
                        var sourceLine = _lineResolver.ResolveLine(
                            SourceNavigationLineResolver.ReadAllTextSafe(sourceDocumentPath),
                            "NamedType",
                            SourceNavigationLineResolver.GetTypeLeafName(fullTypeName),
                            fullTypeName);
                        return _documentService.OpenDocument(
                            state,
                            sourceDocumentPath,
                            sourceLine,
                            successStatusMessage,
                            failureStatusMessage) != null;
                    }
                }
            }

            var response = RequestSource(state, assemblyPath, metadataToken, entityKind, ignoreCache);
            if (response == null)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            return _documentService.OpenDecompilerResult(state, response, 1, successStatusMessage, failureStatusMessage);
        }

        public bool OpenMethodTarget(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, string successStatusMessage, string failureStatusMessage)
        {
            MethodNavigationTarget methodTarget;
            if (!_metadataNavigationService.TryResolveMethodNavigationTarget(assemblyPath, methodMetadataToken, out methodTarget) || methodTarget == null)
            {
                if (!string.IsNullOrEmpty(failureStatusMessage))
                {
                    state.StatusMessage = failureStatusMessage;
                }

                return false;
            }

            var effectiveMetadataName = !string.IsNullOrEmpty(metadataName) ? metadataName : methodTarget.MethodName;
            var effectiveContainingTypeName = !string.IsNullOrEmpty(containingTypeName) ? containingTypeName : methodTarget.ContainingTypeName;
            var effectiveSymbolKind = !string.IsNullOrEmpty(symbolKind) ? symbolKind : methodTarget.SymbolKind;

            string sourceDocumentPath;
            if (_sourcePreferredResolver.TryResolveFromSymbol(
                state,
                _sourceLookupIndex,
                new Symbols.LanguageSymbolNavigationRequest
                {
                    ContainingTypeName = effectiveContainingTypeName,
                    MetadataName = effectiveMetadataName,
                    SymbolKind = effectiveSymbolKind
                },
                out sourceDocumentPath))
            {
                var sourceLine = _lineResolver.ResolveLine(
                    SourceNavigationLineResolver.ReadAllTextSafe(sourceDocumentPath),
                    effectiveSymbolKind,
                    effectiveMetadataName,
                    effectiveContainingTypeName);
                return _documentService.OpenDocument(
                    state,
                    sourceDocumentPath,
                    sourceLine,
                    successStatusMessage,
                    failureStatusMessage) != null;
            }

            int highlightedLine;
            var decompiled = RequestMethodView(
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

            return _documentService.OpenDecompilerResult(state, decompiled, highlightedLine, successStatusMessage, failureStatusMessage);
        }

        public DecompilerResponse RequestMethodView(CortexShellState state, string assemblyPath, int methodMetadataToken, string metadataName, string containingTypeName, string symbolKind, bool ignoreCache, out int highlightedLine)
        {
            highlightedLine = 1;
            if (state == null || string.IsNullOrEmpty(assemblyPath) || methodMetadataToken <= 0)
            {
                return null;
            }

            MethodNavigationTarget methodTarget;
            if (!_metadataNavigationService.TryResolveMethodNavigationTarget(assemblyPath, methodMetadataToken, out methodTarget) || methodTarget == null)
            {
                return null;
            }

            var decompiled = RequestSource(
                state,
                assemblyPath,
                methodTarget.DeclaringTypeMetadataToken,
                DecompilerEntityKind.Type,
                ignoreCache);
            if (decompiled == null)
            {
                return null;
            }

            var effectiveMetadataName = !string.IsNullOrEmpty(metadataName) ? metadataName : methodTarget.MethodName;
            var effectiveContainingTypeName = !string.IsNullOrEmpty(containingTypeName) ? containingTypeName : methodTarget.ContainingTypeName;
            var effectiveSymbolKind = !string.IsNullOrEmpty(symbolKind) ? symbolKind : methodTarget.SymbolKind;
            highlightedLine = _lineResolver.ResolveLine(
                !string.IsNullOrEmpty(decompiled.SourceText) ? decompiled.SourceText : SourceNavigationLineResolver.ReadAllTextSafe(decompiled.CachePath),
                effectiveSymbolKind,
                effectiveMetadataName,
                effectiveContainingTypeName);

            MMLog.WriteInfo("[Cortex.Navigation] Prepared decompiled method view via declaring type. CachePath=" + (decompiled.CachePath ?? string.Empty) +
                ", Line=" + highlightedLine +
                ", MethodToken=0x" + methodMetadataToken.ToString("X8") +
                ", DeclaringTypeToken=0x" + methodTarget.DeclaringTypeMetadataToken.ToString("X8") + ".");

            return decompiled;
        }
    }
}
