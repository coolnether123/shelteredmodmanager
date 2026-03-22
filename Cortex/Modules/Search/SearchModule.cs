using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.Search
{
    public sealed class SearchModule
    {
        private Vector2 _scroll = Vector2.zero;
        private GUIStyle _summaryStyle;
        private GUIStyle _summaryCaptionStyle;
        private GUIStyle _documentStyle;
        private GUIStyle _documentMetaStyle;
        private GUIStyle _resultButtonStyle;
        private GUIStyle _resultButtonActiveStyle;
        private GUIStyle _emptyStateStyle;
        private GUIStyle _actionButtonStyle;
        private Texture2D _summaryBackground;
        private Texture2D _activeBackground;
        private string _appliedTheme = string.Empty;

        public void Draw(
            WorkbenchSearchService searchService,
            CortexNavigationService navigationService,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            ITextSearchService textSearchService,
            CortexShellState state)
        {
            EnsureStyles();
            EnsureResults(searchService, projectCatalog, sourceLookupIndex, textSearchService, state);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            DrawSummary(state);

            if (IsRenameWorkflow(state))
            {
                GUILayout.Space(6f);
                DrawRenamePanel(searchService, documentService, state);
            }

            GUILayout.Space(6f);
            _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
            DrawResults(searchService, navigationService, state);
            GUILayout.EndScrollView();
            GUILayout.EndVertical();
        }

        private void DrawSummary(CortexShellState state)
        {
            GUILayout.BeginVertical(_summaryStyle ?? GUI.skin.box);
            GUILayout.Label(BuildWorkflowTitle(state), _documentStyle ?? GUI.skin.label);

            var results = state != null && state.Search != null ? state.Search.Results : null;
            GUILayout.Label(results != null ? results.StatusMessage ?? "Search ready." : "Search results will appear here.", _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.Label(BuildScopeCaption(state), _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.EndVertical();
        }

        private void DrawRenamePanel(WorkbenchSearchService searchService, IDocumentService documentService, CortexShellState state)
        {
            var searchState = state != null ? state.Search : null;
            if (searchState == null)
            {
                return;
            }

            GUILayout.BeginVertical(GUI.skin.box);
            var toggleLabel = searchState.RenamePanelExpanded ? "Rename All v" : "Rename All >";
            if (GUILayout.Button(toggleLabel, _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(120f)))
            {
                searchState.RenamePanelExpanded = !searchState.RenamePanelExpanded;
            }

            if (searchState.RenamePanelExpanded)
            {
                GUILayout.Space(4f);
                GUILayout.Label(
                    "Review the current matches, then replace every occurrence of '" + (searchState.WorkflowTargetText ?? string.Empty) +
                    "' inside the active search scope.",
                    _summaryCaptionStyle ?? GUI.skin.label);

                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("New Name", GUILayout.Width(72f));
                searchState.RenameReplacementText = GUILayout.TextField(searchState.RenameReplacementText ?? string.Empty, GUILayout.ExpandWidth(true));
                GUILayout.EndHorizontal();

                GUILayout.Space(4f);
                GUILayout.BeginHorizontal();
                GUILayout.Label("Scope: " + BuildScopeLabel(searchState.Query.Scope), _documentMetaStyle ?? GUI.skin.label);
                GUILayout.FlexibleSpace();

                var previousEnabled = GUI.enabled;
                GUI.enabled = CanApplyRename(searchState);
                if (GUILayout.Button("Rename All", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(96f)))
                {
                    ApplyRename(searchService, documentService, state);
                }

                GUI.enabled = previousEnabled;
                if (GUILayout.Button("Collapse", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(84f)))
                {
                    searchState.RenamePanelExpanded = false;
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.EndVertical();
        }

        private void DrawResults(WorkbenchSearchService searchService, CortexNavigationService navigationService, CortexShellState state)
        {
            var searchState = state != null ? state.Search : null;
            var results = searchState != null ? searchState.Results : null;
            if (searchService == null || searchState == null || results == null || results.TotalMatchCount <= 0)
            {
                GUILayout.Label("Run a search to populate results.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            var flatIndex = 0;
            for (var i = 0; i < results.Documents.Count; i++)
            {
                var document = results.Documents[i];
                if (document == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(Path.GetFileName(document.DocumentPath) + "  (" + document.Matches.Count + ")", _documentStyle ?? GUI.skin.label);
                GUILayout.Label(document.DocumentPath ?? string.Empty, _documentMetaStyle ?? GUI.skin.label);
                GUILayout.Space(2f);

                for (var j = 0; j < document.Matches.Count; j++)
                {
                    var match = document.Matches[j];
                    var isActive = flatIndex == searchState.ActiveMatchIndex;
                    if (GUILayout.Button(
                        "Ln " + match.LineNumber + ", Col " + match.ColumnNumber + "  " + (match.PreviewText ?? match.LineText ?? string.Empty),
                        isActive ? (_resultButtonActiveStyle ?? GUI.skin.button) : (_resultButtonStyle ?? GUI.skin.button),
                        GUILayout.Height(24f)))
                    {
                        searchState.ActiveMatchIndex = flatIndex;
                        searchService.NavigateToMatch(match, state, navigationService);
                    }

                    flatIndex++;
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void EnsureResults(
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

        private void ApplyRename(WorkbenchSearchService searchService, IDocumentService documentService, CortexShellState state)
        {
            if (searchService == null || state == null || state.Search == null)
            {
                return;
            }

            var searchState = state.Search;
            var replacement = searchState.RenameReplacementText ?? string.Empty;
            var replacementResult = searchService.ApplyReplacement(searchState.Results, replacement, state, documentService);
            state.StatusMessage = replacementResult != null ? replacementResult.StatusMessage : "Rename did not complete.";
            if (replacementResult == null || replacementResult.UpdatedMatchCount <= 0)
            {
                return;
            }

            searchState.QueryText = replacement;
            searchState.WorkflowTargetText = replacement;
            searchState.WorkflowCaption = "Rename preview refreshed.";
            searchState.PendingRefresh = true;
            searchState.ActiveMatchIndex = -1;
        }

        private static bool CanApplyRename(CortexSearchInteractionState searchState)
        {
            return searchState != null &&
                searchState.Results != null &&
                searchState.Results.TotalMatchCount > 0 &&
                !string.IsNullOrEmpty(searchState.RenameReplacementText) &&
                !string.Equals(searchState.WorkflowTargetText ?? string.Empty, searchState.RenameReplacementText ?? string.Empty, StringComparison.Ordinal);
        }

        private static bool IsRenameWorkflow(CortexShellState state)
        {
            return state != null &&
                state.Search != null &&
                state.Search.WorkflowKind == TextSearchWorkflowKind.Rename;
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

        private string BuildWorkflowTitle(CortexShellState state)
        {
            var searchState = state != null ? state.Search : null;
            if (searchState == null)
            {
                return "Search";
            }

            switch (searchState.WorkflowKind)
            {
                case TextSearchWorkflowKind.References:
                    return "Find All References: " + (searchState.WorkflowTargetText ?? string.Empty);
                case TextSearchWorkflowKind.Rename:
                    return "Rename: " + (searchState.WorkflowTargetText ?? string.Empty);
                case TextSearchWorkflowKind.CallHierarchy:
                    return "Call Hierarchy Seed: " + (searchState.WorkflowTargetText ?? string.Empty);
                case TextSearchWorkflowKind.ValueSource:
                    return "Track Value Source: " + (searchState.WorkflowTargetText ?? string.Empty);
                case TextSearchWorkflowKind.UnitTests:
                    return "Create Unit Tests Seed: " + (searchState.WorkflowTargetText ?? string.Empty);
                default:
                    return "Search";
            }
        }

        private string BuildScopeCaption(CortexShellState state)
        {
            var searchState = state != null ? state.Search : null;
            if (searchState == null)
            {
                return string.Empty;
            }

            var caption = "Scope: " + BuildScopeLabel(searchState.Query.Scope);
            if (!string.IsNullOrEmpty(searchState.WorkflowCaption))
            {
                caption += "  |  " + searchState.WorkflowCaption;
            }

            return caption;
        }

        private static string BuildScopeLabel(SearchScopeKind scope)
        {
            switch (scope)
            {
                case SearchScopeKind.AllOpenDocuments: return "All open documents";
                case SearchScopeKind.CurrentProject: return "Current project";
                case SearchScopeKind.EntireSolution: return "Entire solution";
                default: return "Current document";
            }
        }

        private void EnsureStyles()
        {
            var themeKey = CortexIdeLayout.GetBackgroundColor().ToString() + "|" + CortexIdeLayout.GetAccentColor().ToString();
            if (string.Equals(_appliedTheme, themeKey) &&
                _summaryStyle != null &&
                _documentStyle != null &&
                _resultButtonStyle != null &&
                _resultButtonActiveStyle != null &&
                _actionButtonStyle != null)
            {
                return;
            }

            _appliedTheme = themeKey;
            _summaryBackground = MakeFill(CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.45f));
            _activeBackground = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.20f));

            _summaryStyle = new GUIStyle(GUI.skin.box);
            _summaryStyle.padding = new RectOffset(10, 10, 8, 8);
            GuiStyleUtil.ApplyBackgroundToAllStates(_summaryStyle, _summaryBackground);

            _summaryCaptionStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_summaryCaptionStyle, CortexIdeLayout.GetTextColor());
            _summaryCaptionStyle.wordWrap = true;

            _documentStyle = new GUIStyle(GUI.skin.label);
            _documentStyle.fontStyle = FontStyle.Bold;
            GuiStyleUtil.ApplyTextColorToAllStates(_documentStyle, CortexIdeLayout.GetTextColor());

            _documentMetaStyle = new GUIStyle(GUI.skin.label);
            _documentMetaStyle.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_documentMetaStyle, CortexIdeLayout.GetMutedTextColor());

            _resultButtonStyle = new GUIStyle(GUI.skin.button);
            _resultButtonStyle.alignment = TextAnchor.MiddleLeft;
            _resultButtonStyle.wordWrap = false;

            _resultButtonActiveStyle = new GUIStyle(_resultButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_resultButtonActiveStyle, _activeBackground);

            _emptyStateStyle = new GUIStyle(GUI.skin.label);
            _emptyStateStyle.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_emptyStateStyle, CortexIdeLayout.GetMutedTextColor());

            _actionButtonStyle = new GUIStyle(GUI.skin.button);
            _actionButtonStyle.alignment = TextAnchor.MiddleCenter;
            GuiStyleUtil.ApplyTextColorToAllStates(_actionButtonStyle, CortexIdeLayout.GetTextColor());
        }

        private static Texture2D MakeFill(Color color)
        {
            var texture = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            texture.hideFlags = HideFlags.HideAndDontSave;
            texture.SetPixel(0, 0, color);
            texture.Apply(false, true);
            return texture;
        }
    }
}
