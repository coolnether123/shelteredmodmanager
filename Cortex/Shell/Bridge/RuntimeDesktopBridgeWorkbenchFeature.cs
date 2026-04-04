using System;
using System.Linq;
using Cortex.Bridge;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Services.Editor.Presentation;
using Cortex.Services.Navigation;
using Cortex.Services.Search;
using Cortex.Shell.Shared.Models;

namespace Cortex.Shell.Bridge
{
    internal sealed class RuntimeDesktopBridgeWorkbenchFeature
    {
        private readonly CortexShellState _shellState;
        private readonly Func<IProjectCatalog> _projectCatalogAccessor;
        private readonly Func<ISourceLookupIndex> _sourceLookupIndexAccessor;
        private readonly Func<ITextSearchService> _textSearchServiceAccessor;
        private readonly Func<ICortexNavigationService> _navigationServiceAccessor;
        private readonly EditorPresentationService _editorPresentationService = new EditorPresentationService();
        private readonly SearchWorkbenchPresentationService _searchPresentationService = new SearchWorkbenchPresentationService();
        private readonly WorkbenchSearchService _searchService = new WorkbenchSearchService();
        private string _cachedWorkbenchToken = string.Empty;
        private string _cachedSearchContextToken = string.Empty;

        public RuntimeDesktopBridgeWorkbenchFeature(
            CortexShellState shellState,
            Func<IProjectCatalog> projectCatalogAccessor,
            Func<ISourceLookupIndex> sourceLookupIndexAccessor,
            Func<ITextSearchService> textSearchServiceAccessor,
            Func<ICortexNavigationService> navigationServiceAccessor)
        {
            _shellState = shellState ?? new CortexShellState();
            _projectCatalogAccessor = projectCatalogAccessor;
            _sourceLookupIndexAccessor = sourceLookupIndexAccessor;
            _textSearchServiceAccessor = textSearchServiceAccessor;
            _navigationServiceAccessor = navigationServiceAccessor;
        }

        public void Initialize()
        {
            EnsureSearchResultsCurrent();
            _cachedWorkbenchToken = BuildWorkbenchToken();
        }

        public bool SynchronizeFromRuntime()
        {
            EnsureSearchResultsCurrent();
            var currentToken = BuildWorkbenchToken();
            if (string.Equals(_cachedWorkbenchToken, currentToken, StringComparison.Ordinal))
            {
                return false;
            }

            _cachedWorkbenchToken = currentToken;
            return true;
        }

        public bool TryApplyIntent(BridgeIntentMessage intent, out string statusMessage)
        {
            statusMessage = string.Empty;
            if (intent == null)
            {
                return false;
            }

            switch (intent.IntentType)
            {
                case BridgeIntentType.UpdateSearch:
                    UpdateSearch(intent);
                    EnsureSearchResultsCurrent();
                    statusMessage = _shellState.Search != null && _shellState.Search.Results != null
                        ? _shellState.Search.Results.StatusMessage ?? "Updated search."
                        : "Updated search.";
                    _shellState.StatusMessage = statusMessage;
                    _cachedWorkbenchToken = BuildWorkbenchToken();
                    return true;
                case BridgeIntentType.OpenSearchResult:
                    statusMessage = OpenSearchResult(intent.ResultIndex);
                    _cachedWorkbenchToken = BuildWorkbenchToken();
                    return true;
                default:
                    return false;
            }
        }

        public EditorWorkbenchModel BuildEditorSnapshot()
        {
            var snapshot = new EditorWorkbenchModel();
            var active = _shellState.Documents != null ? _shellState.Documents.ActiveDocument : null;
            if (active == null)
            {
                return snapshot;
            }

            var codeArea = _editorPresentationService.BuildCodeAreaPresentation(_shellState);
            var pathBar = _editorPresentationService.BuildPathBarPresentation(null, _shellState);
            var statusBar = _editorPresentationService.BuildStatusBarPresentation(_shellState);
            var tabs = _editorPresentationService.BuildTabStripPresentation(_shellState);

            snapshot.ActiveDocumentPath = active.FilePath ?? string.Empty;
            snapshot.ActiveDocumentDisplayName = CortexModuleUtil.GetDocumentDisplayName(active);
            snapshot.CompactPath = pathBar.CompactPath ?? string.Empty;
            snapshot.UsesUnifiedSourceSurface = codeArea.UsesUnifiedSourceSurface;
            snapshot.IsEditingEnabled = codeArea.IsEditingEnabled;
            snapshot.IsDirty = statusBar.IsDirty;
            snapshot.AllowSaving = pathBar.AllowSaving;
            snapshot.CaretLine = statusBar.Line;
            snapshot.CaretColumn = statusBar.Column;
            snapshot.LineCount = statusBar.LineCount;
            snapshot.HighlightedLine = pathBar.HighlightedLine;
            snapshot.HasHighlightedLine = pathBar.HasHighlightedLine;
            snapshot.LanguageStatusLabel = statusBar.LanguageStatusLabel ?? string.Empty;
            snapshot.CompletionStatusLabel = statusBar.CompletionStatusLabel ?? string.Empty;

            foreach (var tab in tabs)
            {
                snapshot.OpenDocuments.Add(new EditorDocumentSummaryModel
                {
                    FilePath = tab != null && tab.Session != null ? tab.Session.FilePath ?? string.Empty : string.Empty,
                    DisplayName = tab != null ? tab.DisplayName ?? string.Empty : string.Empty,
                    IsActive = tab != null && tab.IsActive,
                    IsDirty = tab != null && tab.IsDirty
                });
            }

            return snapshot;
        }

        public SearchWorkbenchModel BuildSearchSnapshot()
        {
            EnsureSearchResultsCurrent();

            var snapshot = new SearchWorkbenchModel();
            var searchState = _shellState.Search;
            if (searchState == null)
            {
                return snapshot;
            }

            var summary = _searchPresentationService.BuildSummary(_shellState);
            snapshot.HasSemanticView = summary.HasSemanticView;
            snapshot.Title = summary.Title ?? string.Empty;
            snapshot.StatusMessage = summary.Status ?? string.Empty;
            snapshot.ScopeCaption = summary.ScopeCaption ?? string.Empty;
            snapshot.ActiveMatchIndex = searchState.ActiveMatchIndex;
            snapshot.Query = new SearchQueryModel
            {
                SearchText = searchState.QueryText ?? string.Empty,
                Scope = ConvertScope(searchState.Query != null ? searchState.Query.Scope : SearchScopeKind.CurrentDocument),
                MatchCase = searchState.Query != null && searchState.Query.MatchCase,
                WholeWord = searchState.Query != null && searchState.Query.WholeWord
            };

            var results = searchState.Results;
            snapshot.TotalMatchCount = results != null ? results.TotalMatchCount : 0;
            if (results == null)
            {
                return snapshot;
            }

            var flatIndex = 0;
            foreach (var document in results.Documents)
            {
                if (document == null)
                {
                    continue;
                }

                var documentModel = new SearchDocumentResultModel
                {
                    DocumentPath = document.DocumentPath ?? string.Empty,
                    DisplayPath = document.DisplayPath ?? document.DocumentPath ?? string.Empty
                };

                foreach (var match in document.Matches)
                {
                    if (match == null)
                    {
                        continue;
                    }

                    documentModel.Matches.Add(new SearchMatchModel
                    {
                        ResultIndex = flatIndex,
                        DocumentPath = match.DocumentPath ?? string.Empty,
                        DisplayPath = match.DisplayPath ?? match.DocumentPath ?? string.Empty,
                        LineNumber = match.LineNumber,
                        ColumnNumber = match.ColumnNumber,
                        LineText = match.LineText ?? string.Empty,
                        PreviewText = match.PreviewText ?? string.Empty,
                        IsActive = flatIndex == searchState.ActiveMatchIndex
                    });
                    flatIndex++;
                }

                snapshot.Documents.Add(documentModel);
            }

            return snapshot;
        }

        public ReferenceWorkbenchModel BuildReferenceSnapshot()
        {
            var snapshot = new ReferenceWorkbenchModel();
            var result = _shellState.LastReferenceResult;
            if (result == null)
            {
                return snapshot;
            }

            snapshot.HasDecompilerResult = true;
            snapshot.FromCache = result.FromCache;
            snapshot.StatusMessage = result.StatusMessage ?? string.Empty;
            snapshot.ResolvedTargetDisplayName = result.ResolvedMemberDisplayName ?? string.Empty;
            snapshot.CachePath = result.CachePath ?? string.Empty;
            snapshot.XmlDocumentationPath = result.XmlDocumentationPath ?? string.Empty;
            snapshot.XmlDocumentationText = result.XmlDocumentationText ?? string.Empty;
            snapshot.SourceText = result.SourceText ?? string.Empty;
            return snapshot;
        }

        private void UpdateSearch(BridgeIntentMessage intent)
        {
            var searchState = _shellState.Search;
            if (searchState == null)
            {
                return;
            }

            searchState.IsVisible = true;
            searchState.ScopeMenuOpen = false;
            searchState.FocusQueryRequested = false;
            searchState.QueryText = intent.SearchQuery ?? string.Empty;
            searchState.Query.SearchText = searchState.QueryText;
            searchState.Query.Scope = ConvertScope(intent.SearchScope);
            searchState.Query.MatchCase = intent.MatchCase;
            searchState.Query.WholeWord = intent.WholeWord;
            searchState.PendingRefresh = true;
            searchState.ActiveMatchIndex = -1;
        }

        private string OpenSearchResult(int resultIndex)
        {
            EnsureSearchResultsCurrent();

            var searchState = _shellState.Search;
            var results = searchState != null ? searchState.Results : null;
            var match = _searchService.GetMatchAt(results, resultIndex);
            if (match == null)
            {
                _shellState.StatusMessage = "Search result could not be resolved.";
                return _shellState.StatusMessage;
            }

            searchState.ActiveMatchIndex = resultIndex;
            var navigationService = _navigationServiceAccessor != null ? _navigationServiceAccessor() : null;
            if (!_searchService.NavigateToMatch(match, _shellState, navigationService))
            {
                _shellState.StatusMessage = "Search result could not be opened.";
            }

            return _shellState.StatusMessage ?? "Opened search result.";
        }

        private void EnsureSearchResultsCurrent()
        {
            var searchState = _shellState.Search;
            if (searchState == null)
            {
                return;
            }

            var contextToken = BuildSearchContextToken();
            if (!string.Equals(_cachedSearchContextToken, contextToken, StringComparison.Ordinal))
            {
                searchState.PendingRefresh = true;
            }

            _searchPresentationService.RefreshResultsIfPending(
                _searchService,
                _projectCatalogAccessor != null ? _projectCatalogAccessor() : null,
                _sourceLookupIndexAccessor != null ? _sourceLookupIndexAccessor() : null,
                _textSearchServiceAccessor != null ? _textSearchServiceAccessor() : null,
                _shellState);

            _cachedSearchContextToken = BuildSearchContextToken();
        }

        private string BuildWorkbenchToken()
        {
            var documents = _shellState.Documents;
            var active = documents != null ? documents.ActiveDocument : null;
            var openDocumentsToken = documents != null
                ? string.Join(
                    ";",
                    documents.OpenDocuments.Select(document =>
                        (document != null ? document.FilePath ?? string.Empty : string.Empty) + ":" +
                        (document != null ? document.TextVersion.ToString() : "0") + ":" +
                        (document != null && document.IsDirty)).ToArray())
                : string.Empty;
            var searchState = _shellState.Search;
            var searchResults = searchState != null ? searchState.Results : null;
            var reference = _shellState.LastReferenceResult;

            return string.Join(
                "|",
                new[]
                {
                    active != null ? active.FilePath ?? string.Empty : string.Empty,
                    active != null ? active.TextVersion.ToString() : "0",
                    openDocumentsToken,
                    BuildSearchContextToken(),
                    searchState != null ? searchState.QueryText ?? string.Empty : string.Empty,
                    searchState != null ? searchState.Query.Scope.ToString() : string.Empty,
                    searchState != null ? searchState.Query.MatchCase.ToString() : bool.FalseString,
                    searchState != null ? searchState.Query.WholeWord.ToString() : bool.FalseString,
                    searchState != null ? searchState.ActiveMatchIndex.ToString() : "-1",
                    searchResults != null ? searchResults.TotalMatchCount.ToString() : "0",
                    searchResults != null ? searchResults.StatusMessage ?? string.Empty : string.Empty,
                    searchResults != null ? searchResults.GeneratedUtc.ToString("o") : string.Empty,
                    reference != null ? reference.CachePath ?? string.Empty : string.Empty,
                    reference != null ? reference.ResolvedMemberDisplayName ?? string.Empty : string.Empty,
                    reference != null ? reference.StatusMessage ?? string.Empty : string.Empty,
                    reference != null ? reference.FromCache.ToString() : bool.FalseString
                });
        }

        private string BuildSearchContextToken()
        {
            var searchState = _shellState.Search;
            var scope = searchState != null ? searchState.Query.Scope : SearchScopeKind.CurrentDocument;
            var documents = _shellState.Documents;

            switch (scope)
            {
                case SearchScopeKind.CurrentDocument:
                    var active = documents != null ? documents.ActiveDocument : null;
                    return "doc|" +
                        (active != null ? active.FilePath ?? string.Empty : string.Empty) + "|" +
                        (active != null ? active.TextVersion.ToString() : "0");
                case SearchScopeKind.AllOpenDocuments:
                    return "open|" + (documents != null
                        ? string.Join(
                            ";",
                            documents.OpenDocuments.Select(document =>
                                (document != null ? document.FilePath ?? string.Empty : string.Empty) + ":" +
                                (document != null ? document.TextVersion.ToString() : "0")).ToArray())
                        : string.Empty);
                case SearchScopeKind.CurrentProject:
                    return "project|" +
                        (_shellState.SelectedProject != null ? _shellState.SelectedProject.ModId ?? string.Empty : string.Empty) + "|" +
                        (_shellState.SelectedProject != null ? _shellState.SelectedProject.SourceRootPath ?? string.Empty : string.Empty);
                default:
                    return "workspace|" + (_shellState.Settings != null ? _shellState.Settings.WorkspaceRootPath ?? string.Empty : string.Empty);
            }
        }

        private static WorkbenchSearchScope ConvertScope(SearchScopeKind scope)
        {
            switch (scope)
            {
                case SearchScopeKind.AllOpenDocuments: return WorkbenchSearchScope.AllOpenDocuments;
                case SearchScopeKind.CurrentProject: return WorkbenchSearchScope.CurrentProject;
                case SearchScopeKind.EntireSolution: return WorkbenchSearchScope.Workspace;
                default: return WorkbenchSearchScope.CurrentDocument;
            }
        }

        private static SearchScopeKind ConvertScope(WorkbenchSearchScope scope)
        {
            switch (scope)
            {
                case WorkbenchSearchScope.AllOpenDocuments: return SearchScopeKind.AllOpenDocuments;
                case WorkbenchSearchScope.CurrentProject: return SearchScopeKind.CurrentProject;
                case WorkbenchSearchScope.Workspace: return SearchScopeKind.EntireSolution;
                default: return SearchScopeKind.CurrentDocument;
            }
        }
    }
}
