using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Services;
using ModAPI.Core;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    /// <summary>
    /// Editable source-code surface used for real source files.
    /// It keeps editing behavior isolated from the read-only symbol/hover viewer
    /// so decompiled content never routes through the mutation pipeline.
    /// </summary>
    internal sealed class EditableCodeViewSurface
    {
        private const float DefaultLineHeight = 18f;
        private const float SelectionPadding = 2f;
        private const double DoubleClickThresholdSeconds = 0.32d;
        private const int CompletionVisibleItemCount = 8;

        private readonly IEditorService _editorService = new EditorService();
        private readonly IEditorKeybindingService _keybindingService = new EditorKeybindingService();
        private readonly DocumentLanguageAnalysisService _documentLanguageAnalysisService = new DocumentLanguageAnalysisService();
        private readonly DocumentLanguageInteractionService _documentLanguageInteractionService = new DocumentLanguageInteractionService();
        private readonly GUIContent _measureContent = new GUIContent();
        private readonly List<NormalizedSpan> _orderedSpans = new List<NormalizedSpan>();
        private readonly Dictionary<string, GUIStyle> _classificationStyles = new Dictionary<string, GUIStyle>(StringComparer.OrdinalIgnoreCase);

        private GUIStyle _codeStyle;
        private GUIStyle _gutterStyle;
        private GUIStyle _completionPopupStyle;
        private GUIStyle _completionItemStyle;
        private GUIStyle _completionItemSelectedStyle;
        private GUIStyle _completionDetailStyle;
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
        private string _lastDrawError = string.Empty;
        private DateTime _lastDrawErrorUtc = DateTime.MinValue;
        private string _completionStateKey = string.Empty;
        private int _completionSelectedIndex = -1;
        private LayoutCache _layout;

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
            CortexShellState state,
            string themeKey,
            GUIStyle codeStyle,
            GUIStyle gutterStyle,
            float gutterWidth)
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
                EnsureStyles(themeKey, codeStyle, gutterStyle);
                EnsureLayout(session, themeKey, gutterWidth);
                if (_layout == null || _codeStyle == null || _gutterStyle == null || _surfaceFill == null)
                {
                    return scroll;
                }
                var inputTextVersion = session.TextVersion;
                var inputAnalysisTick = session.LastLanguageAnalysisUtc.Ticks;

                var current = Event.current;
                var localMouse = current != null ? current.mousePosition - new Vector2(rect.x, rect.y) : Vector2.zero;
                var hasMouse = current != null && rect.Contains(current.mousePosition);
                HandleKeyboardInput(session, state, current);
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
                HandlePointerInput(session, state, current, pointerContext, gutterWidth);

                GUI.BeginGroup(rect);
                try
                {
                    var contentRect = new Rect(0f, 0f, Mathf.Max(rect.width - 18f, _layout.ContentWidth), Mathf.Max(rect.height - 18f, _layout.ContentHeight));
                    try
                    {
                        scroll = GUI.BeginScrollView(new Rect(0f, 0f, rect.width, rect.height), scroll, contentRect);
                        GUI.DrawTexture(new Rect(0f, 0f, contentRect.width, contentRect.height), _surfaceFill);
                        DrawLines(session, scroll, rect.height, gutterWidth);
                    }
                    finally
                    {
                        GUI.EndScrollView();
                    }

                    DrawCompletionPopup(session, state, scroll, rect.size, gutterWidth);
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
        }

        private void EnsureStyles(string themeKey, GUIStyle codeStyle, GUIStyle gutterStyle)
        {
            var cacheKey = (themeKey ?? string.Empty) + "|" +
                (codeStyle != null && codeStyle.font != null ? codeStyle.font.name : string.Empty) + "|" +
                (codeStyle != null ? codeStyle.fontSize.ToString() : "0");
            if (string.Equals(cacheKey, _styleCacheKey, StringComparison.Ordinal) &&
                _codeStyle != null &&
                _gutterStyle != null &&
                _completionPopupStyle != null &&
                _completionItemStyle != null &&
                _completionItemSelectedStyle != null &&
                _completionDetailStyle != null &&
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
                    AddSegment(lineLayout, cursor - lineStart, segmentStart - cursor, string.Empty);
                }

                AddSegment(lineLayout, segmentStart - lineStart, spanEnd - segmentStart, span.Classification);
                cursor = spanEnd;
                if (cursor >= lineEnd)
                {
                    break;
                }
            }

            if (cursor < lineEnd)
            {
                AddSegment(lineLayout, cursor - lineStart, lineEnd - cursor, string.Empty);
            }

            if (lineLayout.Segments.Count == 0)
            {
                AddSegment(lineLayout, 0, lineLayout.RawText.Length, string.Empty);
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

        private void DrawLines(DocumentSession session, Vector2 scroll, float viewportHeight, float gutterWidth)
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

        private void DrawCompletionPopup(DocumentSession session, CortexShellState state, Vector2 scroll, Vector2 viewportSize, float gutterWidth)
        {
            if (_layout == null || session == null || state == null || state.Editor == null)
            {
                return;
            }

            var response = state.Editor.ActiveCompletionResponse;
            if (!_documentLanguageInteractionService.HasCompletionItems(response) ||
                !string.Equals(response.DocumentPath ?? string.Empty, session.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase) ||
                response.DocumentVersion != session.TextVersion ||
                session.EditorState == null ||
                session.EditorState.HasMultipleSelections)
            {
                if (HasVisibleCompletion(state))
                {
                    ClearCompletion(state);
                }

                return;
            }

            var caret = _editorService.GetCaretPosition(session, session.EditorState.CaretIndex);
            if (caret.Line < 0 || caret.Line >= _layout.Lines.Count)
            {
                return;
            }

            SyncCompletionSelection(response);
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

            var firstVisible = Mathf.Clamp(_completionSelectedIndex - 3, 0, Math.Max(0, response.Items.Length - visibleCount));
            for (var i = 0; i < visibleCount; i++)
            {
                var itemIndex = firstVisible + i;
                var item = response.Items[itemIndex];
                if (item == null)
                {
                    continue;
                }

                var rowRect = new Rect(popupRect.x + 2f, popupRect.y + 4f + (i * _lineHeight), popupRect.width - 4f, _lineHeight);
                var isSelected = itemIndex == _completionSelectedIndex;
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

        private Rect BuildCaretViewportRect(EditableLineLayout line, float gutterWidth, int caretIndex, Vector2 scroll)
        {
            var rawColumn = Mathf.Max(0, Mathf.Min(line.RawText.Length, caretIndex - line.StartIndex));
            var prefix = rawColumn > 0 ? line.RawText.Substring(0, rawColumn) : string.Empty;
            var x = gutterWidth + Measure(ExpandTabs(prefix)) - scroll.x;
            var y = line.Y - scroll.y;
            return new Rect(x, y, 2f, _lineHeight);
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

        private void HandlePointerInput(DocumentSession session, CortexShellState state, Event current, PointerContext pointerContext, float gutterWidth)
        {
            if (current == null)
            {
                return;
            }

            if (current.type == EventType.MouseDown && current.button == 0)
            {
                ClearCompletion(state);
                _hasFocus = pointerContext.IsWithinSurface;
                if (!pointerContext.IsWithinSurface)
                {
                    _isDraggingSelection = false;
                    return;
                }

                var hitTest = GetCharacterIndexAt(session, pointerContext.ContentMouse, gutterWidth);
                var selectionAction = ApplyPointerSelection(session, current, hitTest.CharacterIndex);
                LogPointerSelection(session, selectionAction, pointerContext, gutterWidth, hitTest);
                WritePointerSelectionAudit(session, selectionAction, pointerContext, hitTest);

                session.EditorState.ScrollToCaretPending = false;
                _isDraggingSelection = true;
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
            if (session == null || string.IsNullOrEmpty(action))
            {
                return;
            }

            var selection = _editorService.GetPrimarySelection(session);
            var caret = _editorService.GetCaretPosition(session, selection.CaretIndex);
            MMLog.WriteInfo("[Cortex.Editor] Pointer selection. Action=" + action +
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

        private void HandleKeyboardInput(DocumentSession session, CortexShellState state, Event current)
        {
            if (!_hasFocus || current == null || current.type != EventType.KeyDown)
            {
                return;
            }

            var selectionCountBefore = session != null && session.EditorState != null ? session.EditorState.Selections.Count : 0;
            var caretIndexBefore = session != null && session.EditorState != null ? session.EditorState.CaretIndex : 0;
            if (TryHandleCompletionInput(session, state, current))
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
                    QueueCompletionRequest(session, state, true, string.Empty);
                    handled = true;
                }
                else
                {
                    handled = ExecuteCommand(session, commandId, current.shift);
                }
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
                        handled = _editorService.InsertNewLine(session);
                        break;
                }
            }

            if (!handled)
            {
                handled = HandleTextInput(session, current.character);
                if (handled)
                {
                    EditorInteractionLog.WriteEdit("Applied direct keyboard text input to the active document.");
                }
            }

            if (handled)
            {
                LogKeyboardSelectionState(session, commandId, current.character, selectionCountBefore, caretIndexBefore);
                HandleCompletionAfterKey(session, state, current, commandId, previousTextVersion);
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

        private bool ExecuteCommand(DocumentSession session, string commandId, bool extendSelection)
        {
            var handled = false;
            switch (commandId ?? string.Empty)
            {
                case "select.all":
                    _editorService.SelectAll(session);
                    handled = true;
                    break;
                case "edit.undo":
                    handled = _editorService.Undo(session);
                    break;
                case "edit.redo":
                    handled = _editorService.Redo(session);
                    break;
                case "caret.left":
                    _editorService.MoveCaretHorizontal(session, -1, extendSelection);
                    handled = true;
                    break;
                case "caret.right":
                    _editorService.MoveCaretHorizontal(session, 1, extendSelection);
                    handled = true;
                    break;
                case "caret.up":
                    _editorService.MoveCaretVertical(session, -1, extendSelection);
                    handled = true;
                    break;
                case "caret.down":
                    _editorService.MoveCaretVertical(session, 1, extendSelection);
                    handled = true;
                    break;
                case "caret.line.start":
                    _editorService.MoveCaretToLineBoundary(session, true, extendSelection);
                    handled = true;
                    break;
                case "caret.line.end":
                    _editorService.MoveCaretToLineBoundary(session, false, extendSelection);
                    handled = true;
                    break;
                case "caret.document.start":
                    _editorService.MoveCaretToDocumentBoundary(session, true, extendSelection);
                    handled = true;
                    break;
                case "caret.document.end":
                    _editorService.MoveCaretToDocumentBoundary(session, false, extendSelection);
                    handled = true;
                    break;
                case "caret.page.up":
                    _editorService.MoveCaretVertical(session, -16, extendSelection);
                    handled = true;
                    break;
                case "caret.page.down":
                    _editorService.MoveCaretVertical(session, 16, extendSelection);
                    handled = true;
                    break;
                case "edit.backspace":
                    handled = _editorService.Backspace(session);
                    break;
                case "edit.delete":
                    handled = _editorService.Delete(session);
                    break;
                case "edit.indent":
                    handled = _editorService.IndentSelection(session, false);
                    break;
                case "edit.outdent":
                    handled = _editorService.IndentSelection(session, true);
                    break;
                case "edit.newline":
                    handled = _editorService.InsertNewLine(session);
                    break;
                case "multi.above":
                    handled = _editorService.AddCaretOnAdjacentLine(session, -1);
                    break;
                case "multi.below":
                    handled = _editorService.AddCaretOnAdjacentLine(session, 1);
                    break;
                case "multi.clear":
                    handled = _editorService.ClearSecondarySelections(session);
                    break;
                case "move.line.up":
                    handled = _editorService.MoveSelectedLines(session, -1);
                    break;
                case "move.line.down":
                    handled = _editorService.MoveSelectedLines(session, 1);
                    break;
            }

            if (handled)
            {
                EditorInteractionLog.WriteEdit("Executed editor command: " + (commandId ?? string.Empty) + ".");
            }

            return handled;
        }

        private bool TryHandleCompletionInput(DocumentSession session, CortexShellState state, Event current)
        {
            var response = state != null && state.Editor != null ? state.Editor.ActiveCompletionResponse : null;
            if (!_documentLanguageInteractionService.HasCompletionItems(response))
            {
                return false;
            }

            SyncCompletionSelection(response);
            switch (current.keyCode)
            {
                case KeyCode.UpArrow:
                    _completionSelectedIndex = Mathf.Max(0, _completionSelectedIndex - 1);
                    return true;
                case KeyCode.DownArrow:
                    _completionSelectedIndex = Mathf.Min(response.Items.Length - 1, _completionSelectedIndex + 1);
                    return true;
                case KeyCode.PageUp:
                    _completionSelectedIndex = Mathf.Max(0, _completionSelectedIndex - CompletionVisibleItemCount);
                    return true;
                case KeyCode.PageDown:
                    _completionSelectedIndex = Mathf.Min(response.Items.Length - 1, _completionSelectedIndex + CompletionVisibleItemCount);
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

        private void HandleCompletionAfterKey(DocumentSession session, CortexShellState state, Event current, string commandId, int previousTextVersion)
        {
            if (state == null || state.Editor == null || session == null || session.EditorState == null)
            {
                return;
            }

            if (string.Equals(commandId, "edit.complete", StringComparison.Ordinal))
            {
                return;
            }

            var textChanged = session.TextVersion != previousTextVersion;
            if (textChanged)
            {
                if (_documentLanguageInteractionService.ShouldTriggerCompletion(current.character))
                {
                    QueueCompletionRequest(session, state, false, current.character.ToString());
                    return;
                }

                if (_documentLanguageInteractionService.ShouldContinueCompletion(session, session.EditorState.CaretIndex))
                {
                    QueueCompletionRequest(session, state, false, string.Empty);
                    return;
                }

                ClearCompletion(state);
                return;
            }

            if (HasVisibleCompletion(state) &&
                (string.Equals(commandId, "caret.left", StringComparison.Ordinal) ||
                 string.Equals(commandId, "caret.right", StringComparison.Ordinal) ||
                 string.Equals(commandId, "caret.up", StringComparison.Ordinal) ||
                 string.Equals(commandId, "caret.down", StringComparison.Ordinal) ||
                 string.Equals(commandId, "caret.line.start", StringComparison.Ordinal) ||
                 string.Equals(commandId, "caret.line.end", StringComparison.Ordinal) ||
                 string.Equals(commandId, "caret.document.start", StringComparison.Ordinal) ||
                 string.Equals(commandId, "caret.document.end", StringComparison.Ordinal)))
            {
                ClearCompletion(state);
            }
        }

        private void QueueCompletionRequest(DocumentSession session, CortexShellState state, bool explicitInvocation, string triggerCharacter)
        {
            if (state == null || state.Editor == null || session == null || session.EditorState == null || session.EditorState.HasMultipleSelections)
            {
                ClearCompletion(state);
                return;
            }

            var caretIndex = Mathf.Max(0, session.EditorState.CaretIndex);
            var caret = _editorService.GetCaretPosition(session, caretIndex);
            state.Editor.RequestedCompletionKey = _documentLanguageInteractionService.BuildCompletionRequestKey(
                session.FilePath,
                session.TextVersion,
                caretIndex,
                explicitInvocation,
                triggerCharacter);
            state.Editor.RequestedCompletionDocumentPath = session.FilePath ?? string.Empty;
            state.Editor.RequestedCompletionLine = caret.Line + 1;
            state.Editor.RequestedCompletionColumn = caret.Column + 1;
            state.Editor.RequestedCompletionAbsolutePosition = caretIndex;
            state.Editor.RequestedCompletionTriggerCharacter = triggerCharacter ?? string.Empty;
            state.Editor.RequestedCompletionExplicit = explicitInvocation;
            state.Editor.ActiveCompletionKey = string.Empty;
            state.Editor.ActiveCompletionResponse = null;
            _completionStateKey = string.Empty;
            _completionSelectedIndex = -1;
        }

        private bool ApplySelectedCompletion(DocumentSession session, CortexShellState state)
        {
            var response = state != null && state.Editor != null ? state.Editor.ActiveCompletionResponse : null;
            if (!_documentLanguageInteractionService.HasCompletionItems(response))
            {
                return false;
            }

            SyncCompletionSelection(response);
            if (_completionSelectedIndex < 0 || _completionSelectedIndex >= response.Items.Length)
            {
                return false;
            }

            var applied = _documentLanguageInteractionService.ApplyCompletion(
                session,
                _editorService,
                response,
                response.Items[_completionSelectedIndex]);
            ClearCompletion(state);
            return applied;
        }

        private bool HasVisibleCompletion(CortexShellState state)
        {
            return state != null &&
                state.Editor != null &&
                _documentLanguageInteractionService.HasCompletionItems(state.Editor.ActiveCompletionResponse);
        }

        private void ClearCompletion(CortexShellState state)
        {
            _completionStateKey = string.Empty;
            _completionSelectedIndex = -1;
            if (state == null || state.Editor == null)
            {
                return;
            }

            state.Editor.RequestedCompletionKey = string.Empty;
            state.Editor.RequestedCompletionDocumentPath = string.Empty;
            state.Editor.RequestedCompletionLine = 0;
            state.Editor.RequestedCompletionColumn = 0;
            state.Editor.RequestedCompletionAbsolutePosition = -1;
            state.Editor.RequestedCompletionTriggerCharacter = string.Empty;
            state.Editor.RequestedCompletionExplicit = false;
            state.Editor.ActiveCompletionKey = string.Empty;
            state.Editor.ActiveCompletionResponse = null;
        }

        private void SyncCompletionSelection(LanguageServiceCompletionResponse response)
        {
            if (response == null)
            {
                _completionStateKey = string.Empty;
                _completionSelectedIndex = -1;
                return;
            }

            var responseKey = (response.DocumentPath ?? string.Empty) + "|" +
                response.DocumentVersion + "|" +
                (response.Items != null ? response.Items.Length : 0);
            if (!string.Equals(_completionStateKey, responseKey, StringComparison.Ordinal))
            {
                _completionStateKey = responseKey;
                _completionSelectedIndex = 0;
                if (response.Items != null)
                {
                    for (var i = 0; i < response.Items.Length; i++)
                    {
                        if (response.Items[i] != null && response.Items[i].IsPreselected)
                        {
                            _completionSelectedIndex = i;
                            break;
                        }
                    }
                }
            }

            _completionSelectedIndex = _documentLanguageInteractionService.NormalizeSelectedIndex(response, _completionSelectedIndex);
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
            style.normal.textColor = GetClassificationColor(classification);
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

        private static Color GetClassificationColor(string classification)
        {
            var key = (classification ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Contains("comment"))
            {
                return CortexIdeLayout.ParseColor("#6A9955", CortexIdeLayout.GetMutedTextColor());
            }

            if (key.Contains("xml"))
            {
                return CortexIdeLayout.ParseColor("#808080", CortexIdeLayout.GetMutedTextColor());
            }

            if (key.Contains("keyword") || key.Contains("control"))
            {
                return CortexIdeLayout.ParseColor("#569CD6", CortexIdeLayout.GetTextColor());
            }

            if (key.Contains("class") || key.Contains("struct") || key.Contains("interface") || key.Contains("enum") || key.Contains("delegate") || key.Contains("record") || key.Contains("typeparameter"))
            {
                return CortexIdeLayout.ParseColor("#4EC9B0", CortexIdeLayout.GetTextColor());
            }

            if (key.Contains("namespace"))
            {
                return CortexIdeLayout.ParseColor("#C8C8C8", CortexIdeLayout.GetTextColor());
            }

            if (key.Contains("method") || key.Contains("property") || key.Contains("event"))
            {
                return CortexIdeLayout.ParseColor("#DCDCAA", CortexIdeLayout.GetTextColor());
            }

            if (key.Contains("field") || key.Contains("enum member") || key.Contains("constant") || key.Contains("parameter") || key.Contains("local"))
            {
                return CortexIdeLayout.ParseColor("#9CDCFE", CortexIdeLayout.GetTextColor());
            }

            if (key.Contains("string") || key.Contains("char"))
            {
                return CortexIdeLayout.ParseColor("#CE9178", CortexIdeLayout.GetTextColor());
            }

            if (key.Contains("numeric") || key.Contains("number"))
            {
                return CortexIdeLayout.ParseColor("#B5CEA8", CortexIdeLayout.GetTextColor());
            }

            if (key.Contains("preprocessor"))
            {
                return CortexIdeLayout.ParseColor("#C586C0", CortexIdeLayout.GetTextColor());
            }

            return CortexIdeLayout.GetTextColor();
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
    }
}
