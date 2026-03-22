using System;
using Cortex.Core.Models;
namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private TextSearchQuery BuildActiveSearchQuery()
        {
            var search = _state.Search;
            return new TextSearchQuery
            {
                SearchText = search != null ? search.QueryText ?? string.Empty : string.Empty,
                Scope = search != null ? search.Query.Scope : SearchScopeKind.CurrentDocument,
                MatchCase = search != null && search.Query.MatchCase,
                WholeWord = search != null && search.Query.WholeWord
            };
        }

        private void OpenFind()
        {
            var search = _state.Search;
            if (search == null)
            {
                return;
            }

            // Mirror Visual Studio's toggle behavior: pressing Ctrl+F again closes
            // the active find UI instead of reopening or duplicating it.
            if (search.IsVisible)
            {
                MMLog.WriteInfo("[Cortex.Find] Ctrl+F invoked while find was already open. Closing overlay.");
                CloseFind();
                return;
            }

            search.IsVisible = true;
            search.FocusQueryRequested = true;
            search.WorkflowKind = TextSearchWorkflowKind.Find;
            search.WorkflowTargetText = string.Empty;
            search.WorkflowCaption = string.Empty;
            search.RenamePanelExpanded = false;
            if (string.IsNullOrEmpty(search.RenameReplacementText))
            {
                search.RenameReplacementText = search.QueryText ?? string.Empty;
            }
            if (string.IsNullOrEmpty(search.QueryText))
            {
                search.Query.Scope = SearchScopeKind.CurrentDocument;
                SeedSearchFromSelection();
            }

            MMLog.WriteInfo("[Cortex.Find] Opened find. SeedQuery='" + (search.QueryText ?? string.Empty) +
                "', Scope=" + search.Query.Scope + ".");
            _state.StatusMessage = "Find opened.";
        }

        private void CloseFind()
        {
            if (_state.Search == null)
            {
                return;
            }

            MMLog.WriteInfo("[Cortex.Find] Closed find.");
            _state.Search.IsVisible = false;
            _state.Search.ScopeMenuOpen = false;
            _state.Search.FocusQueryRequested = false;
            _state.Search.RenamePanelExpanded = false;
            _state.StatusMessage = "Find closed.";
        }

        private void SeedSearchFromSelection()
        {
            var active = _state.Documents.ActiveDocument;
            if (active == null || active.EditorState == null)
            {
                return;
            }

            var editorService = new Cortex.Core.Services.EditorService();
            var selection = editorService.GetPrimarySelection(active);
            if (selection == null || !selection.HasSelection)
            {
                return;
            }

            var text = active.Text ?? string.Empty;
            var start = Math.Max(0, Math.Min(selection.Start, text.Length));
            var end = Math.Max(start, Math.Min(selection.End, text.Length));
            if (end <= start)
            {
                return;
            }

            _state.Search.QueryText = text.Substring(start, end - start);
            MarkSearchDirty();
        }

        private void MarkSearchDirty()
        {
            if (_state.Search == null)
            {
                return;
            }

            _state.Search.PendingRefresh = true;
            _state.Search.ActiveMatchIndex = -1;
        }

        private void ExecuteSearchOrAdvance(int step)
        {
            if (_workbenchSearchService == null || _textSearchService == null || _state.Search == null)
            {
                return;
            }

            var query = BuildActiveSearchQuery();
            if (string.IsNullOrEmpty(query.SearchText))
            {
                MMLog.WriteInfo("[Cortex.Find] Search skipped because query text was empty. Scope=" + query.Scope + ".");
                _state.Search.Results = new TextSearchResultSet
                {
                    Query = query,
                    GeneratedUtc = DateTime.UtcNow,
                    StatusMessage = "Enter text to search."
                };
                _state.StatusMessage = _state.Search.Results.StatusMessage;
                return;
            }

            var fingerprint = _workbenchSearchService.BuildFingerprint(query);
            var queryChanged = _state.Search.PendingRefresh ||
                _state.Search.Results == null ||
                !string.Equals(_state.Search.LastExecutedFingerprint, fingerprint, StringComparison.Ordinal);
            var priorIndex = queryChanged ? -1 : _state.Search.ActiveMatchIndex;
            _state.Search.Results = _workbenchSearchService.Search(query, _state, _projectCatalog, _sourceLookupIndex, _textSearchService);
            _state.Search.LastExecutedFingerprint = fingerprint;
            _state.Search.PendingRefresh = false;
            _state.Search.ActiveMatchIndex = priorIndex;
            MMLog.WriteInfo("[Cortex.Find] Search executed. Query='" + (query.SearchText ?? string.Empty) +
                "', Scope=" + query.Scope +
                ", MatchCase=" + query.MatchCase +
                ", WholeWord=" + query.WholeWord +
                ", Results=" + (_state.Search.Results != null ? _state.Search.Results.TotalMatchCount : 0) +
                ", Step=" + step + ".");

            var totalMatches = _workbenchSearchService.CountMatches(_state.Search.Results);
            if (totalMatches <= 0)
            {
                ActivateContainer(CortexWorkbenchIds.SearchContainer);
                _state.StatusMessage = _state.Search.Results != null ? _state.Search.Results.StatusMessage : "No matches found.";
                return;
            }

            var nextIndex = _state.Search.ActiveMatchIndex;
            if (step >= 0)
            {
                nextIndex = (nextIndex + 1 + totalMatches) % totalMatches;
            }
            else
            {
                nextIndex = nextIndex < 0 ? totalMatches - 1 : (nextIndex - 1 + totalMatches) % totalMatches;
            }

            var match = _workbenchSearchService.GetMatchAt(_state.Search.Results, nextIndex);
            if (match == null)
            {
                MMLog.WriteWarning("[Cortex.Find] Search produced a null match at index " + nextIndex + ".");
                return;
            }

            _state.Search.ActiveMatchIndex = nextIndex;
            ActivateContainer(CortexWorkbenchIds.SearchContainer);
            MMLog.WriteInfo("[Cortex.Find] Navigating to match " + (nextIndex + 1) + "/" + totalMatches +
                " -> " + (match.DocumentPath ?? string.Empty) +
                ":" + match.LineNumber + ":" + match.ColumnNumber + ".");
            _workbenchSearchService.NavigateToMatch(match, _state, _navigationService);
        }
    }
}
