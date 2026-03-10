using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
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
        private const float TabHeight = 26f;
        private const float TabWidth = 160f;
        private const float TabStripHeight = 28f;
        private const float PathBarHeight = 22f;
        private const float StatusBarHeight = 20f;
        private const float GutterWidth = 52f;

        private Vector2 _tabScroll = Vector2.zero;
        private Vector2 _editorScroll = Vector2.zero;
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
        private bool _statusModeMenuOpen;
        private readonly CodeViewSurface _codeViewSurface = new CodeViewSurface();
        private readonly EditableCodeViewSurface _editableCodeViewSurface = new EditableCodeViewSurface();
        private readonly IEditorService _editorService = new EditorService();

        public void Draw(
            IDocumentService documentService,
            Services.CortexNavigationService navigationService,
            CortexShellState state)
        {
            EnsureStyles(state);

            GUILayout.BeginVertical(GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));

            if (state.Documents.OpenDocuments.Count == 0 || state.Documents.ActiveDocument == null)
            {
                DrawEmptyState();
                GUILayout.EndVertical();
                return;
            }

            DrawTabStrip(state);
            DrawPathBar(documentService, state);
            DrawCodeArea(documentService, navigationService, state);
            DrawStatusBar(state);

            GUILayout.EndVertical();
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
                var closingPath = active.FilePath;
                CortexModuleUtil.CloseDocument(state, closingPath);
                GUILayout.EndHorizontal();
                return;
            }

            GUILayout.EndHorizontal();
        }

        private void DrawCodeArea(IDocumentService documentService, Services.CortexNavigationService navigationService, CortexShellState state)
        {
            var active = state.Documents.ActiveDocument;
            if (active == null)
            {
                return;
            }

            documentService.HasExternalChanges(active);
            var editingAllowed = state.Settings != null && state.Settings.EnableFileEditing;
            var isEditable = editingAllowed && state.Documents.EditorUnlocked && active.SupportsEditing;
            var rect = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));

            if (isEditable)
            {
                _editorScroll = _editableCodeViewSurface.Draw(
                    rect,
                    _editorScroll,
                    active,
                    state,
                    _appliedTheme,
                    _editorReadOnlyStyle,
                    _gutterReadOnlyStyle,
                    GutterWidth);
                return;
            }

            _editorScroll = _codeViewSurface.Draw(
                rect,
                _editorScroll,
                active,
                navigationService,
                state,
                _appliedTheme,
                _editorReadOnlyStyle,
                _gutterReadOnlyStyle,
                _codeTooltipStyle,
                _contextMenuStyle,
                _contextMenuButtonStyle,
                _contextMenuHeaderStyle,
                GutterWidth);
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
            var canEditDocument = active.SupportsEditing;
            var isEditing = state.Documents.EditorUnlocked && canEditDocument;
            var editingAllowed = state.Settings != null && state.Settings.EnableFileEditing;
            var savingAllowed = state.Settings != null && state.Settings.EnableFileSaving;
            var analysis = active.LanguageAnalysis;
            var errorCount = CountDiagnostics(analysis, "Error");
            var warningCount = CountDiagnostics(analysis, "Warning");
            var roslynLabel = BuildRoslynLabel(state, analysis, errorCount, warningCount);
            var modeTooltip = "Choose the editor mode for the active tab.";
            var modeContent = new GUIContent((isEditing ? "EDIT" : "READ") + " v", modeTooltip);
            var tooltip = string.Empty;
            Rect modeRect;

            GUILayout.BeginHorizontal(_statusBarStyle ?? GUI.skin.box, GUILayout.Height(StatusBarHeight));
            if (GUILayout.Button(modeContent, ResolveStatusModeButtonStyle(true, isEditing), GUILayout.Width(68f)))
            {
                _statusModeMenuOpen = !_statusModeMenuOpen;
            }

            modeRect = GUILayoutUtility.GetLastRect();
            tooltip = !_statusModeMenuOpen && Event.current != null && modeRect.Contains(Event.current.mousePosition) ? modeContent.tooltip : string.Empty;

            if (active.IsDirty)
            {
                GUILayout.Label("*", GUILayout.Width(10f));
            }

            GUILayout.Label("Ln " + (caret.Line + 1) + ", Col " + (caret.Column + 1) + "   " + lineCount + " lines", GUILayout.ExpandWidth(false));
            GUILayout.Space(10f);
            GUILayout.Label(roslynLabel, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();

            GUI.enabled = savingAllowed && active.SupportsSaving;
            if (GUILayout.Button("Save All", _toolbarButtonStyle ?? GUI.skin.button, GUILayout.Width(70f)))
            {
                state.StatusMessage = "Save All requested.";
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            DrawStatusModeMenu(state, active, modeRect, editingAllowed, canEditDocument, isEditing);
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

            if (!analysis.Success)
            {
                return "Roslyn: " + (string.IsNullOrEmpty(analysis.StatusMessage) ? "analysis failed" : analysis.StatusMessage);
            }

            return "Roslyn E:" + errorCount + " W:" + warningCount;
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

        private void DrawStatusModeMenu(CortexShellState state, DocumentSession session, Rect anchorRect, bool editingAllowed, bool canEditDocument, bool isEditing)
        {
            if (!_statusModeMenuOpen || Event.current == null || anchorRect.width <= 0f || anchorRect.height <= 0f)
            {
                return;
            }

            var current = Event.current;
            var menuHeight = 78f;
            var menuY = anchorRect.y - menuHeight - 2f;
            if (menuY < 8f)
            {
                menuY = anchorRect.yMax + 2f;
            }

            var menuRect = new Rect(anchorRect.x, menuY, 152f, menuHeight);
            var readRect = new Rect(menuRect.x + 6f, menuRect.y + 24f, menuRect.width - 12f, 22f);
            var editRect = new Rect(menuRect.x + 6f, menuRect.y + 48f, menuRect.width - 12f, 22f);

            if (current.type == EventType.MouseDown &&
                current.button == 0 &&
                !menuRect.Contains(current.mousePosition) &&
                !anchorRect.Contains(current.mousePosition))
            {
                _statusModeMenuOpen = false;
                return;
            }

            GUI.Box(menuRect, GUIContent.none, _contextMenuStyle ?? GUI.skin.box);
            GUI.Label(new Rect(menuRect.x + 8f, menuRect.y + 5f, menuRect.width - 16f, 18f), "Editor Mode", _contextMenuHeaderStyle ?? GUI.skin.label);

            if (DrawStatusModeMenuItem(readRect, "READ", !isEditing, true))
            {
                state.Documents.EditorUnlocked = false;
                _statusModeMenuOpen = false;
                EditorInteractionLog.WriteEdit("Switched active tab to read-only mode.");
                current.Use();
                return;
            }

            var editEnabled = editingAllowed && canEditDocument;
            if (DrawStatusModeMenuItem(editRect, "EDIT", isEditing, editEnabled))
            {
                state.Documents.EditorUnlocked = true;
                _statusModeMenuOpen = false;
                EditorInteractionLog.WriteEdit("Unlocked active tab for in-memory editing.");
                current.Use();
                return;
            }

            if (editRect.Contains(current.mousePosition))
            {
                DrawStatusTooltip(editRect, BuildEditModeTooltip(session, editingAllowed, canEditDocument, isEditing));
            }
        }

        private bool DrawStatusModeMenuItem(Rect rect, string label, bool selected, bool enabled)
        {
            var style = ResolveStatusModeButtonStyle(enabled, selected);
            if (!enabled)
            {
                GUI.Box(rect, label, style);
                return false;
            }

            return GUI.Button(rect, label, style);
        }

        private static string BuildEditModeTooltip(DocumentSession session, bool editingAllowed, bool canEditDocument, bool isEditing)
        {
            if (!editingAllowed)
            {
                return "Enable File Editing in Settings to allow source tabs to switch into edit mode.";
            }

            if (!canEditDocument)
            {
                return session != null && session.Kind == DocumentKind.DecompiledCode
                    ? "Decompiler output is read-only. Open the source file instead to edit code."
                    : "This document type is read-only and cannot switch into edit mode.";
            }

            return isEditing
                ? "Edit mode is already active for this tab."
                : "Switch this source tab into in-memory edit mode.";
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
