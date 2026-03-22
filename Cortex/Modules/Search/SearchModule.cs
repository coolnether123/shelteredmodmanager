using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
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
        private readonly SemanticWorkspaceEditService _workspaceEditService = new SemanticWorkspaceEditService();

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
            if (HasSemanticView(state))
            {
                DrawSemanticSummary(state);
                GUILayout.Space(6f);
                _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
                DrawSemanticContent(navigationService, documentService, state);
                GUILayout.EndScrollView();
            }
            else
            {
                DrawSearchSummary(state);
                GUILayout.Space(6f);
                _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
                DrawSearchResults(searchService, navigationService, state);
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private static bool HasSemanticView(CortexShellState state)
        {
            return state != null &&
                state.Semantic != null &&
                state.Semantic.ActiveView != SemanticWorkbenchViewKind.None;
        }

        private void DrawSemanticSummary(CortexShellState state)
        {
            GUILayout.BeginVertical(_summaryStyle ?? GUI.skin.box);
            GUILayout.Label(BuildSemanticTitle(state), _documentStyle ?? GUI.skin.label);
            GUILayout.Label(BuildSemanticStatus(state), _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.EndVertical();
        }

        private void DrawSemanticContent(CortexNavigationService navigationService, IDocumentService documentService, CortexShellState state)
        {
            switch (state.Semantic.ActiveView)
            {
                case SemanticWorkbenchViewKind.RenamePreview:
                    DrawRenamePreview(navigationService, documentService, state);
                    return;
                case SemanticWorkbenchViewKind.References:
                    DrawLocationList(state.Semantic.References != null ? state.Semantic.References.Locations : null, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.PeekDefinition:
                    DrawPeekDefinition(state.Semantic.PeekDefinition, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.CallHierarchy:
                    DrawCallHierarchy(state.Semantic.CallHierarchy, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.ValueSource:
                    DrawValueSource(state.Semantic.ValueSource, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.UnitTestGeneration:
                    DrawUnitTestGeneration(state.Semantic.UnitTestGeneration, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.BaseSymbols:
                    DrawLocationList(state.Semantic.BaseSymbols != null ? state.Semantic.BaseSymbols.Locations : null, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.Implementations:
                    DrawLocationList(state.Semantic.Implementations != null ? state.Semantic.Implementations.Locations : null, navigationService, state);
                    return;
            }

            GUILayout.Label("No semantic results are active.", _emptyStateStyle ?? GUI.skin.label);
        }

        private void DrawRenamePreview(CortexNavigationService navigationService, IDocumentService documentService, CortexShellState state)
        {
            var preview = state != null && state.Semantic != null ? state.Semantic.RenamePreview : null;
            if (preview == null)
            {
                GUILayout.Label("Rename preview is not available.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            GUILayout.BeginHorizontal();
            var previousEnabled = GUI.enabled;
            GUI.enabled = preview.Success && preview.Documents != null && preview.Documents.Length > 0;
            if (GUILayout.Button("Apply Rename", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(120f)))
            {
                string statusMessage;
                _workspaceEditService.ApplyRenamePreview(state, documentService, preview, out statusMessage);
                state.StatusMessage = statusMessage;
            }

            GUI.enabled = previousEnabled;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            if (preview.Documents == null || preview.Documents.Length == 0)
            {
                GUILayout.Label(preview.StatusMessage ?? "No semantic rename edits were produced.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            for (var i = 0; i < preview.Documents.Length; i++)
            {
                var document = preview.Documents[i];
                if (document == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(Path.GetFileName(document.DocumentPath) + "  (" + document.ChangeCount + " edit(s))", _documentStyle ?? GUI.skin.label);
                GUILayout.Label(document.DocumentPath ?? string.Empty, _documentMetaStyle ?? GUI.skin.label);
                for (var j = 0; j < document.Edits.Length; j++)
                {
                    var edit = document.Edits[j];
                    if (edit == null)
                    {
                        continue;
                    }

                    if (GUILayout.Button(edit.PreviewText ?? edit.NewText ?? string.Empty, _resultButtonStyle ?? GUI.skin.button))
                    {
                        OpenDocumentAtRange(navigationService, state, document.DocumentPath, edit.Range);
                    }
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawPeekDefinition(LanguageServiceDefinitionResponse peekDefinition, CortexNavigationService navigationService, CortexShellState state)
        {
            if (peekDefinition == null)
            {
                GUILayout.Label("Peek definition is not available.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            if (GUILayout.Button("Open Definition", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(120f)))
            {
                OpenDefinitionTarget(navigationService, state, peekDefinition);
            }

            GUILayout.Space(6f);
            GUILayout.TextArea(peekDefinition.PreviewText ?? string.Empty, GUILayout.ExpandHeight(true));
        }

        private void DrawCallHierarchy(LanguageServiceCallHierarchyResponse response, CortexNavigationService navigationService, CortexShellState state)
        {
            if (response == null)
            {
                GUILayout.Label("Call hierarchy is not available.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            DrawCallHierarchySection("Incoming Calls", response.IncomingCalls, navigationService, state);
            GUILayout.Space(6f);
            DrawCallHierarchySection("Outgoing Calls", response.OutgoingCalls, navigationService, state);
        }

        private void DrawCallHierarchySection(string title, LanguageServiceCallHierarchyItem[] items, CortexNavigationService navigationService, CortexShellState state)
        {
            GUILayout.Label(title, _documentStyle ?? GUI.skin.label);
            if (items == null || items.Length == 0)
            {
                GUILayout.Label("No entries.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(item.SymbolDisplay ?? string.Empty, _documentStyle ?? GUI.skin.label);
                GUILayout.Label(item.Relationship + "  (" + item.CallCount + ")", _documentMetaStyle ?? GUI.skin.label);
                DrawLocationList(item.Locations, navigationService, state);
                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawValueSource(LanguageServiceValueSourceResponse response, CortexNavigationService navigationService, CortexShellState state)
        {
            if (response == null || response.Items == null || response.Items.Length == 0)
            {
                GUILayout.Label(response != null ? response.StatusMessage ?? "No value-source entries were found." : "No value-source entries were found.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            for (var i = 0; i < response.Items.Length; i++)
            {
                var item = response.Items[i];
                if (item == null || item.Location == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(item.FlowKind + ": " + (item.SymbolDisplay ?? string.Empty), _documentStyle ?? GUI.skin.label);
                GUILayout.Label(item.Relationship ?? string.Empty, _documentMetaStyle ?? GUI.skin.label);
                if (GUILayout.Button(BuildLocationButtonLabel(item.Location), _resultButtonStyle ?? GUI.skin.button, GUILayout.Height(24f)))
                {
                    OpenDocumentAtRange(navigationService, state, item.Location.DocumentPath, item.Location.Range);
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawUnitTestGeneration(UnitTestGenerationPlan plan, CortexNavigationService navigationService, CortexShellState state)
        {
            if (plan == null)
            {
                GUILayout.Label("Unit test generation is not available.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            GUILayout.Label(plan.StatusMessage ?? string.Empty, _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.Label(plan.OutputFilePath ?? string.Empty, _documentMetaStyle ?? GUI.skin.label);
            GUILayout.Space(6f);

            var previousEnabled = GUI.enabled;
            GUI.enabled = plan.CanApply;
            if (GUILayout.Button("Create Scaffold", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(120f)))
            {
                string statusMessage;
                if (_workspaceEditService.ApplyUnitTestPlan(plan, out statusMessage))
                {
                    state.StatusMessage = statusMessage;
                    if (navigationService != null)
                    {
                        navigationService.OpenDocument(state, plan.OutputFilePath, 1, statusMessage, "Could not open generated unit test scaffold.");
                    }
                }
                else
                {
                    state.StatusMessage = statusMessage;
                }
            }

            GUI.enabled = previousEnabled;
            GUILayout.Space(6f);
            GUILayout.TextArea(plan.GeneratedText ?? string.Empty, GUILayout.ExpandHeight(true));
        }

        private void DrawLocationList(LanguageServiceSymbolLocation[] locations, CortexNavigationService navigationService, CortexShellState state)
        {
            if (locations == null || locations.Length == 0)
            {
                GUILayout.Label("No semantic locations were returned.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            for (var i = 0; i < locations.Length; i++)
            {
                var location = locations[i];
                if (location == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(Path.GetFileName(location.DocumentPath) + "  " + (location.Relationship ?? string.Empty), _documentStyle ?? GUI.skin.label);
                GUILayout.Label(location.DocumentPath ?? string.Empty, _documentMetaStyle ?? GUI.skin.label);
                if (GUILayout.Button(BuildLocationButtonLabel(location), _resultButtonStyle ?? GUI.skin.button, GUILayout.Height(24f)))
                {
                    OpenDocumentAtRange(navigationService, state, location.DocumentPath, location.Range);
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawSearchSummary(CortexShellState state)
        {
            GUILayout.BeginVertical(_summaryStyle ?? GUI.skin.box);
            GUILayout.Label("Search", _documentStyle ?? GUI.skin.label);

            var results = state != null && state.Search != null ? state.Search.Results : null;
            GUILayout.Label(results != null ? results.StatusMessage ?? "Search ready." : "Search results will appear here.", _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.Label(BuildScopeCaption(state), _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.EndVertical();
        }

        private void DrawSearchResults(WorkbenchSearchService searchService, CortexNavigationService navigationService, CortexShellState state)
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

        private string BuildSemanticTitle(CortexShellState state)
        {
            switch (state.Semantic.ActiveView)
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
                default:
                    return "Semantic Results";
            }
        }

        private string BuildSemanticStatus(CortexShellState state)
        {
            if (state == null || state.Semantic == null)
            {
                return string.Empty;
            }

            switch (state.Semantic.ActiveView)
            {
                case SemanticWorkbenchViewKind.RenamePreview:
                    return state.Semantic.RenamePreview != null ? state.Semantic.RenamePreview.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.References:
                    return state.Semantic.References != null ? state.Semantic.References.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.PeekDefinition:
                    return state.Semantic.PeekDefinition != null ? state.Semantic.PeekDefinition.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.CallHierarchy:
                    return state.Semantic.CallHierarchy != null ? state.Semantic.CallHierarchy.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.ValueSource:
                    return state.Semantic.ValueSource != null ? state.Semantic.ValueSource.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.UnitTestGeneration:
                    return state.Semantic.UnitTestGeneration != null ? state.Semantic.UnitTestGeneration.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.BaseSymbols:
                    return state.Semantic.BaseSymbols != null ? state.Semantic.BaseSymbols.StatusMessage ?? string.Empty : string.Empty;
                case SemanticWorkbenchViewKind.Implementations:
                    return state.Semantic.Implementations != null ? state.Semantic.Implementations.StatusMessage ?? string.Empty : string.Empty;
                default:
                    return string.Empty;
            }
        }

        private string BuildScopeCaption(CortexShellState state)
        {
            var searchState = state != null ? state.Search : null;
            if (searchState == null)
            {
                return string.Empty;
            }

            return "Scope: " + BuildScopeLabel(searchState.Query.Scope);
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

        private static string BuildLocationButtonLabel(LanguageServiceSymbolLocation location)
        {
            return "Ln " + (location != null && location.Range != null ? location.Range.StartLine.ToString() : "0") +
                ", Col " + (location != null && location.Range != null ? location.Range.StartColumn.ToString() : "0") +
                "  " + (location != null ? location.PreviewText ?? location.LineText ?? string.Empty : string.Empty);
        }

        private static void OpenDocumentAtRange(CortexNavigationService navigationService, CortexShellState state, string documentPath, LanguageServiceRange range)
        {
            if (navigationService == null || string.IsNullOrEmpty(documentPath))
            {
                return;
            }

            navigationService.OpenDocument(state, documentPath, range != null ? range.StartLine : 1, "Opened " + Path.GetFileName(documentPath) + ".", "Could not open " + Path.GetFileName(documentPath) + ".");
        }

        private static void OpenDefinitionTarget(CortexNavigationService navigationService, CortexShellState state, LanguageServiceDefinitionResponse response)
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
