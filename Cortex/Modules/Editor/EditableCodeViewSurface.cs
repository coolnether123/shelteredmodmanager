using System;
using System.Collections.Generic;
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
    /// Shared source surface for writable source files.
    /// READ and EDIT both use this view; edit mode only enables mutation-specific behavior.
    /// </summary>
    internal sealed class EditableCodeViewSurface
    {
        private const float DefaultLineHeight = 18f;
        private const float SelectionPadding = 2f;
        private const float TooltipWidth = 420f;
        private const double DoubleClickThresholdSeconds = 0.32d;
        private const int CompletionVisibleItemCount = 8;
        private static readonly Rect EmptyRect = new Rect(0f, 0f, 0f, 0f);

        private readonly IEditorService _editorService = new EditorService();
        private readonly IEditorKeybindingService _keybindingService = new EditorKeybindingService();
        private readonly DocumentLanguageAnalysisService _documentLanguageAnalysisService = new DocumentLanguageAnalysisService();
        private readonly EditorCompletionService _editorCompletionService = new EditorCompletionService();
        private readonly EditorSignatureHelpService _editorSignatureHelpService = new EditorSignatureHelpService();
        private readonly EditorSemanticPopupSurface _semanticPopupSurface = new EditorSemanticPopupSurface();
        private readonly EditorCommandContextFactory _commandContextFactory = new EditorCommandContextFactory();
        private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();
        private readonly EditorContextMenuService _contextMenuService = new EditorContextMenuService();
        private readonly EditorToolbarService _toolbarService = new EditorToolbarService();
        private readonly SourceEditorCommandRouterService _commandRouterService = new SourceEditorCommandRouterService();
        private readonly SourceEditorHoverService _hoverService = new SourceEditorHoverService();
        private readonly EditorClassificationPresentationService _classificationPresentationService = new EditorClassificationPresentationService();
        private readonly PopupMenuSurface _popupMenuSurface = new PopupMenuSurface();
        private readonly GUIContent _measureContent = new GUIContent();
        private readonly List<NormalizedSpan> _orderedSpans = new List<NormalizedSpan>();
        private readonly Dictionary<string, GUIStyle> _classificationStyles = new Dictionary<string, GUIStyle>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PopupMenuItem> _popupMenuItems = new List<PopupMenuItem>();

        private GUIStyle _codeStyle;
        private GUIStyle _gutterStyle;
        private GUIStyle _tooltipContainerStyle;
        private GUIStyle _tooltipTitleStyle;
        private GUIStyle _tooltipQualifiedStyle;
        private GUIStyle _tooltipDetailStyle;
        private GUIStyle _completionPopupStyle;
        private GUIStyle _completionItemStyle;
        private GUIStyle _completionItemSelectedStyle;
        private GUIStyle _completionDetailStyle;
        private GUIStyle _inlineSuggestionStyle;
        private Texture2D _selectionFill;
        private Texture2D _caretFill;
        private Texture2D _currentLineFill;
        private Texture2D _currentLineEdgeFill;
        private Texture2D _surfaceFill;
        private Texture2D _completionPopupFill;
        private Texture2D _completionSelectedFill;
        private Texture2D _completionBorderFill;
        private string _styleCacheKey = string.Empty;
        private float _lineHeight = DefaultLineHeight;
        private bool _hasFocus;
        private bool _isDraggingSelection;
        private int _dragAnchorIndex;
        private int _lastClickIndex = -1;
        private DateTime _lastClickUtc = DateTime.MinValue;
        private string _hoverCandidateKey = string.Empty;
        private DateTime _hoverCandidateUtc = DateTime.MinValue;
        private bool _contextMenuOpen;
        private Vector2 _contextMenuPosition = Vector2.zero;
        private EditorCommandInvocation _contextInvocation;
        private string _lastDrawError = string.Empty;
        private DateTime _lastDrawErrorUtc = DateTime.MinValue;
        private LayoutCache _layout;
        private Rect _lastContextMenuRect;

        private struct PointerContext
        {
            public bool IsWithinSurface;
            public bool PreGroupHit;
            public bool UsedRectOffset;
            public Rect SurfaceRect;
            public Vector2 RawMouse;
            public Vector2 SurfaceMouse;
            public Vector2 ContentMouse;
        }

        private struct PointerHitTestResult
        {
            public int CharacterIndex;
            public int LineIndex;
            public int LineNumber;
            public int LineStartIndex;
            public int RawLength;
            public int CandidateColumn;
            public float TargetX;
            public float PreviousWidth;
            public float CandidateWidth;
        }

        public Vector2 Draw(
            Rect rect,
            Vector2 scroll,
            DocumentSession session,
            bool editingEnabled,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            CortexShellState state,
            string themeKey,
            GUIStyle codeStyle,
            GUIStyle gutterStyle,
            GUIStyle tooltipStyle,
            GUIStyle contextMenuStyle,
            GUIStyle contextMenuButtonStyle,
            GUIStyle contextMenuHeaderStyle,
            Rect blockedRect,
            float gutterWidth,
            HarmonyPatchGenerationService harmonyPatchGenerationService,
            GeneratedTemplateNavigationService generatedTemplateNavigationService)
        {
            if (session == null || !session.SupportsEditing || rect.width <= 0f || rect.height <= 0f)
            {
                _hasFocus = false;
                _isDraggingSelection = false;
                return scroll;
            }

            try
            {
                var hadExplicitCaretPlacement = session.EditorState != null && session.EditorState.HasExplicitCaretPlacement;
                var highlightedLineBeforeEnsure = session.HighlightedLine;
                _editorService.EnsureDocumentState(session);
                if (!hadExplicitCaretPlacement && highlightedLineBeforeEnsure > 0 && session.EditorState != null)
                {
                    var bootstrappedSelection = _editorService.GetPrimarySelection(session);
                    var bootstrappedCaret = _editorService.GetCaretPosition(session, bootstrappedSelection.CaretIndex);
                    EditorInteractionLog.WriteEditorStateBootstrap(
                        session.FilePath,
                        highlightedLineBeforeEnsure,
                        bootstrappedCaret.Line + 1,
                        bootstrappedCaret.Column + 1,
                        bootstrappedSelection.CaretIndex,
                        session.EditorState.HasExplicitCaretPlacement);
                }
                _editorService.SetUndoLimit(session, state != null && state.Settings != null ? state.Settings.EditorUndoHistoryLimit : 128);
                if (generatedTemplateNavigationService != null)
                {
                    generatedTemplateNavigationService.SyncSession(state, session);
                }
                EnsureStyles(themeKey, codeStyle, gutterStyle, tooltipStyle);
                EnsureLayout(session, themeKey, gutterWidth);
                if (_layout == null || _codeStyle == null || _gutterStyle == null || _surfaceFill == null)
                {
                    return scroll;
                }
                if (!editingEnabled)
                {
                    ClearCompletion(state);
                }
                var inputTextVersion = session.TextVersion;
                var inputAnalysisTick = session.LastLanguageAnalysisUtc.Ticks;

                var current = Event.current;
                var localMouse = current != null ? current.mousePosition - new Vector2(rect.x, rect.y) : Vector2.zero;
                var mouseBlocked = current != null && blockedRect.width > 0f && blockedRect.height > 0f && blockedRect.Contains(current.mousePosition);
                var hasMouse = current != null && rect.Contains(current.mousePosition) && !mouseBlocked;
                if (_contextMenuOpen && _popupMenuItems.Count > 0)
                {
                    _lastContextMenuRect = _popupMenuSurface.PredictMenuRect(_contextMenuPosition, rect.size, _popupMenuItems);
                }
                PreHandleContextMenuInput(current, localMouse);
                HandleKeyboardInput(session, state, editingEnabled, commandRegistry, current, generatedTemplateNavigationService);
                if (session.TextVersion != inputTextVersion)
                {
                    _documentLanguageAnalysisService.ApplyProvisionalClassificationProjection(session);
                }

                if (session.TextVersion != inputTextVersion ||
                    session.LastLanguageAnalysisUtc.Ticks != inputAnalysisTick)
                {
                    EnsureLayout(session, themeKey, gutterWidth);
                    if (_layout == null)
                    {
                        return scroll;
                    }
                }

                scroll = EnsureCaretVisible(session, scroll, rect.height);
                var pointerContext = BuildPointerContext(current, rect, hasMouse, localMouse, scroll, true);
                HandlePointerInput(session, state, editingEnabled, current, pointerContext, gutterWidth, commandRegistry, contributionRegistry, harmonyPatchGenerationService);
                var hoverTarget = hasMouse && !_isDraggingSelection
                    ? TryResolveHoverTarget(session, state, editingEnabled, pointerContext.ContentMouse, gutterWidth)
                    : null;
                _hoverService.UpdateHoverRequest(
                    session,
                    state,
                    hoverTarget,
                    hasMouse && !_contextMenuOpen && !_isDraggingSelection,
                    ref _hoverCandidateKey,
                    ref _hoverCandidateUtc);

                GUI.BeginGroup(rect);
                try
                {
                    var contentRect = new Rect(0f, 0f, Mathf.Max(rect.width - 18f, _layout.ContentWidth), Mathf.Max(rect.height - 18f, _layout.ContentHeight));
                    var preserveEditorScroll = ShouldPreserveEditorScroll(current, localMouse);
                    var scrollBeforeDraw = scroll;
                    try
                    {
                        scroll = GUI.BeginScrollView(new Rect(0f, 0f, rect.width, rect.height), scroll, contentRect);
                        GUI.DrawTexture(new Rect(0f, 0f, contentRect.width, contentRect.height), _surfaceFill);
                        DrawLines(session, state, scroll, rect.height, gutterWidth);
                    }
                    finally
                    {
                        GUI.EndScrollView();
                    }

                    if (preserveEditorScroll)
                    {
                        scroll = scrollBeforeDraw;
                    }

                    _semanticPopupSurface.DrawQuickActions(state, GetViewportAnchorRect(session, state != null && state.Semantic != null ? state.Semantic.QuickActionsTarget : null, scroll, gutterWidth), rect.size, commandRegistry);
                    if (editingEnabled)
                    {
                        DrawCompletionPopup(session, state, scroll, rect.size, gutterWidth);
                    }
                    DrawSignatureHelpPopup(session, state, scroll, rect.size, gutterWidth);
                    _semanticPopupSurface.DrawRename(state, GetViewportAnchorRect(session, state != null && state.Editor != null ? state.Editor.ActiveRenameTarget : null, scroll, gutterWidth), rect.size, commandRegistry);
                    _semanticPopupSurface.DrawPeek(state, GetViewportAnchorRect(session, state != null && state.Editor != null ? state.Editor.ActivePeekTarget : null, scroll, gutterWidth), rect.size);
                    DrawHoverTooltip(state, hoverTarget, pointerContext.SurfaceMouse, rect.size);
                    DrawContextMenu(state, current, pointerContext.SurfaceMouse, rect.size, commandRegistry, contextMenuStyle, contextMenuButtonStyle, contextMenuHeaderStyle);
                }
                finally
                {
                    GUI.EndGroup();
                }
            }
            catch (Exception ex)
            {
                _hasFocus = false;
                _isDraggingSelection = false;
                CloseContextMenu();
                _layout = null;
                var error = ex.GetType().FullName + "|" + (ex.Message ?? string.Empty);
                if (!string.Equals(_lastDrawError, error, StringComparison.Ordinal) ||
                    (DateTime.UtcNow - _lastDrawErrorUtc).TotalSeconds >= 1d)
                {
                    _lastDrawError = error;
                    _lastDrawErrorUtc = DateTime.UtcNow;
                    MMLog.WriteError("[Cortex.Editor] Editable code surface draw failed: " + ex);
                }
            }

            return scroll;
        }

        public void Invalidate()
        {
            _layout = null;
            CloseContextMenu();
        }

        /// <summary>
        /// Draws a horizontal row of buttons derived from the active context-menu
        /// contributions so every right-click action is also a one-click button.
        /// Separator items between groups are rendered as a small visual gap.
        ///
        /// Invoke from <see cref="EditorModule"/> between the path bar and the
        /// code area so the bar is always visible, regardless of scroll position.
        /// </summary>
        public void DrawContextActionBar(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            GUIStyle buttonStyle)
        {
            if (session == null || state == null || commandRegistry == null || contributionRegistry == null)
            {
                return;
            }

            // Build a target from the current caret position so the bar reflects
            // the symbol under the caret rather than a stale right-click position.
            EditorCommandInvocation barInvocation;
            var caretIndex = session.EditorState != null ? session.EditorState.CaretIndex : 0;
            if (!TryBuildCommandTarget(session, state, editingEnabled, caretIndex, out barInvocation))
            {
                // No identifier under the caret – use a minimal document-level
                // target so generic actions (Copy, Paste, Undo, …) remain visible.
                barInvocation = BuildDocumentInvocation(session, state, editingEnabled, caretIndex);
            }

            var items = _toolbarService.BuildItems(state, commandRegistry, contributionRegistry, barInvocation);
            if (items == null || items.Count == 0)
            {
                return;
            }

            var style = buttonStyle ?? GUI.skin.button;
            GUILayout.BeginHorizontal();
            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.IsSeparator)
                {
                    // Visual gap between command groups – mirrors the context menu separator.
                    GUILayout.Space(6f);
                    continue;
                }

                var previousEnabled = GUI.enabled;
                GUI.enabled = item.Enabled;

                var cleanLabel = item.Label ?? string.Empty;
                var parenIndex = cleanLabel.LastIndexOf('(');
                if (parenIndex >= 0 && cleanLabel.EndsWith(")"))
                {
                    cleanLabel = cleanLabel.Substring(0, parenIndex).Trim();
                }
                if (cleanLabel.EndsWith("..."))
                {
                    cleanLabel = cleanLabel.Substring(0, cleanLabel.Length - 3).Trim();
                }

                var content = string.IsNullOrEmpty(item.ToolTip)
                    ? new GUIContent(cleanLabel)
                    : new GUIContent(cleanLabel, item.ToolTip);
                if (GUILayout.Button(content, style))
                {
                    _contextMenuService.Execute(state, commandRegistry, barInvocation, item.CommandId);
                }

                GUI.enabled = previousEnabled;
            }

            GUILayout.EndHorizontal();
        }

        private void EnsureStyles(string themeKey, GUIStyle codeStyle, GUIStyle gutterStyle, GUIStyle tooltipStyle)
        {
            var cacheKey = (themeKey ?? string.Empty) + "|" +
                (codeStyle != null && codeStyle.font != null ? codeStyle.font.name : string.Empty) + "|" +
                (codeStyle != null ? codeStyle.fontSize.ToString() : "0") + "|" +
                (tooltipStyle != null && tooltipStyle.font != null ? tooltipStyle.font.name : string.Empty);
            if (string.Equals(cacheKey, _styleCacheKey, StringComparison.Ordinal) &&
                _codeStyle != null &&
                _gutterStyle != null &&
                _tooltipContainerStyle != null &&
                _tooltipTitleStyle != null &&
                _tooltipQualifiedStyle != null &&
                _tooltipDetailStyle != null &&
                _completionPopupStyle != null &&
                _completionItemStyle != null &&
                _completionItemSelectedStyle != null &&
                _completionDetailStyle != null &&
                _inlineSuggestionStyle != null &&
                _selectionFill != null &&
                _caretFill != null &&
                _currentLineFill != null &&
                _currentLineEdgeFill != null &&
                _completionPopupFill != null &&
                _completionSelectedFill != null &&
                _completionBorderFill != null &&
                _surfaceFill != null)
            {
                return;
            }

            _styleCacheKey = cacheKey;
            _codeStyle = new GUIStyle(codeStyle ?? GUI.skin.label);
            _codeStyle.padding = new RectOffset(0, 0, 0, 0);
            _codeStyle.margin = new RectOffset(0, 0, 0, 0);
            _codeStyle.border = new RectOffset(0, 0, 0, 0);
            _codeStyle.overflow = new RectOffset(0, 0, 0, 0);
            _codeStyle.wordWrap = false;
            _codeStyle.richText = false;
            _codeStyle.alignment = TextAnchor.UpperLeft;
            _codeStyle.clipping = TextClipping.Overflow;
            _codeStyle.stretchWidth = false;
            _codeStyle.stretchHeight = false;
            GuiStyleUtil.ApplyBackgroundToAllStates(_codeStyle, null);

            _gutterStyle = new GUIStyle(gutterStyle ?? GUI.skin.label);
            _gutterStyle.wordWrap = false;
            _gutterStyle.clipping = TextClipping.Clip;
            _tooltipContainerStyle = new GUIStyle(tooltipStyle ?? GUI.skin.box);
            _tooltipContainerStyle.padding = new RectOffset(8, 8, 8, 8);
            _tooltipContainerStyle.margin = new RectOffset(0, 0, 0, 0);
            _tooltipContainerStyle.wordWrap = true;
            _tooltipTitleStyle = new GUIStyle(_codeStyle);
            _tooltipTitleStyle.wordWrap = false;
            _tooltipTitleStyle.fontStyle = FontStyle.Bold;
            GuiStyleUtil.ApplyTextColorToAllStates(_tooltipTitleStyle, CortexIdeLayout.GetTextColor());
            _tooltipQualifiedStyle = new GUIStyle(_codeStyle);
            _tooltipQualifiedStyle.wordWrap = false;
            GuiStyleUtil.ApplyTextColorToAllStates(_tooltipQualifiedStyle, CortexIdeLayout.GetMutedTextColor());
            _tooltipDetailStyle = new GUIStyle(_codeStyle);
            _tooltipDetailStyle.wordWrap = true;
            _tooltipDetailStyle.clipping = TextClipping.Clip;
            GuiStyleUtil.ApplyTextColorToAllStates(_tooltipDetailStyle, CortexIdeLayout.GetTextColor());
            _classificationStyles.Clear();
            _lineHeight = _codeStyle.lineHeight > 0f ? Mathf.Max(DefaultLineHeight, _codeStyle.lineHeight + 2f) : DefaultLineHeight;
            _selectionFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.30f));
            _caretFill = MakeFill(CortexIdeLayout.GetAccentColor());
            _currentLineFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetSurfaceColor(), 0.16f));
            _currentLineEdgeFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.28f));
            _completionPopupFill = MakeFill(CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.55f));
            _completionSelectedFill = MakeFill(CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetSurfaceColor(), 0.22f));
            _completionBorderFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.38f));
            _surfaceFill = MakeFill(CortexIdeLayout.GetBackgroundColor());
            _completionPopupStyle = new GUIStyle(GUI.skin.box);
            _completionPopupStyle.padding = new RectOffset(4, 4, 4, 4);
            _completionPopupStyle.margin = new RectOffset(0, 0, 0, 0);
            _completionPopupStyle.border = new RectOffset(1, 1, 1, 1);
            GuiStyleUtil.ApplyBackgroundToAllStates(_completionPopupStyle, _completionPopupFill);
            GuiStyleUtil.ApplyTextColorToAllStates(_completionPopupStyle, CortexIdeLayout.GetTextColor());

            _completionItemStyle = new GUIStyle(_codeStyle);
            _completionItemStyle.padding = new RectOffset(8, 8, 2, 2);
            _completionItemStyle.margin = new RectOffset(0, 0, 0, 0);
            _completionItemStyle.alignment = TextAnchor.MiddleLeft;
            GuiStyleUtil.ApplyBackgroundToAllStates(_completionItemStyle, null);
            GuiStyleUtil.ApplyTextColorToAllStates(_completionItemStyle, CortexIdeLayout.GetTextColor());

            _completionItemSelectedStyle = new GUIStyle(_completionItemStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_completionItemSelectedStyle, _completionSelectedFill);

            _completionDetailStyle = new GUIStyle(_completionItemStyle);
            _completionDetailStyle.alignment = TextAnchor.MiddleRight;
            GuiStyleUtil.ApplyTextColorToAllStates(_completionDetailStyle, CortexIdeLayout.GetMutedTextColor());
            _inlineSuggestionStyle = new GUIStyle(_codeStyle);
            GuiStyleUtil.ApplyBackgroundToAllStates(_inlineSuggestionStyle, null);
            GuiStyleUtil.ApplyTextColorToAllStates(_inlineSuggestionStyle, CortexIdeLayout.WithAlpha(CortexIdeLayout.GetMutedTextColor(), 0.72f));
            Invalidate();
        }

        private void EnsureLayout(DocumentSession session, string themeKey, float gutterWidth)
        {
            var analysisTick = session != null ? session.LastLanguageAnalysisUtc.Ticks : 0L;
            var textVersion = session != null ? session.TextVersion : 0;
            if (_layout == null ||
                !string.Equals(_layout.ThemeKey, themeKey ?? string.Empty, StringComparison.Ordinal) ||
                _layout.AnalysisTick != analysisTick)
            {
                _layout = BuildLayout(session, themeKey, gutterWidth);
                if (session != null && session.EditorState != null)
                {
                    session.EditorState.PendingInvalidation = new EditorInvalidation();
                }
                return;
            }

            if (_layout.TextVersion != textVersion)
            {
                if (session != null &&
                    session.EditorState != null &&
                    session.EditorState.PendingInvalidation != null &&
                    (session.EditorState.PendingInvalidation.NewLength != 0 || session.EditorState.PendingInvalidation.OldLength != 0))
                {
                    RebuildLayoutFromLine(session, gutterWidth, session.EditorState.PendingInvalidation.StartLine);
                    _layout.TextVersion = textVersion;
                    session.EditorState.PendingInvalidation = new EditorInvalidation();
                    return;
                }

                _layout = BuildLayout(session, themeKey, gutterWidth);
                return;
            }

            if (session != null &&
                session.EditorState != null &&
                session.EditorState.PendingInvalidation != null &&
                (session.EditorState.PendingInvalidation.NewLength != 0 || session.EditorState.PendingInvalidation.OldLength != 0))
            {
                RebuildLayoutFromLine(session, gutterWidth, session.EditorState.PendingInvalidation.StartLine);
                _layout.TextVersion = textVersion;
                session.EditorState.PendingInvalidation = new EditorInvalidation();
            }
        }

        private LayoutCache BuildLayout(DocumentSession session, string themeKey, float gutterWidth)
        {
            var cache = new LayoutCache();
            cache.ThemeKey = themeKey ?? string.Empty;
            cache.TextVersion = session != null ? session.TextVersion : 0;
            cache.AnalysisTick = session != null ? session.LastLanguageAnalysisUtc.Ticks : 0L;
            RebuildLayout(cache, session, gutterWidth, 0);
            return cache;
        }

        private void RebuildLayoutFromLine(DocumentSession session, float gutterWidth, int startLine)
        {
            if (_layout == null)
            {
                _layout = BuildLayout(session, string.Empty, gutterWidth);
                return;
            }

            RebuildLayout(_layout, session, gutterWidth, Math.Max(0, startLine));
        }

        private void RebuildLayout(LayoutCache cache, DocumentSession session, float gutterWidth, int startLine)
        {
            if (session == null)
            {
                cache.Lines.Clear();
                cache.ContentWidth = gutterWidth + 120f;
                cache.ContentHeight = _lineHeight + 4f;
                return;
            }

            if (startLine <= 0 || startLine >= cache.Lines.Count)
            {
                cache.Lines.Clear();
                startLine = 0;
            }
            else
            {
                cache.Lines.RemoveRange(startLine, cache.Lines.Count - startLine);
            }

            session.Text = session.Text ?? string.Empty;
            NormalizeSpans(session);
            var map = _editorService.GetLineMap(session);
            for (var lineIndex = 0; lineIndex < map.LineStarts.Length; lineIndex++)
            {
                if (lineIndex < startLine)
                {
                    continue;
                }

                var lineLayout = BuildLineLayout(session, map, lineIndex, gutterWidth);
                lineLayout.Y = lineIndex * _lineHeight;
                cache.Lines.Add(lineLayout);
            }

            cache.ContentWidth = gutterWidth + 120f;
            for (var i = 0; i < cache.Lines.Count; i++)
            {
                cache.ContentWidth = Mathf.Max(cache.ContentWidth, gutterWidth + cache.Lines[i].Width + 24f);
            }

            cache.ContentHeight = Mathf.Max(_lineHeight, cache.Lines.Count * _lineHeight + 4f);
        }

        private EditableLineLayout BuildLineLayout(DocumentSession session, EditorLineMap map, int lineIndex, float gutterWidth)
        {
            var lineNumber = lineIndex + 1;
            var text = session != null ? session.Text ?? string.Empty : string.Empty;
            var lineStart = map.LineStarts[lineIndex];
            var lineEnd = lineIndex + 1 < map.LineStarts.Length ? map.LineStarts[lineIndex + 1] : text.Length;
            while (lineEnd > lineStart)
            {
                var trailing = text[lineEnd - 1];
                if (trailing != '\r' && trailing != '\n')
                {
                    break;
                }

                lineEnd--;
            }

            var rawText = lineEnd > lineStart ? text.Substring(lineStart, lineEnd - lineStart) : string.Empty;
            var layout = new EditableLineLayout();
            layout.LineNumber = lineNumber;
            layout.StartIndex = lineStart;
            layout.EndIndex = lineEnd;
            layout.RawText = rawText;
            layout.DisplayText = ExpandTabs(rawText);
            layout.Width = Measure(layout.DisplayText);
            BuildSegments(layout);
            return layout;
        }

        private void BuildSegments(EditableLineLayout lineLayout)
        {
            if (lineLayout == null)
            {
                return;
            }

            lineLayout.Segments.Clear();
            var lineStart = lineLayout.StartIndex;
            var lineEnd = lineLayout.EndIndex;
            var cursor = lineStart;
            for (var i = 0; i < _orderedSpans.Count; i++)
            {
                var span = _orderedSpans[i];
                var spanStart = Math.Max(lineStart, span.Start);
                var spanEnd = Math.Min(lineEnd, span.Start + span.Length);
                if (spanEnd <= spanStart || spanEnd <= cursor)
                {
                    continue;
                }

                var segmentStart = Math.Max(cursor, spanStart);
                if (segmentStart > cursor)
                {
                    AddSegmentRuns(lineLayout, cursor - lineStart, segmentStart - cursor, string.Empty);
                }

                AddSegmentRuns(lineLayout, segmentStart - lineStart, spanEnd - segmentStart, span.Classification);
                cursor = spanEnd;
                if (cursor >= lineEnd)
                {
                    break;
                }
            }

            if (cursor < lineEnd)
            {
                AddSegmentRuns(lineLayout, cursor - lineStart, lineEnd - cursor, string.Empty);
            }

            if (lineLayout.Segments.Count == 0)
            {
                AddSegmentRuns(lineLayout, 0, lineLayout.RawText.Length, string.Empty);
            }
        }

        private void AddSegmentRuns(EditableLineLayout lineLayout, int startInLine, int rawLength, string classification)
        {
            if (lineLayout == null || rawLength <= 0)
            {
                return;
            }

            var normalizedClassification = _classificationPresentationService.NormalizeClassification(classification);
            if (!ShouldTokenizeForPresentation(normalizedClassification))
            {
                AddSegment(lineLayout, startInLine, rawLength, normalizedClassification);
                return;
            }

            var raw = lineLayout.RawText.Substring(startInLine, rawLength);
            var offset = 0;
            while (offset < raw.Length)
            {
                var runLength = 1;
                var kind = ClassifyCharacter(raw[offset]);
                if (kind == CharacterKind.Word || kind == CharacterKind.Whitespace)
                {
                    while (offset + runLength < raw.Length && ClassifyCharacter(raw[offset + runLength]) == kind)
                    {
                        runLength++;
                    }
                }

                var tokenStartInLine = startInLine + offset;
                var effectiveClassification = kind == CharacterKind.Word
                    ? _classificationPresentationService.GetEffectiveLineTokenClassification(
                        normalizedClassification,
                        lineLayout.RawText,
                        tokenStartInLine,
                        runLength)
                    : normalizedClassification;
                AddSegment(lineLayout, tokenStartInLine, runLength, effectiveClassification);
                offset += runLength;
            }
        }

        private void AddSegment(EditableLineLayout lineLayout, int startInLine, int rawLength, string classification)
        {
            if (rawLength <= 0)
            {
                return;
            }

            var raw = lineLayout.RawText.Substring(startInLine, rawLength);
            lineLayout.Segments.Add(new EditableSegment
            {
                StartInLine = startInLine,
                Length = rawLength,
                DisplayText = ExpandTabs(raw),
                Classification = classification ?? string.Empty
            });
        }

        private void NormalizeSpans(DocumentSession session)
        {
            _orderedSpans.Clear();
            if (session == null || session.LanguageAnalysis == null || session.LanguageAnalysis.Classifications == null)
            {
                return;
            }

            var textLength = session.Text != null ? session.Text.Length : 0;
            for (var i = 0; i < session.LanguageAnalysis.Classifications.Length; i++)
            {
                var span = session.LanguageAnalysis.Classifications[i];
                if (span == null || span.Length <= 0 || span.Start < 0 || span.Start >= textLength)
                {
                    continue;
                }

                _orderedSpans.Add(new NormalizedSpan
                {
                    Start = span.Start,
                    Length = Math.Min(span.Length, textLength - span.Start),
                    Classification = span.Classification ?? string.Empty
                });
            }

            _orderedSpans.Sort(delegate(NormalizedSpan left, NormalizedSpan right)
            {
                if (left.Start != right.Start)
                {
                    return left.Start.CompareTo(right.Start);
                }

                return right.Length.CompareTo(left.Length);
            });
        }

        private Vector2 EnsureCaretVisible(DocumentSession session, Vector2 scroll, float viewportHeight)
        {
            if (session == null || session.EditorState == null || !session.EditorState.ScrollToCaretPending)
            {
                return scroll;
            }

            var caret = _editorService.GetCaretPosition(session, session.EditorState.CaretIndex);
            if (_layout == null || caret.Line < 0 || caret.Line >= _layout.Lines.Count)
            {
                return scroll;
            }

            var line = _layout.Lines[caret.Line];
            var caretY = line.Y;
            if (caretY < scroll.y)
            {
                scroll.y = Mathf.Max(0f, caretY - (_lineHeight * 0.5f));
            }
            else if (caretY + _lineHeight > scroll.y + viewportHeight)
            {
                scroll.y = Mathf.Max(0f, caretY - viewportHeight + (_lineHeight * 1.5f));
            }

            session.EditorState.ScrollToCaretPending = false;
            return scroll;
        }

        private void DrawLines(DocumentSession session, CortexShellState state, Vector2 scroll, float viewportHeight, float gutterWidth)
        {
            if (_layout == null || _layout.Lines.Count == 0)
            {
                return;
            }

            var firstLine = Mathf.Max(0, Mathf.FloorToInt(scroll.y / _lineHeight));
            var lastLine = Mathf.Min(_layout.Lines.Count - 1, Mathf.CeilToInt((scroll.y + viewportHeight) / _lineHeight) + 1);
            var primarySelection = _editorService.GetPrimarySelection(session);
            var selections = _editorService.GetSelections(session);
            var primaryCaret = _editorService.GetCaretPosition(session, primarySelection.CaretIndex);

            for (var i = firstLine; i <= lastLine; i++)
            {
                var line = _layout.Lines[i];
                var lineRect = new Rect(0f, line.Y, _layout.ContentWidth, _lineHeight);
                if (_hasFocus && primaryCaret.Line == i)
                {
                    GUI.DrawTexture(lineRect, _currentLineFill);
                    GUI.DrawTexture(new Rect(0f, line.Y, _layout.ContentWidth, 1f), _currentLineEdgeFill);
                    GUI.DrawTexture(new Rect(0f, line.Y + _lineHeight - 1f, _layout.ContentWidth, 1f), _currentLineEdgeFill);
                }

                DrawGutter(line, gutterWidth);
                for (var selectionIndex = 0; selectionIndex < selections.Length; selectionIndex++)
                {
                    DrawSelection(line, gutterWidth, selections[selectionIndex].Start, selections[selectionIndex].End);
                }
                DrawCode(line, gutterWidth);
                if (_hasFocus)
                {
                    for (var selectionIndex = 0; selectionIndex < selections.Length; selectionIndex++)
                    {
                        var caret = _editorService.GetCaretPosition(session, selections[selectionIndex].CaretIndex);
                        if (caret.Line == i)
                        {
                            DrawCaret(line, gutterWidth, selections[selectionIndex].CaretIndex);
                        }
                    }
                }
            }

            if (_hasFocus && primaryCaret.Line >= 0 && primaryCaret.Line < _layout.Lines.Count)
            {
                DrawInlineSuggestion(session, state, _layout.Lines[primaryCaret.Line], gutterWidth);
            }
        }

        private void DrawGutter(EditableLineLayout line, float gutterWidth)
        {
            GUI.Label(new Rect(0f, line.Y, gutterWidth - 6f, _lineHeight), line.LineNumber.ToString("D4"), _gutterStyle);
        }

        private void DrawSelection(EditableLineLayout line, float gutterWidth, int selectionStart, int selectionEnd)
        {
            var start = Mathf.Max(selectionStart, line.StartIndex);
            var end = Mathf.Min(selectionEnd, line.EndIndex);
            if (end < start || (selectionStart == selectionEnd && selectionStart == line.EndIndex))
            {
                return;
            }

            var startX = gutterWidth + Measure(ExpandTabs(line.RawText.Substring(0, Math.Max(0, start - line.StartIndex))));
            var selectedRawLength = Math.Max(0, end - start);
            var selectedDisplay = selectedRawLength > 0 ? ExpandTabs(line.RawText.Substring(start - line.StartIndex, selectedRawLength)) : string.Empty;
            var width = selectedRawLength > 0 ? Mathf.Max(SelectionPadding, Measure(selectedDisplay)) : SelectionPadding;
            GUI.DrawTexture(new Rect(startX, line.Y + 1f, width, _lineHeight - 2f), _selectionFill);
        }

        private void DrawCode(EditableLineLayout line, float gutterWidth)
        {
            var x = gutterWidth;
            for (var i = 0; i < line.Segments.Count; i++)
            {
                var segment = line.Segments[i];
                if (string.IsNullOrEmpty(segment.DisplayText))
                {
                    continue;
                }

                var style = GetClassificationStyle(segment.Classification);
                var width = Measure(segment.DisplayText);
                GUI.Label(new Rect(x, line.Y, width + 2f, _lineHeight), segment.DisplayText, style);
                x += width;
            }
        }

        private void DrawCaret(EditableLineLayout line, float gutterWidth, int caretIndex)
        {
            var rawColumn = Mathf.Max(0, Mathf.Min(line.RawText.Length, caretIndex - line.StartIndex));
            var prefix = rawColumn > 0 ? line.RawText.Substring(0, rawColumn) : string.Empty;
            var x = gutterWidth + Measure(ExpandTabs(prefix));
            GUI.DrawTexture(new Rect(x, line.Y + 1f, 1.5f, _lineHeight - 2f), _caretFill);
        }

        private void DrawInlineSuggestion(DocumentSession session, CortexShellState state, EditableLineLayout line, float gutterWidth)
        {
            if (session == null || state == null || state.Editor == null || line.RawText == null)
            {
                return;
            }

            string suffixText;
            if (!_editorCompletionService.TryGetInlineSuggestionSuffix(state.Editor, session, out suffixText) ||
                string.IsNullOrEmpty(suffixText))
            {
                return;
            }

            var displayLines = BuildInlineSuggestionDisplayLines(suffixText);
            if (displayLines == null || displayLines.Length == 0)
            {
                return;
            }

            var caretIndex = session.EditorState != null ? session.EditorState.CaretIndex : line.StartIndex;
            var rawColumn = Mathf.Max(0, Mathf.Min(line.RawText.Length, caretIndex - line.StartIndex));
            var prefix = rawColumn > 0 ? line.RawText.Substring(0, rawColumn) : string.Empty;
            var x = gutterWidth + Measure(ExpandTabs(prefix));
            for (var i = 0; i < displayLines.Length; i++)
            {
                var displayLine = displayLines[i];
                if (string.IsNullOrEmpty(displayLine))
                {
                    continue;
                }

                var drawX = i == 0 ? x + 2f : gutterWidth;
                var drawWidth = Mathf.Max(2f, Measure(displayLine) + 4f);
                GUI.Label(new Rect(drawX, line.Y + (i * _lineHeight), drawWidth, _lineHeight), displayLine, _inlineSuggestionStyle);
            }
        }

        private void DrawCompletionPopup(DocumentSession session, CortexShellState state, Vector2 scroll, Vector2 viewportSize, float gutterWidth)
        {
            if (_layout == null || session == null || state == null || state.Editor == null)
            {
                return;
            }

            var response = state.Editor.ActiveCompletionResponse;
            if (!_editorCompletionService.IsVisibleForSession(state.Editor, session))
            {
                if (HasVisibleCompletion(state))
                {
                    _editorCompletionService.ClearPopupCompletion(state.Editor);
                }

                return;
            }

            var caret = _editorService.GetCaretPosition(session, session.EditorState.CaretIndex);
            if (caret.Line < 0 || caret.Line >= _layout.Lines.Count)
            {
                return;
            }

            _editorCompletionService.SyncSelection(state.Editor);
            var caretRect = BuildCaretViewportRect(_layout.Lines[caret.Line], gutterWidth, session.EditorState.CaretIndex, scroll);
            var popupWidth = CalculateCompletionPopupWidth(response);
            var visibleCount = Math.Min(CompletionVisibleItemCount, response.Items.Length);
            var popupHeight = visibleCount * _lineHeight + 8f;
            var popupRect = new Rect(caretRect.x, caretRect.yMax + 2f, popupWidth, popupHeight);
            if (popupRect.xMax > viewportSize.x - 6f)
            {
                popupRect.x = Mathf.Max(4f, viewportSize.x - popupWidth - 6f);
            }

            if (popupRect.yMax > viewportSize.y - 6f)
            {
                popupRect.y = Mathf.Max(4f, caretRect.y - popupHeight - 4f);
            }

            GUI.Box(popupRect, GUIContent.none, _completionPopupStyle);
            GUI.DrawTexture(new Rect(popupRect.x, popupRect.y, popupRect.width, 1f), _completionBorderFill);
            GUI.DrawTexture(new Rect(popupRect.x, popupRect.yMax - 1f, popupRect.width, 1f), _completionBorderFill);

            var selectedIndex = state.Editor.CompletionSelectedIndex;
            var firstVisible = Mathf.Clamp(selectedIndex - 3, 0, Math.Max(0, response.Items.Length - visibleCount));
            for (var i = 0; i < visibleCount; i++)
            {
                var itemIndex = firstVisible + i;
                var item = response.Items[itemIndex];
                if (item == null)
                {
                    continue;
                }

                var rowRect = new Rect(popupRect.x + 2f, popupRect.y + 4f + (i * _lineHeight), popupRect.width - 4f, _lineHeight);
                var isSelected = itemIndex == selectedIndex;
                if (isSelected)
                {
                    GUI.DrawTexture(rowRect, _completionSelectedFill);
                }

                var displayRect = new Rect(rowRect.x + 6f, rowRect.y, rowRect.width * 0.62f, rowRect.height);
                GUI.Label(displayRect, item.DisplayText ?? string.Empty, isSelected ? _completionItemSelectedStyle : _completionItemStyle);

                var detailText = !string.IsNullOrEmpty(item.InlineDescription)
                    ? item.InlineDescription
                    : (item.Kind ?? string.Empty);
                if (!string.IsNullOrEmpty(detailText))
                {
                    var detailRect = new Rect(rowRect.x + rowRect.width * 0.63f, rowRect.y, rowRect.width * 0.33f, rowRect.height);
                    GUI.Label(detailRect, detailText, _completionDetailStyle);
                }
            }
        }

        private void DrawSignatureHelpPopup(DocumentSession session, CortexShellState state, Vector2 scroll, Vector2 viewportSize, float gutterWidth)
        {
            if (_layout == null || session == null || state == null || state.Editor == null)
            {
                return;
            }

            if (!_editorSignatureHelpService.HasVisibleSignatureHelp(state.Editor, session))
            {
                if (state.Editor.ActiveSignatureHelpResponse != null)
                {
                    ClearSignatureHelp(state);
                }
                return;
            }

            var response = state.Editor.ActiveSignatureHelpResponse;
            if (response == null || response.Items == null || response.Items.Length == 0)
            {
                return;
            }

            var activeSignatureIndex = Mathf.Clamp(response.ActiveSignatureIndex, 0, response.Items.Length - 1);
            var activeItem = response.Items[activeSignatureIndex];
            if (activeItem == null)
            {
                return;
            }

            var caret = _editorService.GetCaretPosition(session, session.EditorState.CaretIndex);
            if (caret.Line < 0 || caret.Line >= _layout.Lines.Count)
            {
                return;
            }

            var signatureText = BuildSignatureHelpText(activeItem, response.ActiveParameterIndex);
            if (string.IsNullOrEmpty(signatureText))
            {
                return;
            }

            var detailText = activeItem.Documentation ?? string.Empty;
            var caretRect = BuildCaretViewportRect(_layout.Lines[caret.Line], gutterWidth, session.EditorState.CaretIndex, scroll);
            var popupWidth = Mathf.Min(560f, Mathf.Max(280f, Measure(signatureText) + 32f));
            var popupHeight = _lineHeight + 10f;
            if (!string.IsNullOrEmpty(detailText))
            {
                popupHeight += Mathf.Max(_lineHeight, _tooltipDetailStyle.CalcHeight(new GUIContent(detailText), popupWidth - 20f)) + 8f;
            }

            var popupRect = new Rect(caretRect.x, caretRect.yMax + 2f, popupWidth, popupHeight);
            if (HasVisibleCompletion(state))
            {
                popupRect.y = Mathf.Max(4f, caretRect.y - popupHeight - 4f);
            }

            if (popupRect.xMax > viewportSize.x - 6f)
            {
                popupRect.x = Mathf.Max(4f, viewportSize.x - popupWidth - 6f);
            }

            if (popupRect.yMax > viewportSize.y - 6f)
            {
                popupRect.y = Mathf.Max(4f, viewportSize.y - popupHeight - 6f);
            }

            GUI.Box(popupRect, GUIContent.none, _completionPopupStyle);
            GUI.DrawTexture(new Rect(popupRect.x, popupRect.y, popupRect.width, 1f), _completionBorderFill);
            GUI.DrawTexture(new Rect(popupRect.x, popupRect.yMax - 1f, popupRect.width, 1f), _completionBorderFill);
            GUI.Label(new Rect(popupRect.x + 8f, popupRect.y + 4f, popupRect.width - 16f, _lineHeight + 2f), signatureText, _completionItemStyle);
            if (!string.IsNullOrEmpty(detailText))
            {
                GUI.Label(new Rect(popupRect.x + 8f, popupRect.y + _lineHeight + 8f, popupRect.width - 16f, popupRect.height - _lineHeight - 12f), detailText, _tooltipDetailStyle);
            }
        }

        private static string BuildSignatureHelpText(LanguageServiceSignatureHelpItem item, int activeParameterIndex)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var prefix = item.PrefixDisplay ?? string.Empty;
            var separator = !string.IsNullOrEmpty(item.SeparatorDisplay) ? item.SeparatorDisplay : ", ";
            var suffix = item.SuffixDisplay ?? string.Empty;
            var parameters = item.Parameters ?? new LanguageServiceSignatureHelpParameter[0];
            if (parameters.Length == 0)
            {
                return prefix + suffix;
            }

            var parameterParts = new string[parameters.Length];
            for (var i = 0; i < parameters.Length; i++)
            {
                var display = parameters[i] != null && !string.IsNullOrEmpty(parameters[i].Display)
                    ? parameters[i].Display
                    : parameters[i] != null ? parameters[i].Name ?? string.Empty : string.Empty;
                parameterParts[i] = i == activeParameterIndex
                    ? "[" + display + "]"
                    : display;
            }

            return prefix + string.Join(separator, parameterParts) + suffix;
        }

        private Rect BuildCaretViewportRect(EditableLineLayout line, float gutterWidth, int caretIndex, Vector2 scroll)
        {
            var rawColumn = Mathf.Max(0, Mathf.Min(line.RawText.Length, caretIndex - line.StartIndex));
            var prefix = rawColumn > 0 ? line.RawText.Substring(0, rawColumn) : string.Empty;
            var x = gutterWidth + Measure(ExpandTabs(prefix)) - scroll.x;
            var y = line.Y - scroll.y;
            return new Rect(x, y, 2f, _lineHeight);
        }

        private Rect GetCharacterRect(DocumentSession session, int characterIndex, float gutterWidth)
        {
            if (_layout == null || session == null || _layout.Lines.Count == 0)
            {
                return new Rect(gutterWidth, 0f, 2f, _lineHeight);
            }

            var caret = _editorService.GetCaretPosition(session, characterIndex);
            var lineIndex = Mathf.Clamp(caret.Line, 0, _layout.Lines.Count - 1);
            var line = _layout.Lines[lineIndex];
            var rawLength = line.RawText != null ? line.RawText.Length : 0;
            var rawColumn = Mathf.Clamp(characterIndex - line.StartIndex, 0, rawLength);
            var prefix = rawColumn > 0 ? line.RawText.Substring(0, rawColumn) : string.Empty;
            var x = gutterWidth + Measure(ExpandTabs(prefix));
            var width = 2f;
            if (line.RawText != null && rawColumn < line.RawText.Length)
            {
                width = Mathf.Max(2f, Measure(ExpandTabs(line.RawText.Substring(rawColumn, 1))));
            }

            return new Rect(x, line.Y, width, _lineHeight);
        }

        private float CalculateCompletionPopupWidth(LanguageServiceCompletionResponse response)
        {
            if (response == null || response.Items == null || response.Items.Length == 0)
            {
                return 240f;
            }

            var width = 240f;
            var maxItems = Math.Min(response.Items.Length, CompletionVisibleItemCount);
            for (var i = 0; i < maxItems; i++)
            {
                var item = response.Items[i];
                if (item == null)
                {
                    continue;
                }

                var detailText = !string.IsNullOrEmpty(item.InlineDescription)
                    ? item.InlineDescription
                    : (item.Kind ?? string.Empty);
                width = Mathf.Max(width, 40f + Measure(item.DisplayText ?? string.Empty) + Measure(detailText) + 40f);
            }

            return Mathf.Min(520f, width);
        }

        private static PointerContext BuildPointerContext(Event current, Rect surfaceRect, bool isWithinSurface, Vector2 surfaceMouse, Vector2 scroll, bool usedRectOffset)
        {
            var context = new PointerContext();
            if (current == null)
            {
                return context;
            }

            context.PreGroupHit = isWithinSurface;
            context.SurfaceRect = surfaceRect;
            context.RawMouse = current.mousePosition;
            context.UsedRectOffset = usedRectOffset;
            context.SurfaceMouse = surfaceMouse;
            context.IsWithinSurface = isWithinSurface;
            context.ContentMouse = scroll + context.SurfaceMouse;
            return context;
        }

        private void HandlePointerInput(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            Event current,
            PointerContext pointerContext,
            float gutterWidth,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            HarmonyPatchGenerationService harmonyPatchGenerationService)
        {
            if (current == null)
            {
                return;
            }

            if (_contextMenuOpen && _lastContextMenuRect.Contains(pointerContext.SurfaceMouse))
            {
                // Context menu is open and cursor is over it; let its buttons receive the event.
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 1)
            {
                _hasFocus = pointerContext.IsWithinSurface;
                if (!pointerContext.IsWithinSurface)
                {
                    CloseContextMenu();
                    return;
                }

                var hitTest = GetCharacterIndexAt(session, pointerContext.ContentMouse, gutterWidth);
                OpenContextMenu(session, state, editingEnabled, hitTest.CharacterIndex, pointerContext.SurfaceMouse, commandRegistry, contributionRegistry);
                current.Use();
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                ClearCompletion(state);
                _hasFocus = pointerContext.IsWithinSurface;
                if (!pointerContext.IsWithinSurface)
                {
                    _isDraggingSelection = false;
                    CloseContextMenu();
                    return;
                }

                var hitTest = GetCharacterIndexAt(session, pointerContext.ContentMouse, gutterWidth);
                var selectionAction = ApplyPointerSelection(session, current, hitTest.CharacterIndex);
                LogPointerSelection(session, selectionAction, pointerContext, gutterWidth, hitTest);
                WritePointerSelectionAudit(session, selectionAction, pointerContext, hitTest);

                session.EditorState.ScrollToCaretPending = false;
                _isDraggingSelection = true;
                if (HandleHarmonyInsertionPick(session, state, hitTest, harmonyPatchGenerationService))
                {
                    current.Use();
                    return;
                }

                // Ctrl+Double-click → Go to Definition (Visual Studio convention).
                // Plain double-click → word selection only; no navigation.
                if (string.Equals(selectionAction, "double-click", StringComparison.Ordinal) &&
                    current != null && current.control)
                {
                    EditorCommandInvocation invocation;
                    if (TryBuildCommandTarget(session, state, editingEnabled, hitTest.CharacterIndex, out invocation) &&
                        invocation != null &&
                        invocation.Target != null &&
                        invocation.Target.CanGoToDefinition)
                    {
                        _symbolInteractionService.RequestDefinition(state, invocation.Target);
                    }
                }

                current.Use();
                return;
            }

            if (_isDraggingSelection && current.type == EventType.MouseDrag && current.button == 0)
            {
                var draggedHitTest = GetCharacterIndexAt(session, pointerContext.ContentMouse, gutterWidth);
                _editorService.SetSelection(session, _dragAnchorIndex, draggedHitTest.CharacterIndex);
                session.EditorState.ScrollToCaretPending = false;
                current.Use();
                return;
            }

            if (current.type == EventType.MouseUp && current.button == 0)
            {
                _isDraggingSelection = false;
            }
        }

        private bool HandleHarmonyInsertionPick(
            DocumentSession session,
            CortexShellState state,
            PointerHitTestResult hitTest,
            HarmonyPatchGenerationService harmonyPatchGenerationService)
        {
            if (state == null ||
                state.Harmony == null ||
                !state.Harmony.IsInsertionPickActive ||
                harmonyPatchGenerationService == null)
            {
                return false;
            }

            string statusMessage;
            if (harmonyPatchGenerationService.TryApplyEditorInsertionSelection(state, session, hitTest.LineNumber, hitTest.CharacterIndex, out statusMessage))
            {
                MMLog.WriteInfo("[Cortex.Harmony] Editor insertion point selected from writable editor. Document='" +
                    (session != null ? session.FilePath ?? string.Empty : string.Empty) +
                    "', Line=" + hitTest.LineNumber + ".");
            }
            else
            {
                MMLog.WriteWarning("[Cortex.Harmony] Editor insertion point selection failed. Document='" +
                    (session != null ? session.FilePath ?? string.Empty : string.Empty) +
                    "', Line=" + hitTest.LineNumber +
                    ", Reason='" + (statusMessage ?? string.Empty) + "'.");
            }

            return true;
        }

        private string ApplyPointerSelection(DocumentSession session, Event current, int clickedIndex)
        {
            var now = DateTime.UtcNow;
            var isDoubleClick = _lastClickIndex >= 0 &&
                Math.Abs(clickedIndex - _lastClickIndex) <= 1 &&
                (now - _lastClickUtc).TotalSeconds <= DoubleClickThresholdSeconds;
            _lastClickIndex = clickedIndex;
            _lastClickUtc = now;

            if (isDoubleClick)
            {
                _editorService.ClearSecondarySelections(session);
                _editorService.SelectWord(session, clickedIndex);
                _dragAnchorIndex = _editorService.GetPrimarySelection(session).AnchorIndex;
                return "double-click";
            }

            if (current != null && current.shift)
            {
                _dragAnchorIndex = _editorService.GetPrimarySelection(session).AnchorIndex;
                _editorService.SetSelection(session, _dragAnchorIndex, clickedIndex);
                return "shift-click";
            }

            _editorService.SetSelection(session, clickedIndex, clickedIndex);
            _dragAnchorIndex = clickedIndex;
            return "click";
        }

        private void PreHandleContextMenuInput(Event current, Vector2 localMouse)
        {
            if (!_contextMenuOpen)
            {
                return;
            }

            _popupMenuSurface.TryCapturePointerInput(current, _lastContextMenuRect, localMouse);
            if (current != null && current.type == EventType.ScrollWheel && !_lastContextMenuRect.Contains(localMouse))
            {
                current.Use();
            }
        }

        private bool ShouldPreserveEditorScroll(Event current, Vector2 localMouse)
        {
            if (!_contextMenuOpen)
            {
                return false;
            }

            if (current != null && current.type == EventType.ScrollWheel)
            {
                return true;
            }

            return Mathf.Abs(Input.GetAxisRaw("Mouse ScrollWheel")) > 0.0001f ||
                Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.0001f;
        }

        private SourceEditorHoverTarget TryResolveHoverTarget(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            Vector2 contentMouse,
            float gutterWidth)
        {
            var hitTest = GetCharacterIndexAt(session, contentMouse, gutterWidth);
            SourceEditorHoverTarget hoverTarget;
            if (!_hoverService.TryCreateHoverTarget(session, state, editingEnabled, hitTest.CharacterIndex, out hoverTarget))
            {
                return null;
            }

            return hoverTarget;
        }

        private void DrawHoverTooltip(CortexShellState state, SourceEditorHoverTarget hoverTarget, Vector2 localMouse, Vector2 viewportSize)
        {
            var response = hoverTarget != null ? _hoverService.ResolveHoverResponse(state, hoverTarget.HoverKey) : null;
            if (hoverTarget == null || hoverTarget.Target == null || response == null || !response.Success || _tooltipContainerStyle == null)
            {
                _hoverService.ClearVisibleHover(state);
                return;
            }

            var title = !string.IsNullOrEmpty(response.SymbolDisplay)
                ? response.SymbolDisplay
                : (hoverTarget.Target.SymbolText ?? string.Empty);
            if (string.IsNullOrEmpty(title))
            {
                _hoverService.ClearVisibleHover(state);
                return;
            }

            var qualified = response.QualifiedSymbolDisplay ?? string.Empty;
            var detail = response.DocumentationText ?? string.Empty;
            var titleHeight = Mathf.Max(18f, _tooltipTitleStyle.CalcHeight(new GUIContent(title), TooltipWidth - 16f));
            var qualifiedHeight = string.IsNullOrEmpty(qualified)
                ? 0f
                : Mathf.Max(16f, _tooltipQualifiedStyle.CalcHeight(new GUIContent(qualified), TooltipWidth - 16f));
            var detailHeight = string.IsNullOrEmpty(detail)
                ? 0f
                : Mathf.Max(18f, _tooltipDetailStyle.CalcHeight(new GUIContent(detail), TooltipWidth - 16f));
            var tooltipHeight = 16f + titleHeight + qualifiedHeight + detailHeight +
                (!string.IsNullOrEmpty(qualified) && detailHeight > 0f ? 4f : 0f);
            var tooltipRect = ClampTooltipRect(
                new Rect(localMouse.x + 18f, localMouse.y + 18f, TooltipWidth, tooltipHeight),
                viewportSize);

            GUI.Box(tooltipRect, GUIContent.none, _tooltipContainerStyle);

            var contentRect = new Rect(tooltipRect.x + 8f, tooltipRect.y + 8f, tooltipRect.width - 16f, tooltipRect.height - 16f);
            var titleRect = new Rect(contentRect.x, contentRect.y, contentRect.width, titleHeight);
            GUI.Label(titleRect, title, _tooltipTitleStyle);

            var nextY = titleRect.yMax;
            if (!string.IsNullOrEmpty(qualified))
            {
                var qualifiedRect = new Rect(contentRect.x, nextY, contentRect.width, qualifiedHeight);
                GUI.Label(qualifiedRect, qualified, _tooltipQualifiedStyle);
                nextY = qualifiedRect.yMax;
            }

            if (!string.IsNullOrEmpty(detail))
            {
                if (nextY > titleRect.yMax)
                {
                    nextY += 4f;
                }

                var detailRect = new Rect(contentRect.x, nextY, contentRect.width, detailHeight);
                GUI.Label(detailRect, detail, _tooltipDetailStyle);
            }

            _hoverService.SetVisibleHover(state, hoverTarget.HoverKey, response);
        }

        private static Rect ClampTooltipRect(Rect tooltipRect, Vector2 viewportSize)
        {
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            {
                return tooltipRect;
            }

            if (tooltipRect.xMax > viewportSize.x - 8f)
            {
                tooltipRect.x = viewportSize.x - tooltipRect.width - 8f;
            }

            if (tooltipRect.yMax > viewportSize.y - 8f)
            {
                tooltipRect.y = viewportSize.y - tooltipRect.height - 8f;
            }

            if (tooltipRect.x < 8f)
            {
                tooltipRect.x = 8f;
            }

            if (tooltipRect.y < 8f)
            {
                tooltipRect.y = 8f;
            }

            return tooltipRect;
        }

        private void DrawContextMenu(
            CortexShellState state,
            Event current,
            Vector2 localMouse,
            Vector2 viewportSize,
            ICommandRegistry commandRegistry,
            GUIStyle contextMenuStyle,
            GUIStyle contextMenuButtonStyle,
            GUIStyle contextMenuHeaderStyle)
        {
            if (!_contextMenuOpen || _contextInvocation == null || _contextInvocation.Target == null || _popupMenuItems.Count == 0)
            {
                return;
            }

            var menuResult = _popupMenuSurface.Draw(
                _contextMenuPosition,
                viewportSize,
                _contextInvocation.Target.SymbolText ?? string.Empty,
                _popupMenuItems,
                current,
                localMouse,
                contextMenuStyle,
                contextMenuButtonStyle,
                contextMenuHeaderStyle);

            _lastContextMenuRect = menuResult.MenuRect;

            if (!string.IsNullOrEmpty(menuResult.ActivatedCommandId))
            {
                _contextMenuService.Execute(state, commandRegistry, _contextInvocation, menuResult.ActivatedCommandId);
                EditorInteractionLog.WriteContextMenu("Executed context menu command '" + menuResult.ActivatedCommandId + "'.");
                CloseContextMenu("command executed");
                return;
            }

            if (menuResult.ShouldClose)
            {
                CloseContextMenu("menu requested close");
            }
        }

        private void OpenContextMenu(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            int absolutePosition,
            Vector2 localMouse,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry)
        {
            EditorCommandInvocation invocation;
            if (!TryBuildCommandTarget(session, state, editingEnabled, absolutePosition, out invocation))
            {
                invocation = BuildDocumentInvocation(session, state, editingEnabled, absolutePosition);
            }

            var target = invocation != null ? invocation.Target : null;
            var items = _contextMenuService.BuildItems(state, commandRegistry, contributionRegistry, invocation);
            if (items == null || items.Count == 0)
            {
                CloseContextMenu("no items available");
                return;
            }

            _contextMenuOpen = true;
            _contextMenuPosition = localMouse;
            _contextInvocation = invocation;
            _popupMenuSurface.Reset();
            PopulatePopupMenuItems(items);
            var enabledCount = CountEnabledPopupMenuItems();
            EditorInteractionLog.WriteContextMenu(
                "Opened context menu for '" + (target != null ? target.SymbolText ?? string.Empty : string.Empty) +
                "'. Items=" + _popupMenuItems.Count +
                ", Enabled=" + enabledCount +
                ", Mouse=(" + localMouse.x.ToString("F1") + "," + localMouse.y.ToString("F1") + ")" +
                ", TargetSymbol='" + (target != null ? target.SymbolText ?? string.Empty : string.Empty) + "'" +
                ", AbsolutePosition=" + absolutePosition + ".");
        }

        private bool TryBuildCommandTarget(DocumentSession session, CortexShellState state, bool editingEnabled, int absolutePosition, out EditorCommandInvocation invocation)
        {
            invocation = null;
            EditorCommandTarget target;
            if (!_hoverService.TryCreateInteractionTarget(session, state, editingEnabled, absolutePosition, out target))
            {
                return false;
            }

            invocation = _commandContextFactory.CreateForTarget(state, target);
            return invocation != null;
        }

        private EditorCommandInvocation BuildDocumentInvocation(DocumentSession session, CortexShellState state, bool editingEnabled, int absolutePosition)
        {
            return _commandContextFactory.CreateDocumentInvocation(session, state, editingEnabled, absolutePosition);
        }

        private Rect GetViewportAnchorRect(DocumentSession session, EditorCommandTarget target, Vector2 scroll, float gutterWidth)
        {
            if (session == null || target == null)
            {
                return new Rect(Mathf.Max(0f, gutterWidth - scroll.x), Mathf.Max(0f, -scroll.y), 2f, _lineHeight);
            }

            var targetIndex = _editorService.GetCharacterIndex(session, target.Line - 1, target.Column - 1);
            var characterRect = GetCharacterRect(session, targetIndex, gutterWidth);
            return new Rect(characterRect.x - scroll.x, characterRect.y - scroll.y, characterRect.width, characterRect.height);
        }

        private void PopulatePopupMenuItems(IList<EditorContextMenuItem> items)
        {
            _popupMenuItems.Clear();
            if (items == null)
            {
                return;
            }

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                _popupMenuItems.Add(new PopupMenuItem
                {
                    CommandId = item.CommandId,
                    Label = item.Label,
                    ShortcutText = item.ShortcutText,
                    Enabled = item.Enabled,
                    IsSeparator = item.IsSeparator,
                    IsSectionHeader = item.IsSectionHeader
                });
            }
        }

        private int CountEnabledPopupMenuItems()
        {
            var count = 0;
            for (var i = 0; i < _popupMenuItems.Count; i++)
            {
                var item = _popupMenuItems[i];
                if (item != null && !item.IsSeparator && !item.IsSectionHeader && item.Enabled)
                {
                    count++;
                }
            }

            return count;
        }

        private void CloseContextMenu()
        {
            CloseContextMenu("unspecified");
        }

        private void CloseContextMenu(string reason)
        {
            if (_contextMenuOpen || _popupMenuItems.Count > 0 || _contextInvocation != null)
            {
                EditorInteractionLog.WriteContextMenu(
                    "Closing context menu. Reason=" + (reason ?? string.Empty) +
                    ", TargetSymbol='" + (_contextInvocation != null && _contextInvocation.Target != null ? (_contextInvocation.Target.SymbolText ?? string.Empty) : string.Empty) + "'" +
                    ", " + _popupMenuSurface.BuildDiagnosticsSummary());
            }

            _contextMenuOpen = false;
            _contextInvocation = null;
            _lastContextMenuRect = EmptyRect;
            _popupMenuSurface.Reset();
            _popupMenuItems.Clear();
        }

        private void LogPointerSelection(DocumentSession session, string action, PointerContext pointerContext, float gutterWidth, PointerHitTestResult hitTest)
        {
            if (!EditorInteractionLog.IsSelectionDiagnosticsEnabled || session == null || string.IsNullOrEmpty(action))
            {
                return;
            }

            var selection = _editorService.GetPrimarySelection(session);
            var caret = _editorService.GetCaretPosition(session, selection.CaretIndex);
            EditorInteractionLog.WritePointerSelection(
                session.FilePath,
                action,
                caret.Line + 1,
                caret.Column + 1,
                selection.CaretIndex,
                selection.HasSelection,
                pointerContext.PreGroupHit,
                pointerContext.UsedRectOffset,
                pointerContext.SurfaceRect,
                pointerContext.RawMouse,
                pointerContext.SurfaceMouse,
                pointerContext.ContentMouse,
                gutterWidth,
                session.Text != null ? session.Text.Length : 0,
                session.TextVersion,
                session.EditorState != null ? session.EditorState.Selections.Count : 0,
                hitTest.LineIndex,
                hitTest.LineNumber,
                hitTest.LineStartIndex,
                hitTest.RawLength,
                hitTest.CharacterIndex,
                hitTest.CandidateColumn,
                hitTest.TargetX,
                hitTest.PreviousWidth,
                hitTest.CandidateWidth,
                _lineHeight);
        }

        private void WritePointerSelectionAudit(DocumentSession session, string action, PointerContext pointerContext, PointerHitTestResult hitTest)
        {
            if (!EditorInteractionLog.IsSelectionDiagnosticsEnabled || session == null || string.IsNullOrEmpty(action))
            {
                return;
            }

            var selection = _editorService.GetPrimarySelection(session);
            var caret = _editorService.GetCaretPosition(session, selection.CaretIndex);
            MMLog.WriteDebug("[Cortex.Editor] Pointer selection. Action=" + action +
                ", File=" + (session.FilePath ?? string.Empty) +
                ", RawMouse=(" + pointerContext.RawMouse.x.ToString("F1") + "," + pointerContext.RawMouse.y.ToString("F1") + ")" +
                ", SurfaceMouse=(" + pointerContext.SurfaceMouse.x.ToString("F1") + "," + pointerContext.SurfaceMouse.y.ToString("F1") + ")" +
                ", ContentMouse=(" + pointerContext.ContentMouse.x.ToString("F1") + "," + pointerContext.ContentMouse.y.ToString("F1") + ")" +
                ", UsedRectOffset=" + pointerContext.UsedRectOffset +
                ", HitLine=" + hitTest.LineNumber +
                ", HitColumn=" + (hitTest.CandidateColumn + 1) +
                ", HitIndex=" + hitTest.CharacterIndex +
                ", SelectedLine=" + (caret.Line + 1) +
                ", SelectedColumn=" + (caret.Column + 1) +
                ", SelectedIndex=" + selection.CaretIndex + ".");
        }

        private void HandleKeyboardInput(DocumentSession session, CortexShellState state, bool editingEnabled, ICommandRegistry commandRegistry, Event current, GeneratedTemplateNavigationService generatedTemplateNavigationService)
        {
            if (!_hasFocus || current == null || current.type != EventType.KeyDown)
            {
                return;
            }

            if (GUIUtility.keyboardControl != 0 && !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl()))
            {
                return;
            }

            var selectionCountBefore = session != null && session.EditorState != null ? session.EditorState.Selections.Count : 0;
            var caretIndexBefore = session != null && session.EditorState != null ? session.EditorState.CaretIndex : 0;
            if (editingEnabled &&
                generatedTemplateNavigationService != null &&
                current.keyCode == KeyCode.Tab &&
                generatedTemplateNavigationService.TryHandleNavigation(state, session, current.shift))
            {
                current.Use();
                return;
            }

            if (TryHandleCompletionInput(session, state, current, editingEnabled))
            {
                current.Use();
                return;
            }

            if (TryHandleSignatureHelpInput(session, state, current))
            {
                current.Use();
                return;
            }

            var previousTextVersion = session != null ? session.TextVersion : 0;
            var handled = false;
            var commandId = string.Empty;
            if (_keybindingService.TryResolveCommand(state != null ? state.Settings : null, current, out commandId))
            {
                if (string.Equals(commandId, "edit.complete", StringComparison.Ordinal))
                {
                    if (editingEnabled)
                    {
                        QueueCompletionRequest(session, state, true, string.Empty);
                        handled = true;
                    }
                }
                else if (string.Equals(commandId, "edit.parameterinfo", StringComparison.Ordinal))
                {
                    QueueSignatureHelpRequest(session, state, true, string.Empty);
                    handled = true;
                }
                else
                {
                    handled = ExecuteCommand(session, state, commandRegistry, commandId, current.shift, editingEnabled);
                }
            }
            else if (current.control && current.shift && current.keyCode == KeyCode.Space)
            {
                QueueSignatureHelpRequest(session, state, true, string.Empty);
                handled = true;
            }

            if (!handled && !current.control && !current.alt)
            {
                switch (current.keyCode)
                {
                    case KeyCode.LeftArrow:
                        _editorService.MoveCaretHorizontal(session, -1, current.shift);
                        handled = true;
                        break;
                    case KeyCode.RightArrow:
                        _editorService.MoveCaretHorizontal(session, 1, current.shift);
                        handled = true;
                        break;
                    case KeyCode.UpArrow:
                        _editorService.MoveCaretVertical(session, -1, current.shift);
                        handled = true;
                        break;
                    case KeyCode.DownArrow:
                        _editorService.MoveCaretVertical(session, 1, current.shift);
                        handled = true;
                        break;
                    case KeyCode.Home:
                        _editorService.MoveCaretToLineBoundary(session, true, current.shift);
                        handled = true;
                        break;
                    case KeyCode.End:
                        _editorService.MoveCaretToLineBoundary(session, false, current.shift);
                        handled = true;
                        break;
                    case KeyCode.PageUp:
                        _editorService.MoveCaretVertical(session, -16, current.shift);
                        handled = true;
                        break;
                    case KeyCode.PageDown:
                        _editorService.MoveCaretVertical(session, 16, current.shift);
                        handled = true;
                        break;
                    case KeyCode.Return:
                    case KeyCode.KeypadEnter:
                        handled = editingEnabled && _editorService.InsertNewLine(session);
                        break;
                }
            }

            if (!handled)
            {
                handled = editingEnabled && HandleTextInput(session, current.character);
                if (handled)
                {
                    EditorInteractionLog.WriteEdit("Applied direct keyboard text input to the active document.");
                }
            }

            if (handled)
            {
                LogKeyboardSelectionState(session, commandId, current.character, selectionCountBefore, caretIndexBefore);
                HandleCompletionAfterKey(session, state, current, commandId, previousTextVersion, editingEnabled);
                current.Use();
            }
        }

        private void LogKeyboardSelectionState(DocumentSession session, string commandId, char character, int selectionCountBefore, int caretIndexBefore)
        {
            if (!EditorInteractionLog.IsSelectionDiagnosticsEnabled || session == null || session.EditorState == null)
            {
                return;
            }

            var selection = _editorService.GetPrimarySelection(session);
            var caret = _editorService.GetCaretPosition(session, selection.CaretIndex);
            EditorInteractionLog.WriteKeyboardSelectionState(
                session.FilePath,
                commandId,
                character,
                selectionCountBefore,
                session.EditorState.Selections.Count,
                caretIndexBefore,
                selection.CaretIndex,
                selection.AnchorIndex,
                caret.Line + 1,
                caret.Column + 1);
        }

        private bool ExecuteCommand(DocumentSession session, CortexShellState state, ICommandRegistry commandRegistry, string commandId, bool extendSelection, bool editingEnabled)
        {
            bool handled;
            if (_commandRouterService.TryExecuteLocal(session, commandId, extendSelection, editingEnabled, out handled))
            {
                if (handled)
                {
                    EditorInteractionLog.WriteEdit("Executed editor command: " + (commandId ?? string.Empty) + ".");
                }

                return handled;
            }

            if (!handled && commandRegistry != null && !string.IsNullOrEmpty(commandId))
            {
                handled = commandRegistry.Execute(commandId, new CommandExecutionContext
                {
                    ActiveContainerId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                    ActiveDocumentId = state != null ? state.Documents.ActiveDocumentPath : string.Empty,
                    FocusedRegionId = state != null ? state.Workbench.FocusedContainerId : string.Empty,
                    Parameter = session
                });
            }

            if (handled)
            {
                EditorInteractionLog.WriteEdit("Executed editor command: " + (commandId ?? string.Empty) + ".");
            }

            return handled;
        }

        private bool TryHandleCompletionInput(DocumentSession session, CortexShellState state, Event current, bool editingEnabled)
        {
            if (!editingEnabled)
            {
                return false;
            }

            var hasInlineSuggestion = _editorCompletionService.HasVisibleInlineSuggestion(state != null ? state.Editor : null, session);
            var response = state != null && state.Editor != null ? state.Editor.ActiveCompletionResponse : null;
            if (!hasInlineSuggestion && !_editorCompletionService.HasCompletionItems(response))
            {
                return false;
            }

            if (hasInlineSuggestion)
            {
                switch (current.keyCode)
                {
                    case KeyCode.Tab:
                        if (!current.shift)
                        {
                            return ApplyInlineSuggestion(session, state);
                        }

                        break;
                    case KeyCode.Escape:
                        ClearCompletion(state);
                        return true;
                }
            }

            if (!_editorCompletionService.HasCompletionItems(response))
            {
                return false;
            }

            _editorCompletionService.SyncSelection(state.Editor);
            switch (current.keyCode)
            {
                case KeyCode.UpArrow:
                    _editorCompletionService.MoveSelection(state.Editor, -1);
                    return true;
                case KeyCode.DownArrow:
                    _editorCompletionService.MoveSelection(state.Editor, 1);
                    return true;
                case KeyCode.PageUp:
                    _editorCompletionService.MoveSelection(state.Editor, -CompletionVisibleItemCount);
                    return true;
                case KeyCode.PageDown:
                    _editorCompletionService.MoveSelection(state.Editor, CompletionVisibleItemCount);
                    return true;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    return ApplySelectedCompletion(session, state);
                case KeyCode.Tab:
                    if (!current.shift)
                    {
                        return ApplySelectedCompletion(session, state);
                    }

                    return false;
                case KeyCode.Escape:
                    ClearCompletion(state);
                    return true;
            }

            return false;
        }

        private bool TryHandleSignatureHelpInput(DocumentSession session, CortexShellState state, Event current)
        {
            if (state == null || state.Editor == null || !_editorSignatureHelpService.HasVisibleSignatureHelp(state.Editor, session))
            {
                return false;
            }

            if (current.keyCode == KeyCode.Escape)
            {
                ClearSignatureHelp(state);
                return true;
            }

            return false;
        }

        private void HandleCompletionAfterKey(DocumentSession session, CortexShellState state, Event current, string commandId, int previousTextVersion, bool editingEnabled)
        {
            if (state == null || state.Editor == null || session == null || session.EditorState == null)
            {
                return;
            }

            if (string.Equals(commandId, "edit.complete", StringComparison.Ordinal))
            {
                return;
            }

            var textChanged = editingEnabled && session.TextVersion != previousTextVersion;
            if (textChanged)
            {
                if (_editorSignatureHelpService.ShouldDismissAfterText(current.character))
                {
                    ClearSignatureHelp(state);
                }
                else if (_editorSignatureHelpService.ShouldTriggerSignatureHelp(current.character) ||
                    _editorSignatureHelpService.HasVisibleSignatureHelp(state.Editor, session))
                {
                    QueueSignatureHelpRequest(session, state, false, current.character != '\0' ? current.character.ToString() : string.Empty);
                }

                if (_editorCompletionService.ShouldTriggerCompletion(current.character))
                {
                    MMLog.WriteInfo("[Cortex.Completion] Triggering completion from typed character '" +
                        current.character +
                        "' in " + (session.FilePath ?? string.Empty) + ".");
                    QueueCompletionRequest(session, state, false, current.character.ToString());
                    return;
                }

                if (_editorCompletionService.ShouldContinueCompletion(session, session.EditorState.CaretIndex))
                {
                    MMLog.WriteInfo("[Cortex.Completion] Continuing completion after text edit in " +
                        (session.FilePath ?? string.Empty) +
                        ". CaretIndex=" + session.EditorState.CaretIndex + ".");
                    QueueCompletionRequest(session, state, false, string.Empty);
                    return;
                }

                ClearCompletion(state);
            }

            var movementCommand =
                string.Equals(commandId, "caret.left", StringComparison.Ordinal) ||
                string.Equals(commandId, "caret.right", StringComparison.Ordinal) ||
                string.Equals(commandId, "caret.up", StringComparison.Ordinal) ||
                string.Equals(commandId, "caret.down", StringComparison.Ordinal) ||
                string.Equals(commandId, "caret.line.start", StringComparison.Ordinal) ||
                string.Equals(commandId, "caret.line.end", StringComparison.Ordinal) ||
                string.Equals(commandId, "caret.document.start", StringComparison.Ordinal) ||
                string.Equals(commandId, "caret.document.end", StringComparison.Ordinal);

            if (movementCommand &&
                (HasVisibleCompletion(state) || _editorCompletionService.HasVisibleInlineSuggestion(state != null ? state.Editor : null, session)))
            {
                ClearCompletion(state);
            }

            if (_editorSignatureHelpService.HasVisibleSignatureHelp(state.Editor, session) && movementCommand)
            {
                ClearSignatureHelp(state);
            }
        }

        private void QueueCompletionRequest(DocumentSession session, CortexShellState state, bool explicitInvocation, string triggerCharacter)
        {
            MMLog.WriteInfo("[Cortex.Completion] Queue request requested. Explicit=" +
                explicitInvocation +
                ", Trigger='" + (triggerCharacter ?? string.Empty) +
                "', Document=" + (session != null ? session.FilePath ?? string.Empty : string.Empty) +
                ", CaretIndex=" + (session != null && session.EditorState != null ? session.EditorState.CaretIndex.ToString() : "-1") + ".");
            if (!_editorCompletionService.QueueRequest(
                session,
                state != null ? state.Editor : null,
                _editorService,
                explicitInvocation,
                triggerCharacter))
            {
                MMLog.WriteInfo("[Cortex.Completion] Queue request was rejected before dispatch. Document=" +
                    (session != null ? session.FilePath ?? string.Empty : string.Empty) + ".");
                ClearCompletion(state);
                return;
            }

            MMLog.WriteInfo("[Cortex.Completion] Queue request accepted. Explicit=" +
                explicitInvocation +
                ", Trigger='" + (triggerCharacter ?? string.Empty) +
                "', Document=" + (session != null ? session.FilePath ?? string.Empty : string.Empty) + ".");
        }

        private bool ApplySelectedCompletion(DocumentSession session, CortexShellState state)
        {
            return _editorCompletionService.ApplySelectedCompletion(
                session,
                state != null ? state.Editor : null,
                _editorService);
        }

        private bool ApplyInlineSuggestion(DocumentSession session, CortexShellState state)
        {
            return _editorCompletionService.ApplyInlineSuggestion(
                session,
                state != null ? state.Editor : null,
                _editorService);
        }

        private bool HasVisibleCompletion(CortexShellState state)
        {
            return state != null && _editorCompletionService.HasVisibleCompletion(state.Editor);
        }

        private void ClearCompletion(CortexShellState state)
        {
            _editorCompletionService.Reset(state != null ? state.Editor : null);
        }

        private void QueueSignatureHelpRequest(DocumentSession session, CortexShellState state, bool explicitInvocation, string triggerCharacter)
        {
            if (!_editorSignatureHelpService.QueueRequest(
                session,
                state != null ? state.Editor : null,
                _editorService,
                explicitInvocation,
                triggerCharacter))
            {
                ClearSignatureHelp(state);
            }
        }

        private void ClearSignatureHelp(CortexShellState state)
        {
            _editorSignatureHelpService.Reset(state != null ? state.Editor : null);
        }

        private static string[] BuildInlineSuggestionDisplayLines(string suffixText)
        {
            if (string.IsNullOrEmpty(suffixText))
            {
                return new string[0];
            }

            var normalized = suffixText.Replace("\r\n", "\n").Replace('\r', '\n');
            var rawLines = normalized.Split(new[] { '\n' }, StringSplitOptions.None);
            var results = new List<string>();
            var maxPreviewLines = 5;
            for (var i = 0; i < rawLines.Length && results.Count < maxPreviewLines; i++)
            {
                var rawLine = rawLines[i] ?? string.Empty;
                if (i == 0 || !string.IsNullOrEmpty(rawLine))
                {
                    results.Add(ExpandTabs(rawLine));
                }
            }

            if (rawLines.Length > maxPreviewLines && results.Count > 0)
            {
                results[results.Count - 1] = results[results.Count - 1] + "...";
            }

            return results.ToArray();
        }

        private bool HandleTextInput(DocumentSession session, char character)
        {
            if (character == '\0' || character == '\b' || character == '\n' || character == '\r' || character == '\t')
            {
                return false;
            }

            switch (character)
            {
                case '{':
                    return _editorService.InsertPair(session, "{", "}");
                case '(':
                    return _editorService.InsertPair(session, "(", ")");
                case '[':
                    return _editorService.InsertPair(session, "[", "]");
                case '"':
                    return HandleQuoteInput(session, "\"");
                case '\'':
                    return HandleQuoteInput(session, "'");
                case '}':
                case ')':
                case ']':
                    return TryAdvanceOverCloser(session, character) || _editorService.InsertText(session, character.ToString());
                default:
                    return _editorService.InsertText(session, character.ToString());
            }
        }

        private bool HandleQuoteInput(DocumentSession session, string quote)
        {
            if (TryAdvanceOverCloser(session, quote[0]))
            {
                return true;
            }

            return _editorService.InsertPair(session, quote, quote);
        }

        private bool TryAdvanceOverCloser(DocumentSession session, char closer)
        {
            if (session == null || session.EditorState == null || session.EditorState.HasSelection)
            {
                return false;
            }

            var text = session.Text ?? string.Empty;
            var caret = session.EditorState.CaretIndex;
            if (caret < text.Length && text[caret] == closer)
            {
                _editorService.SetCaret(session, caret + 1, false, false);
                return true;
            }

            return false;
        }

        private PointerHitTestResult GetCharacterIndexAt(DocumentSession session, Vector2 contentMouse, float gutterWidth)
        {
            var result = new PointerHitTestResult();
            if (_layout == null || _layout.Lines.Count == 0)
            {
                return result;
            }

            var lineIndex = Mathf.Clamp(Mathf.FloorToInt(contentMouse.y / _lineHeight), 0, _layout.Lines.Count - 1);
            var line = _layout.Lines[lineIndex];
            result.LineIndex = lineIndex;
            result.LineNumber = line.LineNumber;
            result.LineStartIndex = line.StartIndex;
            result.RawLength = line.RawText != null ? line.RawText.Length : 0;
            if (contentMouse.x <= gutterWidth)
            {
                result.CharacterIndex = line.StartIndex;
                return result;
            }

            var targetX = contentMouse.x - gutterWidth;
            result.TargetX = targetX;
            var low = 0;
            var high = line.RawText.Length;
            while (low < high)
            {
                var mid = low + ((high - low) / 2);
                var width = Measure(ExpandTabs(line.RawText.Substring(0, mid)));
                if (width < targetX)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid;
                }
            }

            var candidate = Mathf.Clamp(low, 0, line.RawText.Length);
            if (candidate <= 0)
            {
                result.CharacterIndex = line.StartIndex;
                return result;
            }

            var previous = candidate - 1;
            var previousWidth = Measure(ExpandTabs(line.RawText.Substring(0, previous)));
            var candidateWidth = Measure(ExpandTabs(line.RawText.Substring(0, candidate)));
            result.PreviousWidth = previousWidth;
            result.CandidateWidth = candidateWidth;
            if (Mathf.Abs(targetX - previousWidth) <= Mathf.Abs(candidateWidth - targetX))
            {
                candidate = previous;
            }

            result.CandidateColumn = candidate;
            result.CharacterIndex = line.StartIndex + candidate;
            return result;
        }

        private GUIStyle GetClassificationStyle(string classification)
        {
            var key = classification ?? string.Empty;
            GUIStyle style;
            if (_classificationStyles.TryGetValue(key, out style))
            {
                return style;
            }

            style = new GUIStyle(_codeStyle);
            style.normal.textColor = _classificationPresentationService.GetColor(classification);
            style.padding = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.border = new RectOffset(0, 0, 0, 0);
            style.overflow = new RectOffset(0, 0, 0, 0);
            style.wordWrap = false;
            style.richText = false;
            style.alignment = TextAnchor.UpperLeft;
            style.clipping = TextClipping.Overflow;
            GuiStyleUtil.ApplyBackgroundToAllStates(style, null);
            _classificationStyles[key] = style;
            return style;
        }

        private float Measure(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0f;
            }

            _measureContent.text = text;
            return _codeStyle.CalcSize(_measureContent).x;
        }

        private static string ExpandTabs(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\t", "    ");
        }

        private static CharacterKind ClassifyCharacter(char c)
        {
            if (char.IsWhiteSpace(c))
            {
                return CharacterKind.Whitespace;
            }

            return char.IsLetterOrDigit(c) || c == '_'
                ? CharacterKind.Word
                : CharacterKind.Punctuation;
        }

        private static bool ShouldTokenizeForPresentation(string classification)
        {
            return string.IsNullOrEmpty(classification) ||
                classification.IndexOf("identifier", StringComparison.OrdinalIgnoreCase) >= 0 ||
                classification.IndexOf("text", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static Texture2D MakeFill(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private sealed class LayoutCache
        {
            public string ThemeKey = string.Empty;
            public int TextVersion;
            public long AnalysisTick;
            public readonly List<EditableLineLayout> Lines = new List<EditableLineLayout>();
            public float ContentWidth;
            public float ContentHeight;
        }

        private sealed class EditableLineLayout
        {
            public int LineNumber;
            public int StartIndex;
            public int EndIndex;
            public string RawText = string.Empty;
            public string DisplayText = string.Empty;
            public float Y;
            public float Width;
            public readonly List<EditableSegment> Segments = new List<EditableSegment>();
        }

        private sealed class EditableSegment
        {
            public int StartInLine;
            public int Length;
            public string DisplayText = string.Empty;
            public string Classification = string.Empty;
        }

        private struct NormalizedSpan
        {
            public int Start;
            public int Length;
            public string Classification;
        }

        private enum CharacterKind
        {
            Word,
            Whitespace,
            Punctuation
        }
    }
}
