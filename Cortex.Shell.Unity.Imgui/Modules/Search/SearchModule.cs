using System;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Workbench;
using UnityEngine;
using Cortex.Services.Search;
using Cortex.Shell.Unity.Imgui;

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
        private readonly SearchWorkbenchPresentationService _presentationService = new SearchWorkbenchPresentationService();
        private readonly SemanticWorkspaceEditService _workspaceEditService = new SemanticWorkspaceEditService();

        public void Draw(
            WorkbenchSearchService searchService,
            ICortexNavigationService navigationService,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            ITextSearchService textSearchService,
            CortexShellState state)
        {
            EnsureStyles();
            _presentationService.RefreshResultsIfPending(searchService, projectCatalog, sourceLookupIndex, textSearchService, state);
            var summary = _presentationService.BuildSummary(state);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true));
            if (summary.HasSemanticView)
            {
                DrawSemanticSummary(summary);
                GUILayout.Space(6f);
                _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
                DrawSemanticContent(navigationService, documentService, state);
                GUILayout.EndScrollView();
            }
            else
            {
                DrawSearchSummary(summary);
                GUILayout.Space(6f);
                _scroll = GUILayout.BeginScrollView(_scroll, GUI.skin.box, GUILayout.ExpandHeight(true));
                DrawSearchResults(searchService, navigationService, state);
                GUILayout.EndScrollView();
            }
            GUILayout.EndVertical();
        }

        private void DrawSemanticSummary(SearchWorkbenchSummaryPresentation summary)
        {
            GUILayout.BeginVertical(_summaryStyle ?? GUI.skin.box);
            GUILayout.Label(summary != null ? summary.Title : string.Empty, _documentStyle ?? GUI.skin.label);
            GUILayout.Label(summary != null ? summary.Status : string.Empty, _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.EndVertical();
        }

        private void DrawSemanticContent(ICortexNavigationService navigationService, IDocumentService documentService, CortexShellState state)
        {
            switch (state.Semantic.Workbench.ActiveView)
            {
                case SemanticWorkbenchViewKind.RenamePreview:
                    DrawRenamePreview(navigationService, documentService, state);
                    return;
                case SemanticWorkbenchViewKind.References:
                    DrawLocationList(state.Semantic.Workbench.References != null ? state.Semantic.Workbench.References.Locations : null, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.PeekDefinition:
                    DrawPeekDefinition(state.Semantic.Workbench.PeekDefinition, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.CallHierarchy:
                    DrawCallHierarchy(state.Semantic.Workbench.CallHierarchy, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.ValueSource:
                    DrawValueSource(state.Semantic.Workbench.ValueSource, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.UnitTestGeneration:
                    DrawUnitTestGeneration(state.Semantic.Workbench.UnitTestGeneration, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.BaseSymbols:
                    DrawLocationList(state.Semantic.Workbench.BaseSymbols != null ? state.Semantic.Workbench.BaseSymbols.Locations : null, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.Implementations:
                    DrawLocationList(state.Semantic.Workbench.Implementations != null ? state.Semantic.Workbench.Implementations.Locations : null, navigationService, state);
                    return;
                case SemanticWorkbenchViewKind.DocumentEditPreview:
                    DrawDocumentEditPreview(navigationService, documentService, state);
                    return;
            }

            GUILayout.Label("No semantic results are active.", _emptyStateStyle ?? GUI.skin.label);
        }

        private void DrawRenamePreview(ICortexNavigationService navigationService, IDocumentService documentService, CortexShellState state)
        {
            var preview = state != null && state.Semantic != null && state.Semantic.Workbench != null ? state.Semantic.Workbench.RenamePreview : null;
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

            DrawDocumentChangeList(preview.Documents, navigationService, state);
        }

        private void DrawDocumentEditPreview(ICortexNavigationService navigationService, IDocumentService documentService, CortexShellState state)
        {
            var preview = state != null && state.Semantic != null && state.Semantic.Workbench != null ? state.Semantic.Workbench.DocumentEditPreview : null;
            if (preview == null)
            {
                GUILayout.Label("Document edit preview is not available.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            GUILayout.BeginHorizontal();
            var previousEnabled = GUI.enabled;
            GUI.enabled = preview.CanApply && preview.Documents != null && preview.Documents.Length > 0;
            if (GUILayout.Button(!string.IsNullOrEmpty(preview.ApplyLabel) ? preview.ApplyLabel : "Apply Changes", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(120f)))
            {
                string statusMessage;
                var applied = _workspaceEditService.ApplyDocumentEditPreview(state, documentService, preview, out statusMessage);
                state.StatusMessage = statusMessage;
                if (applied && navigationService != null && preview.Documents.Length == 1 && preview.Documents[0] != null)
                {
                    var firstEdit = preview.Documents[0].Edits != null && preview.Documents[0].Edits.Length > 0
                        ? preview.Documents[0].Edits[0]
                        : null;
                    _presentationService.OpenLocation(navigationService, state, preview.Documents[0].DocumentPath, firstEdit != null ? firstEdit.Range : null);
                }
            }

            GUI.enabled = previousEnabled;
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.Space(4f);

            if (preview.Documents == null || preview.Documents.Length == 0)
            {
                GUILayout.Label(preview.StatusMessage ?? "No preview edits were produced.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            DrawDocumentChangeList(preview.Documents, navigationService, state);
        }

        private void DrawPeekDefinition(LanguageServiceDefinitionResponse peekDefinition, ICortexNavigationService navigationService, CortexShellState state)
        {
            if (peekDefinition == null)
            {
                GUILayout.Label("Peek definition is not available.", _emptyStateStyle ?? GUI.skin.label);
                return;
            }

            if (GUILayout.Button("Open Definition", _actionButtonStyle ?? GUI.skin.button, GUILayout.Width(120f)))
            {
                _presentationService.OpenDefinition(navigationService, state, peekDefinition);
            }

            GUILayout.Space(6f);
            GUILayout.TextArea(peekDefinition.PreviewText ?? string.Empty, GUILayout.ExpandHeight(true));
        }

        private void DrawCallHierarchy(LanguageServiceCallHierarchyResponse response, ICortexNavigationService navigationService, CortexShellState state)
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

        private void DrawCallHierarchySection(string title, LanguageServiceCallHierarchyItem[] items, ICortexNavigationService navigationService, CortexShellState state)
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

        private void DrawValueSource(LanguageServiceValueSourceResponse response, ICortexNavigationService navigationService, CortexShellState state)
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
                if (GUILayout.Button(_presentationService.BuildLocationButtonLabel(item.Location), _resultButtonStyle ?? GUI.skin.button, GUILayout.Height(24f)))
                {
                    _presentationService.OpenLocation(navigationService, state, item.Location.DocumentPath, item.Location.Range);
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawUnitTestGeneration(UnitTestGenerationPlan plan, ICortexNavigationService navigationService, CortexShellState state)
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

        private void DrawLocationList(LanguageServiceSymbolLocation[] locations, ICortexNavigationService navigationService, CortexShellState state)
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
                if (GUILayout.Button(_presentationService.BuildLocationButtonLabel(location), _resultButtonStyle ?? GUI.skin.button, GUILayout.Height(24f)))
                {
                    _presentationService.OpenLocation(navigationService, state, location.DocumentPath, location.Range);
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawDocumentChangeList(LanguageServiceDocumentChange[] documents, ICortexNavigationService navigationService, CortexShellState state)
        {
            for (var i = 0; i < documents.Length; i++)
            {
                var document = documents[i];
                if (document == null)
                {
                    continue;
                }

                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label(Path.GetFileName(document.DocumentPath) + "  (" + document.ChangeCount + " edit(s))", _documentStyle ?? GUI.skin.label);
                GUILayout.Label(document.DocumentPath ?? string.Empty, _documentMetaStyle ?? GUI.skin.label);
                for (var j = 0; document.Edits != null && j < document.Edits.Length; j++)
                {
                    var edit = document.Edits[j];
                    if (edit == null)
                    {
                        continue;
                    }

                    if (GUILayout.Button(edit.PreviewText ?? edit.NewText ?? string.Empty, _resultButtonStyle ?? GUI.skin.button))
                    {
                        _presentationService.OpenLocation(navigationService, state, document.DocumentPath, edit.Range);
                    }
                }

                GUILayout.EndVertical();
                GUILayout.Space(4f);
            }
        }

        private void DrawSearchSummary(SearchWorkbenchSummaryPresentation summary)
        {
            GUILayout.BeginVertical(_summaryStyle ?? GUI.skin.box);
            GUILayout.Label(summary != null ? summary.Title : string.Empty, _documentStyle ?? GUI.skin.label);
            GUILayout.Label(summary != null ? summary.Status : string.Empty, _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.Label(summary != null ? summary.ScopeCaption : string.Empty, _summaryCaptionStyle ?? GUI.skin.label);
            GUILayout.EndVertical();
        }

        private void DrawSearchResults(WorkbenchSearchService searchService, ICortexNavigationService navigationService, CortexShellState state)
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

        private void EnsureStyles()
        {
            var themeKey = ImguiWorkbenchLayout.GetBackgroundColor().ToString() + "|" + ImguiWorkbenchLayout.GetAccentColor().ToString();
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
            _summaryBackground = MakeFill(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.45f));
            _activeBackground = MakeFill(ImguiWorkbenchLayout.WithAlpha(ImguiWorkbenchLayout.GetAccentColor(), 0.20f));

            _summaryStyle = new GUIStyle(GUI.skin.box);
            _summaryStyle.padding = new RectOffset(10, 10, 8, 8);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_summaryStyle, _summaryBackground);

            _summaryCaptionStyle = new GUIStyle(GUI.skin.label);
            ImguiStyleUtil.ApplyTextColorToAllStates(_summaryCaptionStyle, ImguiWorkbenchLayout.GetTextColor());
            _summaryCaptionStyle.wordWrap = true;

            _documentStyle = new GUIStyle(GUI.skin.label);
            _documentStyle.fontStyle = FontStyle.Bold;
            ImguiStyleUtil.ApplyTextColorToAllStates(_documentStyle, ImguiWorkbenchLayout.GetTextColor());

            _documentMetaStyle = new GUIStyle(GUI.skin.label);
            _documentMetaStyle.wordWrap = true;
            ImguiStyleUtil.ApplyTextColorToAllStates(_documentMetaStyle, ImguiWorkbenchLayout.GetMutedTextColor());

            _resultButtonStyle = new GUIStyle(GUI.skin.button);
            _resultButtonStyle.alignment = TextAnchor.MiddleLeft;
            _resultButtonStyle.wordWrap = false;

            _resultButtonActiveStyle = new GUIStyle(_resultButtonStyle);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_resultButtonActiveStyle, _activeBackground);

            _emptyStateStyle = new GUIStyle(GUI.skin.label);
            _emptyStateStyle.wordWrap = true;
            ImguiStyleUtil.ApplyTextColorToAllStates(_emptyStateStyle, ImguiWorkbenchLayout.GetMutedTextColor());

            _actionButtonStyle = new GUIStyle(GUI.skin.button);
            _actionButtonStyle.alignment = TextAnchor.MiddleCenter;
            ImguiStyleUtil.ApplyTextColorToAllStates(_actionButtonStyle, ImguiWorkbenchLayout.GetTextColor());
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
