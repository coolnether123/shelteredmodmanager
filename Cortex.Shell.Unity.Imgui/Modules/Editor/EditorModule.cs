using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Modules.Shared;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Context;
using Cortex.Services.Semantics.Hover;
using UnityEngine;
using Cortex.Services.Editor.Context;
using Cortex.Services.Editor.Commands;
using Cortex.Services.Editor.Presentation;
using Cortex.Services.Search;
using Cortex.Shell.Unity.Imgui.Services.Editor.Commands;
using Cortex.Shell.Unity.Imgui;

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
        // This Unity runtime target does not expose Rect.zero, so keep an explicit
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
        private readonly CodeViewSurface _codeViewSurface;
        private readonly EditableCodeViewSurface _editableCodeViewSurface;
        private readonly IEditorHoverService _hoverService;
        private readonly IEditorService _editorService = new EditorService();
        private readonly EditorDocumentModeService _documentModeService = new EditorDocumentModeService();
        private readonly EditorPresentationService _presentationService;
        private readonly IRenderPipeline _renderPipeline;
        private readonly IClipboardService _clipboardService;

        internal EditorModule(IEditorContextService editorContextService, IRenderPipeline renderPipeline)
        {
            _renderPipeline = renderPipeline;
            _hoverService = new EditorHoverService(editorContextService);
            _presentationService = new EditorPresentationService(_editorService, _documentModeService);
            _clipboardService = new ImguiClipboardService();
            var overlayRendererFactory = renderPipeline != null ? renderPipeline.OverlayRendererFactory : null;
            _codeViewSurface = new CodeViewSurface(editorContextService, _hoverService, overlayRendererFactory);
            _editableCodeViewSurface = new EditableCodeViewSurface(editorContextService, _hoverService, overlayRendererFactory);
        }

        internal void Draw(
            IDocumentService documentService,
            ICortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            WorkbenchSearchService workbenchSearchService,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            IEditorContributionRuntime extensionRuntime,
            IRenderPipeline renderPipeline,
            CortexShellState state)
        {
            EditorCommandContributions.EnsureRegistered(commandRegistry, contributionRegistry, state, _clipboardService);
            EnsureStyles(state);
            ApplyPendingHoverVisualRefresh(state);
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
                extensionRuntime,
                renderPipeline ?? _renderPipeline,
                state);
            DrawFindOverlay(commandRegistry, workbenchSearchService, state);
            DrawStatusBar(state);

            GUILayout.EndVertical();
        }

        private void ApplyPendingHoverVisualRefresh(CortexShellState state)
        {
            var plan = _presentationService.BuildPendingHoverRefreshPlan(state, DateTime.UtcNow);
            if (!plan.ShouldInvalidateSurfaces)
            {
                return;
            }

            _codeViewSurface.Invalidate();
            _editableCodeViewSurface.Invalidate();
            GUI.changed = true;
        }

        private void HandleSearchShortcuts(ICommandRegistry commandRegistry, CortexShellState state)
        {
            var current = Event.current;
            if (current == null || current.type != EventType.KeyDown || commandRegistry == null)
            {
                return;
            }

            var commandId = _presentationService.ResolveSearchShortcutCommand(
                new EditorSearchShortcutInput
                {
                    Control = current.control,
                    Alt = current.alt,
                    Shift = current.shift,
                    KeyCode = current.keyCode.ToString(),
                    HasFocusedControl = GUIUtility.keyboardControl != 0,
                    FocusedControlName = GUI.GetNameOfFocusedControl() ?? string.Empty
                },
                state);
            if (string.IsNullOrEmpty(commandId))
            {
                return;
            }

            ExecuteWorkbenchCommand(commandRegistry, commandId, state);
            current.Use();
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
            var tabs = _presentationService.BuildTabStripPresentation(state);
            _tabScroll = GUILayout.BeginScrollView(
                _tabScroll,
                false,
                false,
                GUIStyle.none,
                GUIStyle.none,
                GUIStyle.none,
                GUILayout.Height(TabStripHeight));

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            for (var i = 0; i < tabs.Count; i++)
            {
                var tab = tabs[i];
                var session = tab.Session;
                var isActive = tab.IsActive;
                var style = isActive ? _activeTabStyle : (tab.IsDirty ? _dirtyTabStyle : _tabStyle);
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

                if (GUI.Toggle(tabRect, isActive, tab.DisplayName, style ?? GUI.skin.button) && !isActive)
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

            var presentation = _presentationService.BuildPathBarPresentation(documentService, state);
            var closeRequested = false;

            GUILayout.BeginHorizontal(_pathBarStyle ?? GUI.skin.box, GUILayout.Height(PathBarHeight));

            GUILayout.Label(presentation.CompactPath, GUILayout.ExpandWidth(true));

            if (presentation.HasHighlightedLine)
            {
                GUILayout.Label("Ln " + presentation.HighlightedLine, GUILayout.Width(62f));
            }

            if (presentation.HasExternalChanges)
            {
                GUILayout.Label("External change", GUILayout.Width(120f));
            }

            var previousEnabled = GUI.enabled;

            GUI.enabled = presentation.AllowSaving;
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

            GUI.enabled = presentation.CanReload;
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
            _presentationService.StabilizeActiveDocument(documentService, state);
        }

        private void DrawFindOverlay(ICommandRegistry commandRegistry, WorkbenchSearchService workbenchSearchService, CortexShellState state)
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
            var presentation = _presentationService.BuildFindOverlayPresentation(workbenchSearchService, state);
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

            GUI.Label(summaryRect, presentation.SummaryText);

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

            if (GUI.Button(scopeRect, presentation.ScopeLabel + " v", _toolbarButtonStyle ?? GUI.skin.button))
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
            ICortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            IEditorContributionRuntime extensionRuntime,
            IRenderPipeline renderPipeline,
            CortexShellState state)
        {
            var active = state.Documents.ActiveDocument;
            if (active == null)
            {
                // This Unity runtime target does not expose Rect.zero.
                _lastCodeAreaRect = new Rect(0f, 0f, 0f, 0f);
                return;
            }

            var presentation = _presentationService.BuildCodeAreaPresentation(state);
            var rect = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
            // IMGUI layout passes can report a placeholder 1x1 rect before the real
            // repaint bounds exist. Keep the last usable editor viewport so the find
            // overlay has stable coordinates across layout/repaint pairs.
            if (IsUsableCodeAreaRect(rect))
            {
                _lastCodeAreaRect = rect;
            }

            var overlayBlockRect = BuildActiveFindInputBlockRect(state, rect);

            if (presentation.UsesUnifiedSourceSurface)
            {
                var previousEditorScroll = _editorScroll;
                _editorScroll = _editableCodeViewSurface.Draw(
                    rect,
                    _editorScroll,
                    active,
                    presentation.IsEditingEnabled,
                    new EditorSurfaceServices
                    {
                        DocumentService = documentService,
                        NavigationService = navigationService,
                        CommandRegistry = commandRegistry,
                        ContributionRegistry = contributionRegistry,
                        State = state,
                        ProjectCatalog = projectCatalog,
                        LoadedModCatalog = loadedModCatalog,
                        SourceLookupIndex = sourceLookupIndex,
                        ExtensionRuntime = extensionRuntime
                    },
                    new EditorSurfaceRenderContext
                    {
                        ThemeKey = _appliedTheme,
                        CodeStyle = _editorReadOnlyStyle,
                        GutterStyle = _gutterReadOnlyStyle,
                        PanelRenderer = renderPipeline != null ? renderPipeline.PanelRenderer : null,
                        BlockedRect = new RenderRect(overlayBlockRect.x, overlayBlockRect.y, overlayBlockRect.width, overlayBlockRect.height),
                        GutterWidth = GutterWidth,
                        PopupMenuTheme = BuildPopupMenuThemePalette(),
                        HoverTooltipTheme = BuildHoverTooltipThemePalette()
                    });
                LogEditorScrollChange(active, previousEditorScroll, _editorScroll);
                return;
            }

            var previousReadOnlyScroll = _editorScroll;
            _editorScroll = _codeViewSurface.Draw(
                rect,
                _editorScroll,
                active,
                documentService,
                navigationService,
                commandRegistry,
                contributionRegistry,
                state,
                _appliedTheme,
                _editorReadOnlyStyle,
                _gutterReadOnlyStyle,
                overlayBlockRect,
                GutterWidth,
                projectCatalog,
                loadedModCatalog,
                sourceLookupIndex,
                extensionRuntime,
                renderPipeline != null ? renderPipeline.PanelRenderer : null,
                BuildPopupMenuThemePalette(),
                BuildHoverTooltipThemePalette());
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

            var presentation = _presentationService.BuildStatusBarPresentation(state);
            var modeContent = new GUIContent(presentation.IsEditing ? "EDIT" : "READ", presentation.EditModeTooltip);
            var tooltip = string.Empty;
            Rect modeRect;
            var canToggleMode = presentation.CanToggleEditMode;

            GUILayout.BeginHorizontal(_statusBarStyle ?? GUI.skin.box, GUILayout.Height(StatusBarHeight));
            GUI.enabled = canToggleMode;
            if (GUILayout.Button(modeContent, ResolveStatusModeButtonStyle(canToggleMode, presentation.IsEditing), GUILayout.Width(68f)))
            {
                string statusMessage;
                if (_presentationService.TryToggleEditMode(state, out statusMessage))
                {
                    state.StatusMessage = statusMessage;
                    presentation = _presentationService.BuildStatusBarPresentation(state);
                    modeContent = new GUIContent(presentation.IsEditing ? "EDIT" : "READ", presentation.EditModeTooltip);
                }
            }
            GUI.enabled = true;

            modeRect = GUILayoutUtility.GetLastRect();
            tooltip = Event.current != null && modeRect.Contains(Event.current.mousePosition) ? modeContent.tooltip : string.Empty;

            if (presentation.IsDirty)
            {
                GUILayout.Label("*", GUILayout.Width(10f));
            }

            GUILayout.Label("Ln " + (presentation.Line + 1) + ", Col " + (presentation.Column + 1) + "   " + presentation.LineCount + " lines", GUILayout.ExpandWidth(false));
            GUILayout.Space(10f);
            GUILayout.Label(presentation.LanguageStatusLabel, GUILayout.ExpandWidth(false));
            GUILayout.Space(10f);
            GUILayout.Label(presentation.CompletionStatusLabel, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();

            GUI.enabled = presentation.CanSaveAll;
            if (GUILayout.Button("Save All", _toolbarButtonStyle ?? GUI.skin.button, GUILayout.Width(70f)))
            {
                state.StatusMessage = "Save All requested.";
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();
            DrawStatusTooltip(modeRect, tooltip);
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

        private static PopupMenuThemePalette BuildPopupMenuThemePalette()
        {
            return new PopupMenuThemePalette
            {
                BackgroundColor = ToRenderColor(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.55f)),
                BorderColor = ToRenderColor(ImguiWorkbenchLayout.WithAlpha(ImguiWorkbenchLayout.GetAccentColor(), 0.38f)),
                TextColor = ToRenderColor(ImguiWorkbenchLayout.GetTextColor()),
                MutedTextColor = ToRenderColor(ImguiWorkbenchLayout.GetMutedTextColor()),
                AccentColor = ToRenderColor(ImguiWorkbenchLayout.GetAccentColor()),
                HoverFillColor = ToRenderColor(ImguiWorkbenchLayout.WithAlpha(ImguiWorkbenchLayout.GetAccentColor(), 0.18f)),
                PressedFillColor = ToRenderColor(ImguiWorkbenchLayout.WithAlpha(ImguiWorkbenchLayout.GetAccentColor(), 0.28f))
            };
        }

        private static HoverTooltipThemePalette BuildHoverTooltipThemePalette()
        {
            return new HoverTooltipThemePalette
            {
                BackgroundColor = ToRenderColor(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetHeaderColor(), ImguiWorkbenchLayout.GetBackgroundColor(), 0.22f)),
                BorderColor = ToRenderColor(ImguiWorkbenchLayout.WithAlpha(ImguiWorkbenchLayout.GetAccentColor(), 0.38f)),
                TextColor = ToRenderColor(ImguiWorkbenchLayout.GetTextColor()),
                MutedTextColor = ToRenderColor(ImguiWorkbenchLayout.GetMutedTextColor()),
                AccentColor = ToRenderColor(ImguiWorkbenchLayout.GetAccentColor()),
                HoverFillColor = ToRenderColor(ImguiWorkbenchLayout.WithAlpha(ImguiWorkbenchLayout.GetAccentColor(), 0.18f))
            };
        }

        private static RenderColor ToRenderColor(Color color)
        {
            return new RenderColor(color.r, color.g, color.b, color.a);
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

            var textColor = ImguiWorkbenchLayout.GetTextColor();
            var mutedColor = ImguiWorkbenchLayout.GetMutedTextColor();
            var accentColor = ImguiWorkbenchLayout.GetAccentColor();
            var surfaceColor = ImguiWorkbenchLayout.GetSurfaceColor();
            var headerColor = ImguiWorkbenchLayout.GetHeaderColor();
            var bgColor = ImguiWorkbenchLayout.GetBackgroundColor();
            var warningColor = ImguiWorkbenchLayout.GetWarningColor();

            _tabBackground = MakeTexture(ImguiWorkbenchLayout.Blend(bgColor, surfaceColor, 0.5f));
            _activeTabBackground = MakeTexture(surfaceColor);
            _dirtyTabBackground = MakeTexture(ImguiWorkbenchLayout.Blend(surfaceColor, warningColor, 0.12f));

            _tabStyle = new GUIStyle(GUI.skin.button);
            _tabStyle.alignment = TextAnchor.MiddleLeft;
            _tabStyle.fontSize = 11;
            _tabStyle.padding = new RectOffset(8, 24, 2, 2);
            _tabStyle.margin = new RectOffset(0, 1, 0, 0);
            _tabStyle.border = new RectOffset(1, 1, 1, 0);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_tabStyle, _tabBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_tabStyle, mutedColor);

            _activeTabStyle = new GUIStyle(_tabStyle);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_activeTabStyle, _activeTabBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_activeTabStyle, textColor);

            _dirtyTabStyle = new GUIStyle(_tabStyle);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_dirtyTabStyle, _dirtyTabBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_dirtyTabStyle, warningColor);

            _gutterBackground = MakeTexture(ImguiWorkbenchLayout.Blend(bgColor, headerColor, 0.3f));
            _gutterReadOnlyStyle = new GUIStyle(GUI.skin.label);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_gutterReadOnlyStyle, _gutterBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_gutterReadOnlyStyle, mutedColor);
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
            ImguiStyleUtil.ApplyBackgroundToAllStates(_editorReadOnlyStyle, _editorBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_editorReadOnlyStyle, textColor);
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
            ImguiStyleUtil.ApplyBackgroundToAllStates(_codeTooltipStyle, MakeTexture(ImguiWorkbenchLayout.Blend(headerColor, bgColor, 0.22f)));
            ImguiStyleUtil.ApplyTextColorToAllStates(_codeTooltipStyle, textColor);
            _codeTooltipStyle.alignment = TextAnchor.UpperLeft;
            _codeTooltipStyle.wordWrap = true;
            _codeTooltipStyle.richText = false;
            _codeTooltipStyle.padding = new RectOffset(8, 8, 8, 8);
            _codeTooltipStyle.margin = new RectOffset(0, 0, 0, 0);
            _codeTooltipStyle.border = new RectOffset(1, 1, 1, 1);

            _pathBarBackground = MakeTexture(ImguiWorkbenchLayout.Blend(headerColor, bgColor, 0.3f));
            _pathBarStyle = new GUIStyle(GUI.skin.box);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_pathBarStyle, _pathBarBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_pathBarStyle, mutedColor);
            _pathBarStyle.padding = new RectOffset(8, 4, 2, 2);
            _pathBarStyle.margin = new RectOffset(0, 0, 0, 0);

            _statusBackground = MakeTexture(ImguiWorkbenchLayout.Blend(accentColor, bgColor, 0.15f));
            _statusBarStyle = new GUIStyle(GUI.skin.box);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_statusBarStyle, _statusBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_statusBarStyle, mutedColor);
            _statusBarStyle.padding = new RectOffset(8, 8, 2, 2);
            _statusBarStyle.margin = new RectOffset(0, 0, 0, 0);
            _statusBarStyle.fontSize = 10;

            _toolbarBackground = MakeTexture(ImguiWorkbenchLayout.Blend(surfaceColor, headerColor, 0.5f));
            _toolbarButtonStyle = new GUIStyle(GUI.skin.button);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_toolbarButtonStyle, _toolbarBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_toolbarButtonStyle, textColor);
            _toolbarButtonStyle.padding = new RectOffset(6, 6, 2, 2);
            _toolbarButtonStyle.margin = new RectOffset(2, 0, 0, 0);
            _toolbarButtonStyle.fontSize = 11;

            _contextMenuStyle = new GUIStyle(GUI.skin.box);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_contextMenuStyle, MakeTexture(ImguiWorkbenchLayout.Blend(surfaceColor, headerColor, 0.55f)));
            ImguiStyleUtil.ApplyTextColorToAllStates(_contextMenuStyle, textColor);
            _contextMenuStyle.padding = new RectOffset(6, 6, 6, 6);
            _contextMenuStyle.margin = new RectOffset(0, 0, 0, 0);
            _contextMenuStyle.border = new RectOffset(1, 1, 1, 1);

            _contextMenuButtonStyle = new GUIStyle(_toolbarButtonStyle);
            _contextMenuButtonStyle.alignment = TextAnchor.MiddleLeft;
            _contextMenuButtonStyle.padding = new RectOffset(8, 8, 3, 3);

            _contextMenuHeaderStyle = new GUIStyle(GUI.skin.label);
            ImguiStyleUtil.ApplyTextColorToAllStates(_contextMenuHeaderStyle, ImguiWorkbenchLayout.GetAccentColor());
            _contextMenuHeaderStyle.fontStyle = FontStyle.Bold;
            _contextMenuHeaderStyle.wordWrap = false;

            _statusModeBackground = MakeTexture(ImguiWorkbenchLayout.Blend(surfaceColor, headerColor, 0.5f));
            _statusModeActiveBackground = MakeTexture(ImguiWorkbenchLayout.Blend(accentColor, surfaceColor, 0.2f));
            _statusModeDisabledBackground = MakeTexture(ImguiWorkbenchLayout.Blend(bgColor, surfaceColor, 0.45f));

            _statusModeButtonStyle = new GUIStyle(_toolbarButtonStyle);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_statusModeButtonStyle, _statusModeBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_statusModeButtonStyle, textColor);
            _statusModeButtonStyle.alignment = TextAnchor.MiddleCenter;
            _statusModeButtonStyle.fontStyle = FontStyle.Bold;
            _statusModeButtonStyle.margin = new RectOffset(0, 0, 0, 0);

            _statusModeButtonActiveStyle = new GUIStyle(_statusModeButtonStyle);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_statusModeButtonActiveStyle, _statusModeActiveBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_statusModeButtonActiveStyle, accentColor);

            _statusModeButtonDisabledStyle = new GUIStyle(_statusModeButtonStyle);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_statusModeButtonDisabledStyle, _statusModeDisabledBackground);
            ImguiStyleUtil.ApplyTextColorToAllStates(_statusModeButtonDisabledStyle, ImguiWorkbenchLayout.WithAlpha(mutedColor, 0.72f));

            _statusTooltipStyle = new GUIStyle(_codeTooltipStyle);
            _statusTooltipStyle.wordWrap = true;
            _statusTooltipStyle.alignment = TextAnchor.UpperLeft;

            _tabCloseButtonStyle = new GUIStyle(GUI.skin.button);
            _tabCloseButtonStyle.alignment = TextAnchor.MiddleCenter;
            _tabCloseButtonStyle.fontSize = 10;
            _tabCloseButtonStyle.padding = new RectOffset(0, 0, 0, 0);
            _tabCloseButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            ImguiStyleUtil.ApplyBackgroundToAllStates(_tabCloseButtonStyle, MakeTexture(ImguiWorkbenchLayout.Blend(headerColor, bgColor, 0.45f)));
            ImguiStyleUtil.ApplyTextColorToAllStates(_tabCloseButtonStyle, textColor);

            _emptyStateStyle = new GUIStyle(GUI.skin.label);
            _emptyStateStyle.alignment = TextAnchor.MiddleCenter;
            _emptyStateStyle.wordWrap = true;
            ImguiStyleUtil.ApplyTextColorToAllStates(_emptyStateStyle, mutedColor);
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
