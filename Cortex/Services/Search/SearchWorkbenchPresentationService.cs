using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Navigation;

namespace Cortex.Services.Search
{
    internal sealed class SearchWorkbenchSummaryPresentation
    {
        public bool HasSemanticView;
        public string Title = string.Empty;
        public string Status = string.Empty;
        public string ScopeCaption = string.Empty;
    }

    internal sealed class SearchWorkbenchPresentationService
    {
        public SearchWorkbenchSummaryPresentation BuildSummary(CortexShellState state)
        {
            var presentation = new SearchWorkbenchSummaryPresentation();
            presentation.HasSemanticView = HasSemanticView(state);
            if (presentation.HasSemanticView)
            {
                presentation.Title = BuildSemanticTitle(state);
                presentation.Status = BuildSemanticStatus(state);
                return presentation;
            }

            presentation.Title = "Search";
            var results = state != null && state.Search != null ? state.Search.Results : null;
            presentation.Status = results != null
                ? results.StatusMessage ?? "Search ready."
                : "Search results will appear here.";
            presentation.ScopeCaption = BuildScopeCaption(state);
            return presentation;
        }

        public void RefreshResultsIfPending(
            WorkbenchSearchService searchService,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            ITextSearchService textSearchService,
            CortexShellState state)
        {
            var searchState = state != null ? state.Search : null;
            if (searchState == null || !searchState.PendingRefresh)
            {
                return;
            }

            var query = BuildQuery(searchState);
            if (string.IsNullOrEmpty(query.SearchText))
            {
                searchState.Results = new TextSearchResultSet
                {
                    Query = query,
                    GeneratedUtc = DateTime.UtcNow,
                    StatusMessage = "Enter text to search."
                };
                searchState.LastExecutedFingerprint = string.Empty;
                searchState.PendingRefresh = false;
                searchState.ActiveMatchIndex = -1;
                return;
            }

            searchState.Results = searchService != null
                ? searchService.Search(query, state, projectCatalog, sourceLookupIndex, textSearchService)
                : new TextSearchResultSet
                {
                    Query = query,
                    GeneratedUtc = DateTime.UtcNow,
                    StatusMessage = "Search service was not available."
                };
            searchState.LastExecutedFingerprint = searchService != null ? searchService.BuildFingerprint(query) : string.Empty;
            searchState.PendingRefresh = false;

            var totalMatches = searchState.Results != null ? searchState.Results.TotalMatchCount : 0;
            if (totalMatches <= 0)
            {
                searchState.ActiveMatchIndex = -1;
            }
            else if (searchState.ActiveMatchIndex < 0 || searchState.ActiveMatchIndex >= totalMatches)
            {
                searchState.ActiveMatchIndex = 0;
            }
        }

        public string BuildLocationButtonLabel(LanguageServiceSymbolLocation location)
        {
            return "Ln " + (location != null && location.Range != null ? location.Range.StartLine.ToString() : "0") +
                ", Col " + (location != null && location.Range != null ? location.Range.StartColumn.ToString() : "0") +
                "  " + (location != null ? location.PreviewText ?? location.LineText ?? string.Empty : string.Empty);
        }

        public void OpenLocation(ICortexNavigationService navigationService, CortexShellState state, string documentPath, LanguageServiceRange range)
        {
            if (navigationService == null || string.IsNullOrEmpty(documentPath))
            {
                return;
            }

            navigationService.OpenDocument(
                state,
                documentPath,
                range != null ? range.StartLine : 1,
                "Opened " + Path.GetFileName(documentPath) + ".",
                "Could not open " + Path.GetFileName(documentPath) + ".");
        }

        public void OpenDefinition(ICortexNavigationService navigationService, CortexShellState state, LanguageServiceDefinitionResponse response)
        {
            if (navigationService == null || state == null || response == null)
            {
                return;
            }

            navigationService.OpenLanguageSymbolTarget(
                state,
                response.SymbolDisplay,
                response.SymbolKind,
                response.MetadataName,
                response.ContainingTypeName,
                response.ContainingAssemblyName,
                response.DocumentationCommentId,
                response.DocumentPath,
                response.Range,
                "Opened definition.",
                "Could not open definition.");
        }

        private static bool HasSemanticView(CortexShellState state)
        {
            return state != null &&
                state.Semantic != null &&
                state.Semantic.Workbench != null &&
                state.Semantic.Workbench.ActiveView != SemanticWorkbenchViewKind.None;
        }

        private static TextSearchQuery BuildQuery(CortexSearchInteractionState searchState)
        {
            return new TextSearchQuery
            {
                SearchText = searchState != null ? searchState.QueryText ?? string.Empty : string.Empty,
                Scope = searchState != null ? searchState.Query.Scope : SearchScopeKind.CurrentDocument,
                MatchCase = searchState != null && searchState.Query.MatchCase,
                WholeWord = searchState != null && searchState.Query.WholeWord
            };
        }

        private static string BuildSemanticTitle(CortexShellState state)
        {
            switch (state.Semantic.Workbench.ActiveView)
            {
                case SemanticWorkbenchViewKind.RenamePreview:
                    return "Semantic Rename";
                case SemanticWorkbenchViewKind.References:
                    return "Semantic References";
                case SemanticWorkbenchViewKind.PeekDefinition:
                    return "Peek Definition";
                case SemanticWorkbenchViewKind.CallHierarchy:
                    return "Call Hierarchy";
                case SemanticWorkbenchViewKind.ValueSource:
                    return "Value Source";
                case SemanticWorkbenchViewKind.UnitTestGeneration:
                    return "Unit Test Generation";
                case SemanticWorkbenchViewKind.BaseSymbols:
                    return "Base Symbols";
                case SemanticWorkbenchViewKind.Implementations:
                    return "Implementations";
                case SemanticWorkbenchViewKind.DocumentEditPreview:
                    return "Document Edit Preview";
                default:
                    return "Semantic Results";
            }
        }

        private static string BuildSemanticStatus(CortexShellState state)
        {
            if (state == null || state.Semantic == null)
            {
                return string.Empty;
            }

            switch (state.Semantic.Workbench.ActiveView)
            {
                case SemanticWorkbenchViewKind.RenamePreview:
                    return state.Semantic.Workbench.RenamePreview != null ? state.Semantic.Workbench.RenamePreview.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.References:
                    return state.Semantic.Workbench.References != null ? state.Semantic.Workbench.References.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.PeekDefinition:
                    return state.Semantic.Workbench.PeekDefinition != null ? state.Semantic.Workbench.PeekDefinition.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.CallHierarchy:
                    return state.Semantic.Workbench.CallHierarchy != null ? state.Semantic.Workbench.CallHierarchy.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.ValueSource:
                    return state.Semantic.Workbench.ValueSource != null ? state.Semantic.Workbench.ValueSource.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.UnitTestGeneration:
                    return state.Semantic.Workbench.UnitTestGeneration != null ? state.Semantic.Workbench.UnitTestGeneration.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.BaseSymbols:
                    return state.Semantic.Workbench.BaseSymbols != null ? state.Semantic.Workbench.BaseSymbols.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.Implementations:
                    return state.Semantic.Workbench.Implementations != null ? state.Semantic.Workbench.Implementations.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.DocumentEditPreview:
                    return state.Semantic.Workbench.DocumentEditPreview != null ? state.Semantic.Workbench.DocumentEditPreview.StatusMessage ?? string.Empty : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private static string BuildScopeCaption(CortexShellState state)
        {
            var searchState = state != null ? state.Search : null;
            if (searchState == null)
            {
                return string.Empty;
            }

            return "Scope: " + BuildScopeLabel(searchState.Query.Scope);
        }

        public static string BuildScopeLabel(SearchScopeKind scope)
        {
            switch (scope)
            {
                case SearchScopeKind.AllOpenDocuments: return "All open documents";
                case SearchScopeKind.CurrentProject: return "Current project";
                case SearchScopeKind.EntireSolution: return "Entire solution";
                default: return "Current document";
            }
        }
    }
}
