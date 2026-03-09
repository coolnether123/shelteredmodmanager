using System;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    /// <summary>
    /// Pure code-editor module. Responsible only for rendering the tab strip,
    /// the line-number gutter, and the editable content area for the active
    /// <see cref="DocumentSession"/>. File navigation is handled separately by
    /// <see cref="FileExplorer.FileExplorerModule"/> (Single Responsibility Principle).
    /// </summary>
    public sealed class EditorModule
    {
        // ── layout state ──────────────────────────────────────────────────────────────
        private Vector2 _tabScroll = Vector2.zero;
        private Vector2 _editorScroll = Vector2.zero;

        // ── styles (created lazily, invalidated on theme change) ──────────────────────
        private string _appliedTheme = string.Empty;
        private GUIStyle _tabStyle;
        private GUIStyle _activeTabStyle;
        private GUIStyle _dirtyTabStyle;
        private GUIStyle _gutterStyle;
        private GUIStyle _gutterReadOnlyStyle;
        private GUIStyle _editorAreaStyle;
        private GUIStyle _editorReadOnlyStyle;
        private GUIStyle _pathBarStyle;
        private GUIStyle _statusBarStyle;
        private GUIStyle _emptyStateStyle;
        private GUIStyle _toolbarButtonStyle;
        private GUIStyle _editingToggleStyle;
        private GUIStyle _tabCloseButtonStyle;
        private GUIStyle _codeTooltipStyle;
        private GUIStyle _contextMenuStyle;
        private GUIStyle _contextMenuButtonStyle;
        private GUIStyle _contextMenuHeaderStyle;
        private Font _monoFont;
        private Texture2D _tabBg;
        private Texture2D _activeTabBg;
        private Texture2D _dirtyTabBg;
        private Texture2D _gutterBg;
        private Texture2D _editorBg;
        private Texture2D _pathBarBg;
        private Texture2D _statusBg;
        private Texture2D _toolbarBg;
        private readonly CodeViewSurface _codeViewSurface = new CodeViewSurface();

        // ── constants ─────────────────────────────────────────────────────────────────
        private const float TabHeight = 26f;
        private const float TabWidth = 160f;
        private const float TabStripHeight = 28f;
        private const float PathBarHeight = 22f;
        private const float StatusBarHeight = 20f;
        private const float GutterWidth = 52f;

        public void Draw(
            IDocumentService documentService,
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
            DrawCodeArea(documentService, state);
            DrawStatusBar(state);

            GUILayout.EndVertical();
        }

        // ── Empty state ───────────────────────────────────────────────────────────────

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

        // ── Tab strip ─────────────────────────────────────────────────────────────────

        private void DrawTabStrip(CortexShellState state)
        {
            _tabScroll = GUILayout.BeginScrollView(
                _tabScroll,
                false, false,
                GUIStyle.none, GUIStyle.none,
                GUIStyle.none,
                GUILayout.Height(TabStripHeight));

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));

            for (var i = 0; i < state.Documents.OpenDocuments.Count; i++)
            {
                var session = state.Documents.OpenDocuments[i];
                var isActive = session == state.Documents.ActiveDocument;
                var displayName = CortexModuleUtil.GetDocumentDisplayName(session);
                var isDirty = session.IsDirty;

                var style = isActive ? _activeTabStyle : (isDirty ? _dirtyTabStyle : _tabStyle);
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

                if (GUI.Toggle(tabRect, isActive, displayName, style ?? GUI.skin.button))
                {
                    if (!isActive)
                    {
                        state.Documents.ActiveDocument = session;
                        state.Documents.ActiveDocumentPath = session.FilePath;
                    }
                }

                if (isHovered || isActive)
                {
                    GUI.Box(closeRect, "x", _tabCloseButtonStyle ?? GUI.skin.button);
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

        // ── Path bar (breadcrumb + action buttons) ────────────────────────────────────

        private void DrawPathBar(IDocumentService documentService, CortexShellState state)
        {
            var active = state.Documents.ActiveDocument;
            if (active == null)
            {
                return;
            }

            var allowSaving = state.Settings != null && state.Settings.EnableFileSaving;

            GUILayout.BeginHorizontal(_pathBarStyle ?? GUI.skin.box, GUILayout.Height(PathBarHeight));

            // Compact path display
            var displayPath = BuildCompactPath(active.FilePath);
            GUILayout.Label(displayPath, GUILayout.ExpandWidth(true));

            if (active.HighlightedLine > 0)
            {
                GUILayout.Label("Ln " + active.HighlightedLine, GUILayout.Width(62f));
            }

            if (active.HasExternalChanges)
            {
                GUILayout.Label("⚠ External change", GUILayout.Width(120f));
            }

            // Action buttons - compact, VS-style labels
            var saveButtonsEnabled = GUI.enabled;
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
            if (GUILayout.Button("Reload", _toolbarButtonStyle ?? GUI.skin.button, GUILayout.Width(56f)))
            {
                documentService.Reload(active);
                state.StatusMessage = "Reloaded from disk.";
            }
            GUI.enabled = saveButtonsEnabled;
            if (GUILayout.Button("✕", _toolbarButtonStyle ?? GUI.skin.button, GUILayout.Width(26f)))
            {
                var closingPath = active.FilePath;
                CortexModuleUtil.CloseDocument(state, closingPath);
                GUILayout.EndHorizontal();
                return;
            }

            GUILayout.EndHorizontal();
        }

        // ── Code editor area ──────────────────────────────────────────────────────────

        private void DrawCodeArea(IDocumentService documentService, CortexShellState state)
        {
            var active = state.Documents.ActiveDocument;
            if (active == null)
            {
                return;
            }

            documentService.HasExternalChanges(active);
            var editingAllowed = state.Settings != null && state.Settings.EnableFileEditing;
            var isEditable = editingAllowed && state.Documents.EditorUnlocked;

            if (isEditable)
            {
                _editorScroll = GUILayout.BeginScrollView(
                    _editorScroll,
                    false, true,
                    GUILayout.ExpandHeight(true),
                    GUILayout.ExpandWidth(true));

                GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
                GUILayout.TextArea(
                    BuildLineGutter(active),
                    _gutterStyle ?? GUI.skin.textArea,
                    GUILayout.Width(GutterWidth),
                    GUILayout.ExpandHeight(true));

                var previousEnabled = GUI.enabled;
                GUI.enabled = true;
                var updated = GUILayout.TextArea(
                    active.Text ?? string.Empty,
                    _editorAreaStyle ?? GUI.skin.textArea,
                    GUILayout.ExpandHeight(true),
                    GUILayout.ExpandWidth(true));
                GUI.enabled = previousEnabled;

                if (!string.Equals(updated, active.Text, StringComparison.Ordinal))
                {
                    active.Text = updated;
                    active.IsDirty = true;
                    _codeViewSurface.Invalidate();
                }

                GUILayout.EndHorizontal();
                GUILayout.EndScrollView();
            }
            else
            {
                var rect = GUILayoutUtility.GetRect(0f, 100000f, 0f, 100000f, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
                _editorScroll = _codeViewSurface.Draw(
                    rect,
                    _editorScroll,
                    active,
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
        }

        // ── Status bar ────────────────────────────────────────────────────────────────

        private void DrawStatusBar(CortexShellState state)
        {
            var active = state.Documents.ActiveDocument;
            if (active == null)
            {
                return;
            }

            var lineCount = CortexModuleUtil.SplitLines(active.Text).Length;
            var editLabel = state.Documents.EditorUnlocked ? "EDIT" : "READ";
            var editingAllowed = state.Settings != null && state.Settings.EnableFileEditing;
            var savingAllowed = state.Settings != null && state.Settings.EnableFileSaving;
            var dirtyLabel = active.IsDirty ? " ●" : string.Empty;
            var analysis = active.LanguageAnalysis;
            var errorCount = CountDiagnostics(analysis, "Error");
            var warningCount = CountDiagnostics(analysis, "Warning");
            var roslynLabel = BuildRoslynLabel(state, analysis, errorCount, warningCount);

            GUILayout.BeginHorizontal(_statusBarStyle ?? GUI.skin.box, GUILayout.Height(StatusBarHeight));
            GUILayout.Label(editLabel + dirtyLabel + "   Ln " + lineCount + " lines", GUILayout.ExpandWidth(false));
            GUILayout.Space(10f);
            GUILayout.Label(roslynLabel, GUILayout.ExpandWidth(false));
            GUILayout.FlexibleSpace();

            // Editing toggle on the right like VS's "Edit" toggle in status bar
            var previousEnabled = GUI.enabled;
            GUI.enabled = editingAllowed;
            state.Documents.EditorUnlocked = GUILayout.Toggle(
                state.Documents.EditorUnlocked,
                state.Documents.EditorUnlocked ? "🔓 Editing" : "🔒 Read Only",
                _editingToggleStyle ?? GUI.skin.toggle,
                GUILayout.Width(156f));

            GUI.enabled = savingAllowed;
            if (GUILayout.Button("Save All", _toolbarButtonStyle ?? GUI.skin.button, GUILayout.Width(70f)))
            {
                // Signal handled by shell — we just flip the dirty flag hint via status
                state.StatusMessage = "Save All requested.";
            }
            GUI.enabled = previousEnabled;
            GUILayout.EndHorizontal();
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

        // ── Helpers ───────────────────────────────────────────────────────────────────

        private static string BuildCompactPath(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                return string.Empty;
            }

            // Show last two path segments for readability
            var parts = filePath.Replace('\\', '/').Split('/');
            if (parts.Length >= 3)
            {
                return "…/" + parts[parts.Length - 2] + "/" + parts[parts.Length - 1];
            }

            return filePath;
        }

        private static string BuildLineGutter(DocumentSession session)
        {
            var lines = CortexModuleUtil.SplitLines(session != null ? session.Text : string.Empty);
            var sb = new StringBuilder(lines.Length * 6);
            for (var i = 0; i < lines.Length; i++)
            {
                var lineNumber = i + 1;
                var isHighlighted = session != null && session.HighlightedLine == lineNumber;
                sb.Append(isHighlighted ? "→ " : "  ");
                sb.Append(lineNumber.ToString("D4"));
                sb.Append('\n');
            }

            return sb.ToString();
        }

        // ── Style management ──────────────────────────────────────────────────────────

        private void EnsureStyles(CortexShellState state)
        {
            var themeId = state.Settings != null && !string.IsNullOrEmpty(state.Settings.ThemeId)
                ? state.Settings.ThemeId
                : "cortex.vs-dark";

            if (string.Equals(_appliedTheme, themeId, StringComparison.OrdinalIgnoreCase) &&
                _tabStyle != null)
            {
                return;
            }

            _appliedTheme = themeId;

            var textColor = CortexIdeLayout.GetTextColor();
            var mutedColor = CortexIdeLayout.GetMutedTextColor();
            var accentColor = CortexIdeLayout.GetAccentColor();
            var surfaceColor = CortexIdeLayout.GetSurfaceColor();
            var headerColor = CortexIdeLayout.GetHeaderColor();
            var borderColor = CortexIdeLayout.GetBorderColor();
            var bgColor = CortexIdeLayout.GetBackgroundColor();
            var warningColor = CortexIdeLayout.GetWarningColor();

            // Tabs
            _tabBg = MakeTex(CortexIdeLayout.Blend(bgColor, surfaceColor, 0.5f));
            _activeTabBg = MakeTex(surfaceColor);
            _dirtyTabBg = MakeTex(CortexIdeLayout.Blend(surfaceColor, warningColor, 0.12f));

            _tabStyle = new GUIStyle(GUI.skin.button);
            _tabStyle.alignment = TextAnchor.MiddleLeft;
            _tabStyle.fontSize = 11;
            _tabStyle.padding = new RectOffset(8, 24, 2, 2);
            _tabStyle.margin = new RectOffset(0, 1, 0, 0);
            _tabStyle.border = new RectOffset(1, 1, 1, 0);
            GuiStyleUtil.ApplyBackgroundToAllStates(_tabStyle, _tabBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_tabStyle, mutedColor);

            _activeTabStyle = new GUIStyle(_tabStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_activeTabStyle, _activeTabBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_activeTabStyle, textColor);

            _dirtyTabStyle = new GUIStyle(_tabStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_dirtyTabStyle, _dirtyTabBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_dirtyTabStyle, warningColor);

            // Gutter
            _gutterBg = MakeTex(CortexIdeLayout.Blend(bgColor, headerColor, 0.3f));
            _gutterStyle = new GUIStyle(GUI.skin.textArea);
            GuiStyleUtil.ApplyBackgroundToAllStates(_gutterStyle, _gutterBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_gutterStyle, mutedColor);
            _gutterStyle.alignment = TextAnchor.UpperRight;
            _gutterStyle.padding = new RectOffset(4, 6, 4, 4);
            _gutterStyle.margin = new RectOffset(0, 0, 0, 0);
            _gutterStyle.border = new RectOffset(0, 1, 0, 0);
            _gutterStyle.fontSize = 11;
            _gutterReadOnlyStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyBackgroundToAllStates(_gutterReadOnlyStyle, _gutterBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_gutterReadOnlyStyle, mutedColor);
            _gutterReadOnlyStyle.alignment = TextAnchor.UpperRight;
            _gutterReadOnlyStyle.padding = new RectOffset(4, 6, 4, 4);
            _gutterReadOnlyStyle.margin = new RectOffset(0, 0, 0, 0);
            _gutterReadOnlyStyle.border = new RectOffset(0, 1, 0, 0);
            _gutterReadOnlyStyle.fontSize = 11;
            _gutterReadOnlyStyle.wordWrap = false;

            // Editor area
            _editorBg = MakeTex(bgColor);
            _editorAreaStyle = new GUIStyle(GUI.skin.textArea);
            GuiStyleUtil.ApplyBackgroundToAllStates(_editorAreaStyle, _editorBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_editorAreaStyle, textColor);
            _editorAreaStyle.wordWrap = false;
            _editorAreaStyle.padding = new RectOffset(8, 8, 4, 4);
            _editorAreaStyle.margin = new RectOffset(0, 0, 0, 0);
            _editorAreaStyle.fontSize = 12;
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
                _gutterStyle.font = _monoFont;
                _editorAreaStyle.font = _monoFont;
            }

            _editorReadOnlyStyle = new GUIStyle(GUI.skin.label);
            GuiStyleUtil.ApplyBackgroundToAllStates(_editorReadOnlyStyle, _editorBg);
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
                _gutterReadOnlyStyle.font = _monoFont;
                _editorReadOnlyStyle.font = _monoFont;
            }

            _codeTooltipStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_codeTooltipStyle, MakeTex(CortexIdeLayout.Blend(headerColor, bgColor, 0.22f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_codeTooltipStyle, textColor);
            _codeTooltipStyle.alignment = TextAnchor.UpperLeft;
            _codeTooltipStyle.wordWrap = true;
            _codeTooltipStyle.richText = false;
            _codeTooltipStyle.padding = new RectOffset(8, 8, 8, 8);
            _codeTooltipStyle.margin = new RectOffset(0, 0, 0, 0);
            _codeTooltipStyle.border = new RectOffset(1, 1, 1, 1);

            // Path bar
            _pathBarBg = MakeTex(CortexIdeLayout.Blend(headerColor, bgColor, 0.3f));
            _pathBarStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_pathBarStyle, _pathBarBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_pathBarStyle, mutedColor);
            _pathBarStyle.padding = new RectOffset(8, 4, 2, 2);
            _pathBarStyle.margin = new RectOffset(0, 0, 0, 0);

            // Status bar
            _statusBg = MakeTex(CortexIdeLayout.Blend(accentColor, bgColor, 0.15f));
            _statusBarStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_statusBarStyle, _statusBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_statusBarStyle, mutedColor);
            _statusBarStyle.padding = new RectOffset(8, 8, 2, 2);
            _statusBarStyle.margin = new RectOffset(0, 0, 0, 0);
            _statusBarStyle.fontSize = 10;

            // Toolbar button (compact)
            _toolbarBg = MakeTex(CortexIdeLayout.Blend(surfaceColor, headerColor, 0.5f));
            _toolbarButtonStyle = new GUIStyle(GUI.skin.button);
            GuiStyleUtil.ApplyBackgroundToAllStates(_toolbarButtonStyle, _toolbarBg);
            GuiStyleUtil.ApplyTextColorToAllStates(_toolbarButtonStyle, textColor);
            _toolbarButtonStyle.padding = new RectOffset(6, 6, 2, 2);
            _toolbarButtonStyle.margin = new RectOffset(2, 0, 0, 0);
            _toolbarButtonStyle.fontSize = 11;

            _contextMenuStyle = new GUIStyle(GUI.skin.box);
            GuiStyleUtil.ApplyBackgroundToAllStates(_contextMenuStyle, MakeTex(CortexIdeLayout.Blend(surfaceColor, headerColor, 0.55f)));
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

            // Editing toggle
            _editingToggleStyle = new GUIStyle(GUI.skin.toggle);
            GuiStyleUtil.ApplyTextColorToAllStates(_editingToggleStyle, mutedColor);
            _editingToggleStyle.fontSize = 10;
            _editingToggleStyle.padding = new RectOffset(18, 4, 2, 2);

            _tabCloseButtonStyle = new GUIStyle(GUI.skin.button);
            _tabCloseButtonStyle.alignment = TextAnchor.MiddleCenter;
            _tabCloseButtonStyle.fontSize = 10;
            _tabCloseButtonStyle.padding = new RectOffset(0, 0, 0, 0);
            _tabCloseButtonStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyBackgroundToAllStates(_tabCloseButtonStyle, MakeTex(CortexIdeLayout.Blend(headerColor, bgColor, 0.45f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_tabCloseButtonStyle, textColor);

            // Empty state
            _emptyStateStyle = new GUIStyle(GUI.skin.label);
            _emptyStateStyle.alignment = TextAnchor.MiddleCenter;
            _emptyStateStyle.wordWrap = true;
            GuiStyleUtil.ApplyTextColorToAllStates(_emptyStateStyle, mutedColor);
            _emptyStateStyle.fontSize = 12;
            _codeViewSurface.Invalidate();
        }

        private static Texture2D MakeTex(Color color)
        {
            var tex = new Texture2D(1, 1);
            tex.SetPixel(0, 0, color);
            tex.Apply();
            return tex;
        }
    }
}
