using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    /// <summary>
    /// Renders the document tab strip, path bar, editor surface, and status bar for
    /// the active document. File navigation and document lifecycle remain outside
    /// this module.
    /// </summary>
    public sealed class EditorModule
    {
        private const string FindQueryControlName = "cortex.find.query";
        private const float TabHeight = 26f;
        private const float TabWidth = 160f;
        private const float TabStripHeight = 28f;
        private const float PathBarHeight = 22f;
        private const float FindOverlayWidth = 336f;
        private const float FindOverlayTopRowHeight = 24f;
        private const float FindOverlayBottomRowHeight = 20f;
        private const float FindOverlayPadding = 8f;
        private const float MinimumUsableCodeAreaWidth = 64f;
        private const float MinimumUsableCodeAreaHeight = 64f;
        private const float StatusBarHeight = 20f;
        private const float GutterWidth = 52f;

        private Vector2 _tabScroll = Vector2.zero;
        private Vector2 _editorScroll = Vector2.zero;
        // Sheltered's Unity runtime does not expose Rect.zero, so keep an explicit
        // zero rect here instead of relying on the convenience property.
        private Rect _lastCodeAreaRect = new Rect(0f, 0f, 0f, 0f);
        private string _lastFindOverlayLogKey = string.Empty;
        private DateTime _lastFindOverlayLogUtc = DateTime.MinValue;
        private string _appliedTheme = string.Empty;
        private GUIStyle _tabStyle;
        private GUIStyle _activeTabStyle;
        private GUIStyle _dirtyTabStyle;
        private GUIStyle _gutterReadOnlyStyle;
        private GUIStyle _editorReadOnlyStyle;
        private GUIStyle _pathBarStyle;
        private GUIStyle _statusBarStyle;
        private GUIStyle _emptyStateStyle;
        private GUIStyle _toolbarButtonStyle;
        private GUIStyle _statusModeButtonStyle;
        private GUIStyle _statusModeButtonActiveStyle;
        private GUIStyle _statusModeButtonDisabledStyle;
        private GUIStyle _statusTooltipStyle;
        private GUIStyle _tabCloseButtonStyle;
        private GUIStyle _codeTooltipStyle;
        private GUIStyle _contextMenuStyle;
        private GUIStyle _contextMenuButtonStyle;
        private GUIStyle _contextMenuHeaderStyle;
        private Font _monoFont;
        private Texture2D _tabBackground;
        private Texture2D _activeTabBackground;
        private Texture2D _dirtyTabBackground;
        private Texture2D _gutterBackground;
        private Texture2D _editorBackground;
        private Texture2D _pathBarBackground;
        private Texture2D _statusBackground;
        private Texture2D _toolbarBackground;
        private Texture2D _statusModeBackground;
        private Texture2D _statusModeActiveBackground;
        private Texture2D _statusModeDisabledBackground;
        private readonly CodeViewSurface _codeViewSurface = new CodeViewSurface();
        private readonly EditableCodeViewSurface _editableCodeViewSurface = new EditableCodeViewSurface();
        private readonly IEditorService _editorService = new EditorService();
        private readonly EditorDocumentModeService _documentModeService = new EditorDocumentModeService();

        internal void Draw(
            IDocumentService documentService,
            Services.CortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            Services.WorkbenchSearchService workbenchSearchService,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyPatchInspectionService,
            HarmonyPatchResolutionService harmonyPatchResolutionService,
            HarmonyPatchDisplayService harmonyPatchDisplayService,
            HarmonyPatchGenerationService harmonyPatchGenerationService,
            GeneratedTemplateNavigationService generatedTemplateNavigationService,
            CortexShellState state)
        {
            EditorCommandContributions.EnsureRegistered(commandRegistry, contributionRegistry, state);
            EnsureStyles(state);
            HandleSearchShortcuts(commandRegistry, state);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            if (state.Documents.OpenDocuments.Count == 0 || state.Documents.ActiveDocument == null)
            {
                DrawEmptyState();
                GUILayout.EndVertical();
                return;
            }

            DrawTabStrip(state);
            StabilizeActiveDocumentViewState(documentService, state);
            DrawPathBar(documentService, state);
            DrawCodeArea(
                documentService,
                navigationService,
                commandRegistry,
                contributionRegistry,
                projectCatalog,
                loadedModCatalog,
                sourceLookupIndex,
                harmonyPatchInspectionService,
                harmonyPatchResolutionService,
                harmonyPatchDisplayService,
                harmonyPatchGenerationService,
                generatedTemplateNavigationService,
                state);
            DrawFindOverlay(commandRegistry, workbenchSearchService, state);
            DrawStatusBar(state);

            GUILayout.EndVertical();
        }

        private void HandleSearchShortcuts(ICommandRegistry commandRegistry, CortexShellState state)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.KeyDown || commandRegistry == null || state == null || state.Documents.ActiveDocument == null)
            {
                return;
            }

            if (GUIUtility.keyboardControl != 0 &&
                !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()) &&
                !string.Equals(GUI.GetNameOfFocusedControl(), FindQueryControlName, StringComparison.Ordinal))
            {
                return;
            }

            if (current.control && !current.alt && !current.shift && current.keyCode == KeyCode.F)
            {
                ExecuteWorkbenchCommand(commandRegistry, "cortex.editor.find", state);
                current.Use();
                return;
            }

            if (!state.Search.IsVisible)
            {
                return;
            }

            if (!current.control && !current.alt && !current.shift && current.keyCode == KeyCode.F3)
            {
                ExecuteWorkbenchCommand(commandRegistry, "cortex.search.next", state);
                current.Use();
            }
            else if (!current.control && !current.alt && current.shift && current.keyCode == KeyCode.F3)
            {
                ExecuteWorkbenchCommand(commandRegistry, "cortex.search.previous", state);
                current.Use();
            }
            else if (!current.control && !current.alt && !current.shift && current.keyCode == KeyCode.Escape)
            {
                ExecuteWorkbenchCommand(commandRegistry, "cortex.search.close", state);
                current.Use();
            }
        }

        private void DrawEmptyState()
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginVertical();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Open a file from the Explorer or click a log entry to jump to source.", _emptyStateStyle ?? GUI.skin.label);
            GUILayout.Space(8f);
            GUILayout.Label("Editing is locked by default to prevent accidental changes during gameplay.", _emptyStateStyle ?? GUI.skin.label);
            GUILayout.FlexibleSpace();
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
        }

        private void DrawTabStrip(CortexShellState state)
        {
            _tabScroll = GUILayout.BeginScrollView(
                _tabScroll,
                false,
                false,
                GUIStyle.none,
                GUIStyle.none,
                GUIStyle.none,
                GUILayout.Height(TabStripHeight));

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            for (var i = 0; i < state.Documents.OpenDocuments.Count; i++)
            {
                var session = state.Documents.OpenDocuments[i];
                var isActive = session == state.Documents.ActiveDocument;
                var displayName = CortexModuleUtil.GetDocumentDisplayName(session);
                var style = isActive ? _activeTabStyle : (session.IsDirty ? _dirtyTabStyle : _tabStyle);
                var tabRect = GUILayoutUtility.GetRect(TabWidth, TabWidth, TabHeight, TabHeight);
                var current = Event.current;
                var isHovered = current != null && tabRect.Contains(current.mousePosition);
                var closeRect = new Rect(tabRect.xMax - 18f, tabRect.y + 5f, 14f, Mathf.Max(12f, tabRect.height - 10f));

                if ((isHovered || isActive) &&
                    current != null &&
                    current.type == EventType.MouseDown &&
                    current.button == 0 &&
                    closeRect.Contains(current.mousePosition))
                {
                    CortexModuleUtil.CloseDocument(state, session.FilePath);
                    current.Use();
                    break;
                }

                if (GUI.Toggle(tabRect, isActive, displayName, style ?? GUI.skin.button) && !isActive)
                {
                    state.Documents.ActiveDocument = session;
                    state.Documents.ActiveDocumentPath = session.FilePath;
                }

                if (isHovered || isActive)
                {
                    GUI.Box(closeRect, "X", _tabCloseButtonStyle ?? GUI.skin.button);
                }

                if (current != null && current.type == EventType.MouseDown && current.button == 2 && tabRect.Contains(current.mousePosition))
                {
                    CortexModuleUtil.CloseDocument(state, session.FilePath);
                    current.Use();
                    break;
                }
            }

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.EndScrollView();
        }

        private void DrawPathBar(IDocumentService documentService, CortexShellState state)
        {
            var active = state.Documents.ActiveDocument;
            if (active == null)
            {
                return;
            }

            var allowSaving = state.Settings != null && state.Settings.EnableFileSaving && active.SupportsSaving;
            var canReload = documentService != null;
            var closeRequested = false;

            GUILayout.BeginHorizontal(_pathBarStyle ?? GUI.skin.box, GUILayout.Height(PathBarHeight));

            GUILayout.Label(BuildCompactPath(active.FilePath), GUILayout.ExpandWidth(true));

            if (active.HighlightedLine > 0)
            {
                GUILayout.Label("Ln " + active.HighlightedLine, GUILayout.Width(62f));
            }

            if (active.HasExternalChanges)
            {
                GUILayout.Label("External change", GUILayout.Width(120f));
            }

            var previousEnabled = GUI.enabled;

            GUI.enabled = allowSaving;
            if (GUILayout.Button("Save", _toolbarButtonStyle ?? GUI.skin.button, GUILayout.Width(52f)))
            {
                if (documentService.Save(active))
                {
                    state.StatusMessage = "Saved " + System.IO.Path.GetFileName(active.FilePath);
                }
                else
                {
                    state.StatusMessage = "Save blocked: file changed outside the current snapshot.";
                }
            }

            GUI.enabled = canReload;
            if (GUILayout.Button("Reload", _toolbarButtonStyle ?? GUI.skin.button, GUILayout.Width(56f)))
            {
                documentService.Reload(active);
                state.StatusMessage = "Reloaded from disk.";
            }

            GUI.enabled = previousEnabled;
            if (GUILayout.Button("X", _toolbarButtonStyle ?? GUI.skin.button, GUILayout.Width(26f)))
            {
                closeRequested = true;
            }

            GUILayout.EndHorizontal();
            if (closeRequested)
            {
                CortexModuleUtil.CloseDocument(state, active.FilePath);
            }
        }

        private void StabilizeActiveDocumentViewState(IDocumentService documentService, CortexShellState state)
        {
            var active = state != null && state.Documents != null ? state.Documents.ActiveDocument : null;
            if (active == null)
            {
                return;
            }

            _editorService.EnsureDocumentState(active);
            if (documentService != null)
            {
                documentService.HasExternalChanges(active);
            }
        }

        private void DrawFindOverlay(ICommandRegistry commandRegistry, Services.WorkbenchSearchService workbenchSearchService, CortexShellState state)
        {
            var search = state != null ? state.Search : null;
            if (search == null || !search.IsVisible)
            {
                return;
            }

            var query = search.Query;
            var current = Event.current;
            var codeAreaRect = _lastCodeAreaRect;
            if (!IsUsableCodeAreaRect(codeAreaRect))
            {
                LogFindOverlayState("Skipped render: code area rect was not usable.", search, codeAreaRect, new Rect(0f, 0f, 0f, 0f), true);
                return;
            }

            var menuHeight = search.ScopeMenuOpen ? 94f : 0f;
            var overlayRect = BuildFindOverlayRect(codeAreaRect, menuHeight);
            var topRowRect = new Rect(FindOverlayPadding, FindOverlayPadding, overlayRect.width - FindOverlayPadding * 2f, FindOverlayTopRowHeight);
            var bottomRowRect = new Rect(FindOverlayPadding, topRowRect.yMax + 4f, overlayRect.width - FindOverlayPadding * 2f, FindOverlayBottomRowHeight);
            var queryRect = new Rect(topRowRect.x, topRowRect.y, topRowRect.width - 104f, topRowRect.height);
            var summaryRect = new Rect(queryRect.xMax + 4f, topRowRect.y + 2f, 30f, topRowRect.height - 4f);
            var prevRect = new Rect(summaryRect.xMax + 2f, topRowRect.y, 18f, topRowRect.height);
            var nextRect = new Rect(prevRect.xMax + 2f, topRowRect.y, 22f, topRowRect.height);
            var closeRect = new Rect(nextRect.xMax + 2f, topRowRect.y, 22f, topRowRect.height);
            var toggleCaseRect = new Rect(bottomRowRect.x, bottomRowRect.y, 30f, bottomRowRect.height);
            var toggleWordRect = new Rect(toggleCaseRect.xMax + 4f, bottomRowRect.y, 26f, bottomRowRect.height);
            var scopeRect = new Rect(bottomRowRect.xMax - 154f, bottomRowRect.y, 154f, bottomRowRect.height);
            LogFindOverlayState("Rendering overlay.", search, codeAreaRect, overlayRect, false);

            GUI.Box(overlayRect, GUIContent.none, _pathBarStyle ?? GUI.skin.box);
            GUILayout.BeginArea(overlayRect);
            GUI.SetNextControlName(FindQueryControlName);
            var updatedQuery = GUI.TextField(queryRect, search.QueryText ?? string.Empty);
            if (!string.Equals(updatedQuery, search.QueryText, StringComparison.Ordinal))
            {
                search.QueryText = updatedQuery;
                search.PendingRefresh = true;
                search.ActiveMatchIndex = -1;
            }

            if (search.FocusQueryRequested)
            {
                GUIUtility.hotControl = 0;
                GUIUtility.keyboardControl = 0;
                GUI.FocusControl(FindQueryControlName);
                if (string.Equals(GUI.GetNameOfFocusedControl(), FindQueryControlName, StringComparison.Ordinal))
                {
                    SelectAllFocusedFindQuery();
                    search.FocusQueryRequested = false;
                }
            }

            GUI.Label(summaryRect, BuildFindSummary(workbenchSearchService, state));

            if (GUI.Button(prevRect, "<", _toolbarButtonStyle ?? GUI.skin.button))
            {
                ExecuteWorkbenchCommand(commandRegistry, "cortex.search.previous", state);
            }

            if (GUI.Button(nextRect, ">", _toolbarButtonStyle ?? GUI.skin.button))
            {
                ExecuteWorkbenchCommand(commandRegistry, "cortex.search.next", state);
            }

            if (GUI.Button(closeRect, "X", _toolbarButtonStyle ?? GUI.skin.button))
            {
                ExecuteWorkbenchCommand(commandRegistry, "cortex.search.close", state);
            }

            var updatedMatchCase = GUI.Toggle(toggleCaseRect, query.MatchCase, "Aa", _toolbarButtonStyle ?? GUI.skin.button);
            if (updatedMatchCase != query.MatchCase)
            {
                query.MatchCase = updatedMatchCase;
                search.PendingRefresh = true;
                search.ActiveMatchIndex = -1;
            }

            var updatedWholeWord = GUI.Toggle(toggleWordRect, query.WholeWord, "W", _toolbarButtonStyle ?? GUI.skin.button);
            if (updatedWholeWord != query.WholeWord)
            {
                query.WholeWord = updatedWholeWord;
                search.PendingRefresh = true;
                search.ActiveMatchIndex = -1;
            }

            if (GUI.Button(scopeRect, BuildScopeLabel(query.Scope) + " v", _toolbarButtonStyle ?? GUI.skin.button))
            {
                search.ScopeMenuOpen = !search.ScopeMenuOpen;
            }

            if (search.ScopeMenuOpen)
            {
                var menuRect = new Rect(scopeRect.x, bottomRowRect.yMax + 4f, scopeRect.width, 90f);
                GUI.Box(menuRect, GUIContent.none, GUI.skin.box);
                DrawScopeButton(commandRegistry, state, SearchScopeKind.CurrentDocument, "Current document", new Rect(menuRect.x + 4f, menuRect.y + 4f, menuRect.width - 8f, 19f));
                DrawScopeButton(commandRegistry, state, SearchScopeKind.AllOpenDocuments, "All open documents", new Rect(menuRect.x + 4f, menuRect.y + 25f, menuRect.width - 8f, 19f));
                DrawScopeButton(commandRegistry, state, SearchScopeKind.CurrentProject, "Current project", new Rect(menuRect.x + 4f, menuRect.y + 46f, menuRect.width - 8f, 19f));
                DrawScopeButton(commandRegistry, state, SearchScopeKind.EntireSolution, "Entire solution", new Rect(menuRect.x + 4f, menuRect.y + 67f, menuRect.width - 8f, 19f));
            }
            GUILayout.EndArea();

            var queryHasFocus = string.Equals(GUI.GetNameOfFocusedControl(), FindQueryControlName, StringComparison.Ordinal);
            if (current != null && current.type == EventType.KeyDown && queryHasFocus)
            {
                if (current.keyCode == KeyCode.Return || current.keyCode == KeyCode.KeypadEnter)
                {
                    ExecuteWorkbenchCommand(commandRegistry, "cortex.search.next", state);
                    current.Use();
                }
                else if (current.keyCode == KeyCode.Escape)
                {
                    ExecuteWorkbenchCommand(commandRegistry, "cortex.search.close", state);
                    current.Use();
                }
            }
        }

        private void DrawScopeButton(ICommandRegistry commandRegistry, CortexShellState state, SearchScopeKind scope, string label, Rect rect)
        {
            var selected = state != null && state.Search != null && state.Search.Query.Scope == scope;
            if (GUI.Button(rect, (selected ? "> " : string.Empty) + label, _toolbarButtonStyle ?? GUI.skin.button))
            {
                state.Search.Query.Scope = scope;
                state.Search.ScopeMenuOpen = false;
                state.Search.PendingRefresh = true;
                state.Search.ActiveMatchIndex = -1;
                state.Search.FocusQueryRequested = true;
                ExecuteWorkbenchCommand(commandRegistry, "cortex.window.search", state);
            }
        }

        private string BuildFindSummary(Services.WorkbenchSearchService workbenchSearchService, CortexShellState state)
        {
            if (state == null || state.Search == null)
            {
                return string.Empty;
            }

            if (state.Search.PendingRefresh)
            {
                return string.IsNullOrEmpty(state.Search.QueryText) ? "Type to search" : "Press Enter";
            }

            var results = state.Search.Results;
            if (results == null)
            {
                return "Press Enter";
            }

            var total = workbenchSearchService != null ? workbenchSearchService.CountMatches(results) : results.TotalMatchCount;
            if (total <= 0)
            {
                return results.StatusMessage ?? "No matches";
            }

            var active = state.Search.ActiveMatchIndex >= 0 ? state.Search.ActiveMatchIndex + 1 : 0;
            return active + "/" + total;
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

        private static void ExecuteWorkbenchCommand(ICommandRegistry commandRegistry, string commandId, CortexShellState state)
        {
            if (commandRegistry == null || string.IsNullOrEmpty(commandId))
            {
                return;
            }

            commandRegistry.Execute(commandId, new CommandExecutionContext
            {
                ActiveContainerId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                ActiveDocumentId = state != null ? state.Documents.ActiveDocumentPath : string.Empty,
                FocusedRegionId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                Parameter = state != null ? state.Documents.ActiveDocument : null
            });
        }

        private void DrawCodeArea(
            IDocumentService documentService,
            Services.CortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyPatchInspectionService,
            HarmonyPatchResolutionService harmonyPatchResolutionService,
            HarmonyPatchDisplayService harmonyPatchDisplayService,
            HarmonyPatchGenerationService harmonyPatchGenerationService,
            GeneratedTemplateNavigationService generatedTemplateNavigationService,
            CortexShellState state)
        {
            var active = state.Documents.ActiveDocument;
            if (active == null)
            {
                // Sheltered's Unity runtime does not expose Rect.zero.
                _lastCodeAreaRect = new Rect(0f, 0f, 0f, 0f);
                return;
            }

            _editorService.EnsureDocumentState(active);
            var settings = state != null ? state.Settings : null;
            var usesUnifiedSourceSurface = _documentModeService.UsesUnifiedSourceSurface(active);
            var isEditable = _documentModeService.IsEditingEnabled(settings, active);
            var rect = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            // IMGUI layout passes can report a placeholder 1x1 rect before the real
            // repaint bounds exist. Keep the last usable editor viewport so the find
            // overlay has stable coordinates across layout/repaint pairs.
            if (IsUsableCodeAreaRect(rect))
            {
                _lastCodeAreaRect = rect;
            }

            var overlayBlockRect = BuildActiveFindInputBlockRect(state, rect);

            if (usesUnifiedSourceSurface)
            {
                var previousEditorScroll = _editorScroll;
                _editorScroll = _editableCodeViewSurface.Draw(
                    rect,
                    _editorScroll,
                    active,
                    isEditable,
                    commandRegistry,
                    contributionRegistry,
                    state,
                    _appliedTheme,
                    _editorReadOnlyStyle,
                    _gutterReadOnlyStyle,
                    _codeTooltipStyle,
                    _contextMenuStyle,
                    _contextMenuButtonStyle,
                    _contextMenuHeaderStyle,
                    overlayBlockRect,
                    GutterWidth,
                    harmonyPatchGenerationService,
                    generatedTemplateNavigationService);
                LogEditorScrollChange(active, previousEditorScroll, _editorScroll);
                return;
            }

            var previousReadOnlyScroll = _editorScroll;
            _editorScroll = _codeViewSurface.Draw(
                rect,
                _editorScroll,
                active,
                navigationService,
                commandRegistry,
                contributionRegistry,
                state,
                _appliedTheme,
                _editorReadOnlyStyle,
                _gutterReadOnlyStyle,
                _codeTooltipStyle,
                _contextMenuStyle,
                _contextMenuButtonStyle,
                _contextMenuHeaderStyle,
                overlayBlockRect,
                GutterWidth,
                projectCatalog,
                loadedModCatalog,
                sourceLookupIndex,
                harmonyPatchInspectionService,
                harmonyPatchResolutionService,
                harmonyPatchDisplayService);
            LogEditorScrollChange(active, previousReadOnlyScroll, _editorScroll);
        }

        private static void LogEditorScrollChange(DocumentSession active, Vector2 previousScroll, Vector2 currentScroll)
        {
            // Scroll diagnostics are intentionally suppressed to keep logs focused on workflow-level behavior.
        }

        private Rect BuildActiveFindInputBlockRect(CortexShellState state, Rect codeAreaRect)
        {
            var search = state != null ? state.Search : null;
            if (search == null || !search.IsVisible || !IsUsableCodeAreaRect(codeAreaRect))
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            return BuildFindOverlayRect(codeAreaRect, search.ScopeMenuOpen ? 94f : 0f);
        }

        private static Rect BuildFindOverlayRect(Rect codeAreaRect, float menuHeight)
        {
            var overlayHeight = FindOverlayPadding * 2f + FindOverlayTopRowHeight + 4f + FindOverlayBottomRowHeight + menuHeight;
            return new Rect(
                codeAreaRect.xMax - FindOverlayWidth,
                codeAreaRect.y,
                FindOverlayWidth,
                overlayHeight);
        }

        private static bool IsUsableCodeAreaRect(Rect rect)
        {
            return rect.width >= MinimumUsableCodeAreaWidth && rect.height >= MinimumUsableCodeAreaHeight;
        }

        private static void SelectAllFocusedFindQuery()
        {
            var textEditor = GUIUtility.GetStateObject(typeof(TextEditor), GUIUtility.keyboardControl) as TextEditor;
            if (textEditor != null)
            {
                textEditor.SelectAll();
            }
        }

        private void LogFindOverlayState(string message, CortexSearchInteractionState search, Rect codeAreaRect, Rect overlayRect, bool force)
        {
            var searchText = search != null ? search.QueryText ?? string.Empty : string.Empty;
            var scope = search != null ? search.Query.Scope.ToString() : string.Empty;
            var key = message + "|" +
                searchText + "|" +
                scope + "|" +
                (search != null && search.ScopeMenuOpen) + "|" +
                codeAreaRect.x.ToString("F0") + "," + codeAreaRect.y.ToString("F0") + "," + codeAreaRect.width.ToString("F0") + "," + codeAreaRect.height.ToString("F0") + "|" +
                overlayRect.x.ToString("F0") + "," + overlayRect.y.ToString("F0") + "," + overlayRect.width.ToString("F0") + "," + overlayRect.height.ToString("F0");
            if (!force &&
                string.Equals(_lastFindOverlayLogKey, key, StringComparison.Ordinal) &&
                (DateTime.UtcNow - _lastFindOverlayLogUtc).TotalSeconds < 1d)
            {
                return;
            }

                _lastFindOverlayLogKey = key;
                _lastFindOverlayLogUtc = DateTime.UtcNow;
                MMLog.WriteInfo("[Cortex.FindUI] " + message +
                " Query='" + searchText +
                "', Scope=" + scope +
                ", ScopeMenuOpen=" + (search != null && search.ScopeMenuOpen) +
                ", CodeArea=(" + codeAreaRect.x.ToString("F1") + "," + codeAreaRect.y.ToString("F1") + "," + codeAreaRect.width.ToString("F1") + "," + codeAreaRect.height.ToString("F1") + ")" +
                ", Overlay=(" + overlayRect.x.ToString("F1") + "," + overlayRect.y.ToString("F1") + "," + overlayRect.width.ToString("F1") + "," + overlayRect.height.ToString("F1") + ").");
        }

        private void DrawStatusBar(CortexShellState state)
        {
            var active = state.Documents.ActiveDocument;
            if (active == null)
            {
                return;
            }

            _editorService.EnsureDocumentState(active);
            var lineCount = _editorService.GetLineCount(active);
            var caret = active.EditorState != null
                ? _editorService.GetCaretPosition(active, active.EditorState.CaretIndex)
                : new EditorCaretPosition();
            var settings = state != null ? state.Settings : null;
            var canEditDocument = _documentModeService.CanToggleEditing(settings, active);
            var isEditing = _documentModeService.IsEditingEnabled(settings, active);
            var savingAllowed = state.Settings != null && state.Settings.EnableFileSaving;
            var analysis = active.LanguageAnalysis;
            var errorCount = CountDiagnostics(analysis, "Error");
            var warningCount = CountDiagnostics(analysis, "Warning");
            var roslynLabel = BuildRoslynLabel(state, analysis, errorCount, warningCount);
            var augmentationLabel = BuildCompletionAugmentationLabel(state, active);
            var modeTooltip = _documentModeService.BuildEditModeTooltip(active, settings);
            var modeContent = new GUIContent(isEditing ? "EDIT" : "READ", modeTooltip);
            var tooltip = string.Empty;
            Rect modeRect;
            var canToggleMode = canEditDocument;

            GUILayout.BeginHorizontal(_statusBarStyle ?? GUI.skin.box, GUILayout.Height(StatusBarHeight));
            GUI.enabled = canToggleMode;
            if (GUILayout.Button(modeContent, ResolveStatusModeButtonStyle(canToggleMode, isEditing), GUILayout.Width(68f)))
            {
                _documentModeService.SetEditingEnabled(settings, active, !isEditing);
                state.StatusMessage = _documentModeService.IsEditingEnabled(settings, active)
                    ? "Edit mode enabled for " + CortexModuleUtil.GetDocumentDisplayName(active) + "."
                    : "Read mode enabled for " + CortexModuleUtil.GetDocumentDisplayName(active) + ".";
            }
            GUI.enabled = true;

            modeRect = GUILayoutUtility.GetLastRect();
            tooltip = Event.current != null && modeRect.Contains(Event.current.mousePosition) ? modeContent.tooltip : string.Empty;

            if (active.IsDirty)
            {
                GUILayout.Label("*", GUILayout.Width(10f));
            }

            GUILayout.Label("Ln " + (caret.Line + 1) + ", Col " + (caret.Column + 1) + "   " + lineCount + " lines", GUILayout.ExpandWidth(false));
            GUILayout.Space(10f);
            GUILayout.Label(roslynLabel, GUILayout.ExpandWidth(false));
            GUILayout.Space(10f);
            GUILayout.Label(augmentationLabel, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();

            GUI.enabled = savingAllowed && active.SupportsSaving;
            if (GUILayout.Button("Save All", _toolbarButtonStyle ?? GUI.skin.button, GUILayout.Width(70f)))
            {
                state.StatusMessage = "Save All requested.";
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            DrawStatusTooltip(modeRect, tooltip);
        }

        private static int CountDiagnostics(LanguageServiceAnalysisResponse analysis, string severity)
        {
            if (analysis == null || analysis.Diagnostics == null || analysis.Diagnostics.Length == 0)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < analysis.Diagnostics.Length; i++)
            {
                if (string.Equals(analysis.Diagnostics[i].Severity, severity, StringComparison.OrdinalIgnoreCase))
                {
                    count++;
                }
            }

            return count;
        }

        private static string BuildRoslynLabel(CortexShellState state, LanguageServiceAnalysisResponse analysis, int errorCount, int warningCount)
        {
            var status = state != null ? state.LanguageServiceStatus : null;
            if (status == null)
            {
                return "Roslyn: offline";
            }

            if (!status.IsRunning)
            {
                return "Roslyn: " + (string.IsNullOrEmpty(status.StatusMessage) ? "standby" : status.StatusMessage);
            }

            if (analysis == null)
            {
                return "Roslyn: ready";
            }

            if (!HasResolvedAnalysis(analysis))
            {
                return "Roslyn: analyzing";
            }

            if (!analysis.Success)
            {
                return "Roslyn: " + (string.IsNullOrEmpty(analysis.StatusMessage) ? "analysis failed" : analysis.StatusMessage);
            }

            return "Roslyn E:" + errorCount + " W:" + warningCount;
        }

        private static string BuildCompletionAugmentationLabel(CortexShellState state, DocumentSession active)
        {
            var settings = state != null ? state.Settings : null;
            if (settings == null || !settings.EnableCompletionAugmentation)
            {
                return "AI: off";
            }

            var editor = state != null ? state.Editor : null;
            var providerId = editor != null ? editor.CompletionAugmentationProviderId ?? string.Empty : string.Empty;
            var status = editor != null ? editor.CompletionAugmentationStatus ?? string.Empty : string.Empty;
            var statusMessage = editor != null ? editor.CompletionAugmentationStatusMessage ?? string.Empty : string.Empty;
            var hasInlineSuggestion = editor != null &&
                active != null &&
                editor.ActiveInlineCompletionResponse != null &&
                string.Equals(editor.ActiveInlineCompletionProviderId ?? string.Empty, providerId, StringComparison.OrdinalIgnoreCase);

            if (string.IsNullOrEmpty(providerId) &&
                !string.IsNullOrEmpty(settings.CompletionAugmentationProviderId))
            {
                providerId = settings.CompletionAugmentationProviderId ?? string.Empty;
            }

            if (string.IsNullOrEmpty(providerId))
            {
                return "AI: standby";
            }

            if (string.Equals(providerId, "tabby", StringComparison.OrdinalIgnoreCase) && !settings.EnableTabbyCompletion)
            {
                return "Tabby: off";
            }

            var providerLabel = CompletionAugmentationProviderIds.GetDisplayName(providerId);
            if (hasInlineSuggestion)
            {
                return providerLabel + ": suggestion";
            }

            switch ((status ?? string.Empty).ToLowerInvariant())
            {
                case "starting":
                    return providerLabel + ": starting";
                case "thinking":
                    return providerLabel + ": thinking";
                case "suggestion":
                    return providerLabel + ": suggestion";
                case "ready":
                    return providerLabel + ": ready";
                case "offline":
                    return providerLabel + ": offline";
                case "error":
                    return providerLabel + ": " + BuildCompletionStatusMessage(statusMessage);
            }

            return providerLabel + ": ready";
        }

        private static string BuildCompletionStatusMessage(string statusMessage)
        {
            if (string.IsNullOrEmpty(statusMessage))
            {
                return "error";
            }

            return statusMessage.Length <= 24
                ? statusMessage
                : statusMessage.Substring(0, 21) + "...";
        }

        private static bool HasResolvedAnalysis(LanguageServiceAnalysisResponse analysis)
        {
            return analysis != null &&
                (analysis.Success ||
                 !string.IsNullOrEmpty(analysis.StatusMessage) ||
                 !string.IsNullOrEmpty(analysis.DocumentPath) ||
                 analysis.DocumentVersion > 0 ||
                 (analysis.Diagnostics != null && analysis.Diagnostics.Length > 0) ||
                 (analysis.Classifications != null && analysis.Classifications.Length > 0));
        }

        private GUIStyle ResolveStatusModeButtonStyle(bool enabled, bool isEditing)
        {
            if (!enabled)
            {
                return _statusModeButtonDisabledStyle ?? _toolbarButtonStyle ?? GUI.skin.button;
            }

            return isEditing
                ? (_statusModeButtonActiveStyle ?? _toolbarButtonStyle ?? GUI.skin.button)
                : (_statusModeButtonStyle ?? _toolbarButtonStyle ?? GUI.skin.button);
        }

        private void DrawStatusTooltip(Rect anchorRect, string tooltip)
        {
            if (string.IsNullOrEmpty(tooltip) || Event.current == null || anchorRect.width <= 0f || anchorRect.height <= 0f)
            {
                return;
            }

            var tooltipStyle = _statusTooltipStyle ?? _codeTooltipStyle ?? GUI.skin.box;
            var content = new GUIContent(tooltip);
            var size = tooltipStyle.CalcSize(content);
            var width = Mathf.Min(340f, Mathf.Max(220f, size.x + 16f));
            var height = Mathf.Max(22f, tooltipStyle.CalcHeight(content, width - 16f) + 12f);
            var mousePosition = Event.current.mousePosition;
            var tooltipRect = new Rect(mousePosition.x + 14f, anchorRect.y - height - 6f, width, height);
            var screenWidth = Screen.width > 0 ? Screen.width : 1920;

            if (tooltipRect.xMax > screenWidth - 8f)
            {
                tooltipRect.x = screenWidth - width - 8f;
            }

            if (tooltipRect.x < 8f)
            {
                tooltipRect.x = 8f;
            }

            if (tooltipRect.y < 8f)
            {
                tooltipRect.y = anchorRect.yMax + 4f;
            }

            GUI.Box(tooltipRect, tooltip, tooltipStyle);
        }

        private static string BuildCompactPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            var parts = filePath.Replace('\\', '/').Split('/');
            if (parts.Length >= 3)
            {
                return ".../" + parts[parts.Length - 2] + "/" + parts[parts.Length - 1];
            }

            return filePath;
        }

        private void EnsureStyles(CortexShellState state)
        {
            var themeId = state.Settings != null && !string.IsNullOrEmpty(state.Settings.ThemeId)
                ? state.Settings.ThemeId
                : "cortex.vs-dark";

            if (string.Equals(_appliedTheme, themeId, StringComparison.OrdinalIgnoreCase) && _tabStyle != null)
            {
                return;
            }

            _appliedTheme = themeId;

            var textColor = CortexIdeLayout.GetTextColor();
            var mutedColor = CortexIdeLayout.GetMutedTextColor();
            var accentColor = CortexIdeLayout.GetAccentColor();
            var surfaceColor = CortexIdeLayout.GetSurfaceColor();
            var headerColor = CortexIdeLayout.GetHeaderColor();
            var bgColor = CortexIdeLayout.GetBackgroundColor();
            var warningColor = CortexIdeLayout.GetWarningColor();

            _tabBackground = MakeTexture(CortexIdeLayout.Blend(bgColor, surfaceColor, 0.5f));
            _activeTabBackground = MakeTexture(surfaceColor);
            _dirtyTabBackground = MakeTexture(CortexIdeLayout.Blend(surfaceColor, warningColor, 0.12f));

            _tabStyle = new GUIStyle(GUI.skin.button);
            _tabStyle.alignment = TextAnchor.MiddleLeft;
            _tabStyle.fontSize = 11;
            _tabStyle.padding = new RectOffset(8, 24, 2, 2);
            _tabStyle.margin = new RectOffset(0, 1, 0, 0);
            _tabStyle.border = new RectOffset(1, 1, 1, 0);
            GuiStyleUtil.ApplyBackgroundToAllStates(_tabStyle, _tabBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_tabStyle, mutedColor);

            _activeTabStyle = new GUIStyle(_tabStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_activeTabStyle, _activeTabBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_activeTabStyle, textColor);

            _dirtyTabStyle = new GUIStyle(_tabStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_dirtyTabStyle, _dirtyTabBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_dirtyTabStyle, warningColor);

            _gutterBackground = MakeTexture(CortexIdeLayout.Blend(bgColor, headerColor, 0.3f));
            _gutterReadOnlyStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyBackgroundToAllStates(_gutterReadOnlyStyle, _gutterBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_gutterReadOnlyStyle, mutedColor);
            _gutterReadOnlyStyle.alignment = TextAnchor.UpperRight;
            _gutterReadOnlyStyle.padding = new RectOffset(4, 6, 4, 4);
            _gutterReadOnlyStyle.margin = new RectOffset(0, 0, 0, 0);
            _gutterReadOnlyStyle.border = new RectOffset(0, 1, 0, 0);
            _gutterReadOnlyStyle.fontSize = 11;
            _gutterReadOnlyStyle.wordWrap = false;

            _editorBackground = MakeTexture(bgColor);
            if (_monoFont == null)
            {
                try
                {
                    _monoFont = Font.CreateDynamicFontFromOSFont(new[] { "Consolas", "Courier New", "Courier" }, 13);
                }
                catch
                {
                    _monoFont = null;
                }
            }

            if (_monoFont != null)
            {
                _gutterReadOnlyStyle.font = _monoFont;
            }

            _editorReadOnlyStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyBackgroundToAllStates(_editorReadOnlyStyle, _editorBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_editorReadOnlyStyle, textColor);
            _editorReadOnlyStyle.wordWrap = false;
            _editorReadOnlyStyle.richText = true;
            _editorReadOnlyStyle.alignment = TextAnchor.UpperLeft;
            _editorReadOnlyStyle.padding = new RectOffset(8, 8, 4, 4);
            _editorReadOnlyStyle.margin = new RectOffset(0, 0, 0, 0);
            _editorReadOnlyStyle.fontSize = 12;
            _editorReadOnlyStyle.stretchHeight = false;
            if (_monoFont != null)
            {
                _editorReadOnlyStyle.font = _monoFont;
            }

            _codeTooltipStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_codeTooltipStyle, MakeTexture(CortexIdeLayout.Blend(headerColor, bgColor, 0.22f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_codeTooltipStyle, textColor);
            _codeTooltipStyle.alignment = TextAnchor.UpperLeft;
            _codeTooltipStyle.wordWrap = true;
            _codeTooltipStyle.richText = false;
            _codeTooltipStyle.padding = new RectOffset(8, 8, 8, 8);
            _codeTooltipStyle.margin = new RectOffset(0, 0, 0, 0);
            _codeTooltipStyle.border = new RectOffset(1, 1, 1, 1);

            _pathBarBackground = MakeTexture(CortexIdeLayout.Blend(headerColor, bgColor, 0.3f));
            _pathBarStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_pathBarStyle, _pathBarBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_pathBarStyle, mutedColor);
            _pathBarStyle.padding = new RectOffset(8, 4, 2, 2);
            _pathBarStyle.margin = new RectOffset(0, 0, 0, 0);

            _statusBackground = MakeTexture(CortexIdeLayout.Blend(accentColor, bgColor, 0.15f));
            _statusBarStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_statusBarStyle, _statusBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_statusBarStyle, mutedColor);
            _statusBarStyle.padding = new RectOffset(8, 8, 2, 2);
            _statusBarStyle.margin = new RectOffset(0, 0, 0, 0);
            _statusBarStyle.fontSize = 10;

            _toolbarBackground = MakeTexture(CortexIdeLayout.Blend(surfaceColor, headerColor, 0.5f));
            _toolbarButtonStyle = new GUIStyle(GUI.skin.button);
            GuiStyleUtil.ApplyBackgroundToAllStates(_toolbarButtonStyle, _toolbarBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_toolbarButtonStyle, textColor);
            _toolbarButtonStyle.padding = new RectOffset(6, 6, 2, 2);
            _toolbarButtonStyle.margin = new RectOffset(2, 0, 0, 0);
            _toolbarButtonStyle.fontSize = 11;

            _contextMenuStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_contextMenuStyle, MakeTexture(CortexIdeLayout.Blend(surfaceColor, headerColor, 0.55f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_contextMenuStyle, textColor);
            _contextMenuStyle.padding = new RectOffset(6, 6, 6, 6);
            _contextMenuStyle.margin = new RectOffset(0, 0, 0, 0);
            _contextMenuStyle.border = new RectOffset(1, 1, 1, 1);

            _contextMenuButtonStyle = new GUIStyle(_toolbarButtonStyle);
            _contextMenuButtonStyle.alignment = TextAnchor.MiddleLeft;
            _contextMenuButtonStyle.padding = new RectOffset(8, 8, 3, 3);

            _contextMenuHeaderStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyTextColorToAllStates(_contextMenuHeaderStyle, CortexIdeLayout.GetAccentColor());
            _contextMenuHeaderStyle.fontStyle = FontStyle.Bold;
            _contextMenuHeaderStyle.wordWrap = false;

            _statusModeBackground = MakeTexture(CortexIdeLayout.Blend(surfaceColor, headerColor, 0.5f));
            _statusModeActiveBackground = MakeTexture(CortexIdeLayout.Blend(accentColor, surfaceColor, 0.2f));
            _statusModeDisabledBackground = MakeTexture(CortexIdeLayout.Blend(bgColor, surfaceColor, 0.45f));

            _statusModeButtonStyle = new GUIStyle(_toolbarButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_statusModeButtonStyle, _statusModeBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_statusModeButtonStyle, textColor);
            _statusModeButtonStyle.alignment = TextAnchor.MiddleCenter;
            _statusModeButtonStyle.fontStyle = FontStyle.Bold;
            _statusModeButtonStyle.margin = new RectOffset(0, 0, 0, 0);

            _statusModeButtonActiveStyle = new GUIStyle(_statusModeButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_statusModeButtonActiveStyle, _statusModeActiveBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_statusModeButtonActiveStyle, accentColor);

            _statusModeButtonDisabledStyle = new GUIStyle(_statusModeButtonStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_statusModeButtonDisabledStyle, _statusModeDisabledBackground);
            GuiStyleUtil.ApplyTextColorToAllStates(_statusModeButtonDisabledStyle, CortexIdeLayout.WithAlpha(mutedColor, 0.72f));

            _statusTooltipStyle = new GUIStyle(_codeTooltipStyle);
            _statusTooltipStyle.wordWrap = true;
            _statusTooltipStyle.alignment = TextAnchor.UpperLeft;

            _tabCloseButtonStyle = new GUIStyle(GUI.skin.button);
            _tabCloseButtonStyle.alignment = TextAnchor.MiddleCenter;
            _tabCloseButtonStyle.fontSize = 10;
            _tabCloseButtonStyle.padding = new RectOffset(0, 0, 0, 0);
            _tabCloseButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyBackgroundToAllStates(_tabCloseButtonStyle, MakeTexture(CortexIdeLayout.Blend(headerColor, bgColor, 0.45f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_tabCloseButtonStyle, textColor);

            _emptyStateStyle = new GUIStyle(GUI.skin.label);
            _emptyStateStyle.alignment = TextAnchor.MiddleCenter;
            _emptyStateStyle.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_emptyStateStyle, mutedColor);
            _emptyStateStyle.fontSize = 12;

            _codeViewSurface.Invalidate();
            _editableCodeViewSurface.Invalidate();
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
