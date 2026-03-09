using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using ModAPI.Core;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    internal sealed class CodeViewSurface
    {
        private const float HoverDelaySeconds = 0.18f;
        private const float MenuWidth = 190f;
        private const float MenuItemHeight = 24f;
        private const float TooltipWidth = 420f;
        private const float FoldGlyphWidth = 14f;
        private const int TabSize = 4;
        private const double StickyHoverGraceMs = 220d;

        private readonly Dictionary<string, GUIStyle> _classificationStyles = new Dictionary<string, GUIStyle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _collapsedRegionKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly GUIContent _sharedContent = new GUIContent();
        private string _styleCacheKey = string.Empty;
        private string _layoutCacheKey = string.Empty;
        private CodeViewLayout _layout;
        private GUIStyle _baseStyle;
        private GUIStyle _gutterStyle;
        private GUIStyle _tooltipStyle;
        private GUIStyle _contextMenuStyle;
        private GUIStyle _contextMenuButtonStyle;
        private GUIStyle _contextMenuHeaderStyle;
        private GUIStyle _collapsedHintStyle;
        private GUIStyle _foldGlyphStyle;
        private Texture2D _hoverFill;
        private Texture2D _selectedFill;
        private Texture2D _lineHighlightFill;
        private float _lineHeight = 18f;
        private string _hoverCandidateKey = string.Empty;
        private DateTime _hoverCandidateUtc = DateTime.MinValue;
        private string _selectedTokenKey = string.Empty;
        private string _stickyHoverKey = string.Empty;
        private string _stickyHoverDocumentPath = string.Empty;
        private Rect _stickyHoverAnchorRect = new Rect(0f, 0f, 0f, 0f);
        private Rect _stickyHoverTooltipRect = new Rect(0f, 0f, 0f, 0f);
        private DateTime _stickyHoverKeepAliveUtc = DateTime.MinValue;
        private bool _contextMenuOpen;
        private Vector2 _contextMenuPosition = Vector2.zero;
        private CodeViewToken _contextToken;
        private string _lastDrawError = string.Empty;
        private string _lastFocusedDocumentPath = string.Empty;
        private int _lastFocusedLineNumber = -1;

        public Vector2 Draw(Rect rect, Vector2 scroll, DocumentSession session, CortexShellState state, string themeKey, GUIStyle baseStyle, GUIStyle gutterStyle, GUIStyle tooltipStyle, GUIStyle contextMenuStyle, GUIStyle contextMenuButtonStyle, GUIStyle contextMenuHeaderStyle, float gutterWidth)
        {
            if (session == null)
            {
                ClearStickyHover(state);
                return scroll;
            }

            if (rect.width <= 0f || rect.height <= 0f || GUI.skin == null || GUI.skin.label == null || GUI.skin.button == null || GUI.skin.box == null)
            {
                ClearStickyHover(state);
                return scroll;
            }

            try
            {
                EnsureStyles(themeKey, baseStyle, gutterStyle, tooltipStyle, contextMenuStyle, contextMenuButtonStyle, contextMenuHeaderStyle);
                EnsureLayout(session, themeKey, gutterWidth);
                if (_layout == null)
                {
                    return scroll;
                }

                scroll = EnsureFocusedLineVisible(session, scroll, rect.height);

                var current = Event.current;
                var localMouse = current != null ? current.mousePosition - new Vector2(rect.x, rect.y) : Vector2.zero;
                var hasMouse = current != null && rect.Contains(current.mousePosition);
                var contentMouse = scroll + localMouse;
                var hoveredFoldRegion = hasMouse ? FindFoldRegionAt(contentMouse) : null;
                var hoveredToken = hoveredFoldRegion == null && hasMouse ? FindTokenAt(contentMouse, gutterWidth) : null;
                hoveredToken = CanHoverToken(hoveredToken) ? hoveredToken : null;
                UpdateHoverRequest(session, state, hoveredToken);
                HandlePointerInput(session, state, hoveredToken, hoveredFoldRegion, hasMouse, current, localMouse, rect.size);

                GUI.BeginGroup(rect);
                try
                {
                    var contentRect = new Rect(0f, 0f, Mathf.Max(rect.width - 18f, _layout.ContentWidth), Mathf.Max(rect.height - 18f, _layout.ContentHeight));
                    scroll = GUI.BeginScrollView(new Rect(0f, 0f, rect.width, rect.height), scroll, contentRect);
                    try
                    {
                        DrawVisibleLines(scroll, rect.height, hoveredToken, hoveredFoldRegion, gutterWidth);
                    }
                    finally
                    {
                        GUI.EndScrollView();
                    }

                    DrawTooltip(session, localMouse, hoveredToken, state, rect.size, hasMouse);
                    DrawContextMenu(state, session, current, localMouse, rect.size);
                }
                finally
                {
                    GUI.EndGroup();
                }

                return scroll;
            }
            catch (Exception ex)
            {
                LogDrawFailure(session, ex);
                _contextMenuOpen = false;
                _contextToken = null;
                ClearStickyHover(state);
                Invalidate();
                return scroll;
            }
        }

        public void Invalidate()
        {
            _layoutCacheKey = string.Empty;
            _layout = null;
        }

        private void EnsureStyles(string themeKey, GUIStyle baseStyle, GUIStyle gutterStyle, GUIStyle tooltipStyle, GUIStyle contextMenuStyle, GUIStyle contextMenuButtonStyle, GUIStyle contextMenuHeaderStyle)
        {
            var cacheKey = (themeKey ?? string.Empty) + "|" + (baseStyle != null && baseStyle.font != null ? baseStyle.font.name : string.Empty) + "|" + (baseStyle != null ? baseStyle.fontSize.ToString() : "0");
            if (string.Equals(cacheKey, _styleCacheKey, StringComparison.Ordinal) && _baseStyle != null)
            {
                return;
            }

            _styleCacheKey = cacheKey;
            _classificationStyles.Clear();
            _baseStyle = baseStyle ?? GUI.skin.label;
            _gutterStyle = gutterStyle ?? GUI.skin.label;
            _tooltipStyle = tooltipStyle ?? GUI.skin.box;
            _contextMenuStyle = contextMenuStyle ?? GUI.skin.box;
            _contextMenuButtonStyle = contextMenuButtonStyle ?? GUI.skin.button;
            _contextMenuHeaderStyle = contextMenuHeaderStyle ?? GUI.skin.label;
            _collapsedHintStyle = new GUIStyle(GUI.skin.label);
            _collapsedHintStyle.font = _baseStyle.font;
            _collapsedHintStyle.fontSize = _baseStyle.fontSize;
            _collapsedHintStyle.wordWrap = false;
            _collapsedHintStyle.richText = false;
            _collapsedHintStyle.alignment = TextAnchor.UpperLeft;
            _collapsedHintStyle.clipping = TextClipping.Overflow;
            _collapsedHintStyle.padding = new RectOffset(0, 0, 0, 0);
            _collapsedHintStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyTextColorToAllStates(_collapsedHintStyle, CortexIdeLayout.GetMutedTextColor());

            _foldGlyphStyle = new GUIStyle(GUI.skin.button);
            _foldGlyphStyle.fontSize = 10;
            _foldGlyphStyle.alignment = TextAnchor.MiddleCenter;
            _foldGlyphStyle.padding = new RectOffset(0, 0, 0, 0);
            _foldGlyphStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyBackgroundToAllStates(_foldGlyphStyle, MakeFill(CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.55f)));
            GuiStyleUtil.ApplyTextColorToAllStates(_foldGlyphStyle, CortexIdeLayout.GetMutedTextColor());
            _sharedContent.text = "Ag";
            _lineHeight = _baseStyle.lineHeight > 0f
                ? Mathf.Max(18f, _baseStyle.lineHeight + 2f)
                : Mathf.Max(18f, _baseStyle.CalcSize(_sharedContent).y + 3f);
            _hoverFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.18f));
            _selectedFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.28f));
            _lineHighlightFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetSurfaceColor(), 0.35f));
            Invalidate();
        }

        private void EnsureLayout(DocumentSession session, string themeKey, float gutterWidth)
        {
            var key = BuildLayoutKey(session, themeKey, gutterWidth);
            if (_layout != null && string.Equals(_layoutCacheKey, key, StringComparison.Ordinal))
            {
                return;
            }

            _layoutCacheKey = key;
            _layout = BuildLayout(session, gutterWidth);
        }

        private Vector2 EnsureFocusedLineVisible(DocumentSession session, Vector2 scroll, float viewportHeight)
        {
            if (_layout == null || session == null || session.HighlightedLine <= 0)
            {
                return scroll;
            }

            var documentPath = session.FilePath ?? string.Empty;
            if (string.Equals(_lastFocusedDocumentPath, documentPath, StringComparison.OrdinalIgnoreCase) &&
                _lastFocusedLineNumber == session.HighlightedLine)
            {
                return scroll;
            }

            var targetLine = FindVisibleLineByLineNumber(session.HighlightedLine);
            if (targetLine == null)
            {
                return scroll;
            }

            var targetY = Mathf.Max(0f, targetLine.Y - Mathf.Max(0f, (viewportHeight * 0.35f)));
            var maxScrollY = Mathf.Max(0f, _layout.ContentHeight - viewportHeight);
            scroll.y = Mathf.Clamp(targetY, 0f, maxScrollY);
            _lastFocusedDocumentPath = documentPath;
            _lastFocusedLineNumber = session.HighlightedLine;
            return scroll;
        }

        private CodeViewLayout BuildLayout(DocumentSession session, float gutterWidth)
        {
            var layout = new CodeViewLayout();
            var text = session.Text ?? string.Empty;
            var spans = NormalizeSpans(text.Length, session.LanguageAnalysis != null ? session.LanguageAnalysis.Classifications : null);
            var cursor = 0;
            var line = new CodeViewLine { LineNumber = 1, StartOffset = 0 };
            layout.Lines.Add(line);

            for (var i = 0; i < spans.Count; i++)
            {
                var span = spans[i];
                if (span.Start > cursor)
                {
                    AppendSegment(layout, text, cursor, span.Start - cursor, string.Empty);
                }

                AppendSegment(layout, text, span.Start, span.Length, span.Classification);
                cursor = span.Start + span.Length;
            }

            if (cursor < text.Length)
            {
                AppendSegment(layout, text, cursor, text.Length - cursor, string.Empty);
            }

            BuildFoldRegions(session, layout);
            ApplyCollapsedVisibility(session, layout, gutterWidth);
            return layout;
        }

        private void AppendSegment(CodeViewLayout layout, string text, int start, int length, string classification)
        {
            if (layout == null || string.IsNullOrEmpty(text) || length <= 0)
            {
                return;
            }

            var index = start;
            var remaining = length;
            while (remaining > 0)
            {
                var currentLine = layout.Lines[layout.Lines.Count - 1];
                var segmentLength = 0;
                while (segmentLength < remaining)
                {
                    var c = text[index + segmentLength];
                    if (c == '\r' || c == '\n')
                    {
                        break;
                    }

                    segmentLength++;
                }

                if (segmentLength > 0)
                {
                    var raw = text.Substring(index, segmentLength);
                    AddTokenRuns(currentLine, index, raw, classification ?? string.Empty);
                }

                index += segmentLength;
                remaining -= segmentLength;
                if (remaining <= 0)
                {
                    break;
                }

                var newlineChar = text[index];
                index++;
                remaining--;
                if (newlineChar == '\r' && remaining > 0 && text[index] == '\n')
                {
                    index++;
                    remaining--;
                }

                layout.Lines.Add(new CodeViewLine
                {
                    LineNumber = layout.Lines.Count + 1,
                    StartOffset = index
                });
            }
        }

        private void ApplyCollapsedVisibility(DocumentSession session, CodeViewLayout layout, float gutterWidth)
        {
            var collapsedKeys = GetCollapsedKeys(session != null ? session.FilePath : string.Empty);
            var hiddenLineNumbers = new HashSet<int>();
            layout.VisibleLines.Clear();
            layout.ContentWidth = gutterWidth + 120f;

            for (var lineIndex = 0; lineIndex < layout.Lines.Count; lineIndex++)
            {
                layout.Lines[lineIndex].FoldRegion = null;
                layout.Lines[lineIndex].IsFoldCollapsed = false;
                layout.Lines[lineIndex].CollapsedHintText = string.Empty;
            }

            for (var regionIndex = 0; regionIndex < layout.FoldRegions.Count; regionIndex++)
            {
                var region = layout.FoldRegions[regionIndex];
                var line = FindLine(layout.Lines, region.StartLineNumber);
                if (line == null)
                {
                    continue;
                }

                line.FoldRegion = region;
                line.IsFoldCollapsed = collapsedKeys.Contains(region.Key);
                line.CollapsedHintText = line.IsFoldCollapsed ? BuildCollapsedHint(region) : string.Empty;
                if (!line.IsFoldCollapsed)
                {
                    continue;
                }

                for (var hiddenLine = region.StartLineNumber + 1; hiddenLine <= region.EndLineNumber; hiddenLine++)
                {
                    hiddenLineNumbers.Add(hiddenLine);
                }
            }

            for (var i = 0; i < layout.Lines.Count; i++)
            {
                var line = layout.Lines[i];
                if (hiddenLineNumbers.Contains(line.LineNumber))
                {
                    continue;
                }

                line.Y = layout.VisibleLines.Count * _lineHeight;
                layout.VisibleLines.Add(line);
                var lineWidth = gutterWidth + line.Width + 12f;
                if (!string.IsNullOrEmpty(line.CollapsedHintText))
                {
                    lineWidth += MeasureCollapsedHint(line.CollapsedHintText) + 8f;
                }

                layout.ContentWidth = Mathf.Max(layout.ContentWidth, lineWidth);
            }

            layout.ContentHeight = Mathf.Max(_lineHeight, layout.VisibleLines.Count * _lineHeight + 4f);
        }

        private void DrawVisibleLines(Vector2 scroll, float viewHeight, CodeViewToken hoveredToken, FoldRegion hoveredFoldRegion, float gutterWidth)
        {
            if (_layout == null || _layout.VisibleLines.Count == 0)
            {
                return;
            }

            var firstLine = Mathf.Max(0, Mathf.FloorToInt(scroll.y / _lineHeight));
            var lastLine = Mathf.Min(_layout.VisibleLines.Count - 1, Mathf.CeilToInt((scroll.y + viewHeight) / _lineHeight) + 1);
            var hoveredLine = hoveredToken != null ? hoveredToken.LineNumber : -1;

            for (var i = firstLine; i <= lastLine; i++)
            {
                var line = _layout.VisibleLines[i];
                var lineRect = new Rect(0f, line.Y, _layout.ContentWidth, _lineHeight);
                if (hoveredLine == line.LineNumber || (hoveredFoldRegion != null && hoveredFoldRegion.StartLineNumber == line.LineNumber))
                {
                    GUI.DrawTexture(lineRect, _lineHighlightFill);
                }

                DrawFoldGlyph(line, hoveredFoldRegion);
                GUI.Label(new Rect(FoldGlyphWidth + 2f, line.Y, gutterWidth - FoldGlyphWidth - 10f, _lineHeight), line.LineNumber.ToString("D4"), _gutterStyle);
                for (var tokenIndex = 0; tokenIndex < line.Tokens.Count; tokenIndex++)
                {
                    var token = line.Tokens[tokenIndex];
                    var tokenRect = new Rect(gutterWidth + token.X, line.Y, Mathf.Max(2f, token.Width), _lineHeight);
                    token.LastRect = tokenRect;
                    if (string.Equals(token.Key, _selectedTokenKey, StringComparison.Ordinal))
                    {
                        GUI.DrawTexture(tokenRect, _selectedFill);
                    }
                    else if (hoveredToken != null && string.Equals(token.Key, hoveredToken.Key, StringComparison.Ordinal))
                    {
                        GUI.DrawTexture(tokenRect, _hoverFill);
                    }

                    GUI.Label(tokenRect, token.DisplayText, GetClassificationStyle(token.Classification));
                }

                if (!string.IsNullOrEmpty(line.CollapsedHintText))
                {
                    GUI.Label(
                        new Rect(gutterWidth + line.Width + 6f, line.Y, Mathf.Max(40f, _layout.ContentWidth - gutterWidth - line.Width - 6f), _lineHeight),
                        line.CollapsedHintText,
                        _collapsedHintStyle);
                }
            }
        }

        private void HandlePointerInput(DocumentSession session, CortexShellState state, CodeViewToken hoveredToken, FoldRegion hoveredFoldRegion, bool hasMouse, Event current, Vector2 localMouse, Vector2 viewportSize)
        {
            if (current == null)
            {
                return;
            }

            if (_contextMenuOpen && current.type == EventType.MouseDown && current.button == 0)
            {
                var menuRect = BuildMenuRect(viewportSize);
                if (!menuRect.Contains(localMouse))
                {
                    _contextMenuOpen = false;
                    _contextToken = null;
                }
            }

            if (!hasMouse || current.type != EventType.MouseDown)
            {
                return;
            }

            if (current.button == 0 && hoveredFoldRegion != null)
            {
                ToggleFold(session, hoveredFoldRegion);
                current.Use();
                return;
            }

            if (current.button == 1 && hoveredToken != null)
            {
                _selectedTokenKey = hoveredToken.Key;
                _contextMenuOpen = true;
                _contextMenuPosition = localMouse;
                _contextToken = hoveredToken;
                current.Use();
                return;
            }

            if (current.button != 0 || hoveredToken == null)
            {
                return;
            }

            _selectedTokenKey = hoveredToken.Key;
            if (current.clickCount >= 2 && CanNavigateToDefinition(hoveredToken))
            {
                RequestDefinition(state, session, hoveredToken);
            }

            current.Use();
        }

        private void BuildFoldRegions(DocumentSession session, CodeViewLayout layout)
        {
            layout.FoldRegions.Clear();
            if (layout == null || layout.Lines.Count == 0)
            {
                return;
            }

            var documentPath = session != null ? session.FilePath : string.Empty;
            var regionStack = new Stack<FoldStart>();
            var braceStack = new Stack<FoldStart>();

            for (var i = 0; i < layout.Lines.Count; i++)
            {
                var line = layout.Lines[i];
                var raw = line != null ? (line.RawText ?? string.Empty) : string.Empty;
                var trimmed = raw.Trim();

                if (trimmed.StartsWith("#region", StringComparison.OrdinalIgnoreCase))
                {
                    regionStack.Push(new FoldStart
                    {
                        HeaderLineNumber = line.LineNumber,
                        HeaderText = trimmed
                    });
                    continue;
                }

                if (trimmed.StartsWith("#endregion", StringComparison.OrdinalIgnoreCase))
                {
                    if (regionStack.Count > 0)
                    {
                        var start = regionStack.Pop();
                        if (line.LineNumber > start.HeaderLineNumber)
                        {
                            layout.FoldRegions.Add(new FoldRegion
                            {
                                Key = BuildFoldRegionKey(documentPath, "region", start.HeaderLineNumber, line.LineNumber),
                                Kind = "region",
                                HeaderText = start.HeaderText,
                                StartLineNumber = start.HeaderLineNumber,
                                EndLineNumber = line.LineNumber
                            });
                        }
                    }

                    continue;
                }

                var sanitized = SanitizeLineForBraceScan(raw);
                for (var charIndex = 0; charIndex < sanitized.Length; charIndex++)
                {
                    var c = sanitized[charIndex];
                    if (c == '{')
                    {
                        var headerLineNumber = ResolveBraceHeaderLine(layout.Lines, i, charIndex);
                        braceStack.Push(new FoldStart
                        {
                            HeaderLineNumber = headerLineNumber,
                            HeaderText = FindLine(layout.Lines, headerLineNumber) != null ? FindLine(layout.Lines, headerLineNumber).RawText : raw
                        });
                    }
                    else if (c == '}' && braceStack.Count > 0)
                    {
                        var start = braceStack.Pop();
                        if (line.LineNumber > start.HeaderLineNumber + 1)
                        {
                            layout.FoldRegions.Add(new FoldRegion
                            {
                                Key = BuildFoldRegionKey(documentPath, "brace", start.HeaderLineNumber, line.LineNumber),
                                Kind = "brace",
                                HeaderText = start.HeaderText,
                                StartLineNumber = start.HeaderLineNumber,
                                EndLineNumber = line.LineNumber
                            });
                        }
                    }
                }
            }
        }

        private void DrawFoldGlyph(CodeViewLine line, FoldRegion hoveredFoldRegion)
        {
            if (line == null || line.FoldRegion == null || line.FoldRegion.EndLineNumber <= line.LineNumber)
            {
                return;
            }

            var glyphRect = new Rect(1f, line.Y + 2f, FoldGlyphWidth - 2f, Mathf.Max(12f, _lineHeight - 4f));
            var glyph = line.IsFoldCollapsed ? "+" : "-";
            if (hoveredFoldRegion != null && hoveredFoldRegion.Key == line.FoldRegion.Key)
            {
                GUI.DrawTexture(glyphRect, _hoverFill);
            }

            GUI.Label(glyphRect, glyph, _foldGlyphStyle);
        }

        private void UpdateHoverRequest(DocumentSession session, CortexShellState state, CodeViewToken hoveredToken)
        {
            var hoverKey = hoveredToken != null ? hoveredToken.Key : string.Empty;
            if (!string.Equals(_hoverCandidateKey, hoverKey, StringComparison.Ordinal))
            {
                _hoverCandidateKey = hoverKey;
                _hoverCandidateUtc = DateTime.UtcNow;
                return;
            }

            if (hoveredToken == null || (DateTime.UtcNow - _hoverCandidateUtc).TotalSeconds < HoverDelaySeconds)
            {
                return;
            }

            if (state == null || state.Editor == null)
            {
                return;
            }

            if (string.Equals(state.Editor.RequestedHoverKey, hoverKey, StringComparison.Ordinal))
            {
                return;
            }

            state.Editor.RequestedHoverKey = hoverKey;
            state.Editor.RequestedHoverDocumentPath = session.FilePath ?? string.Empty;
            state.Editor.RequestedHoverLine = hoveredToken.LineNumber;
            state.Editor.RequestedHoverColumn = hoveredToken.Column;
            state.Editor.RequestedHoverTokenText = hoveredToken.RawText.Trim();
        }

        private void RequestDefinition(CortexShellState state, DocumentSession session, CodeViewToken token)
        {
            if (state == null || state.Editor == null || session == null || token == null)
            {
                return;
            }

            if (!CanNavigateToDefinition(token))
            {
                return;
            }

            state.Editor.RequestedDefinitionKey = token.Key + "|" + DateTime.UtcNow.Ticks;
            state.Editor.RequestedDefinitionDocumentPath = session.FilePath ?? string.Empty;
            state.Editor.RequestedDefinitionLine = token.LineNumber;
            state.Editor.RequestedDefinitionColumn = token.Column;
            state.Editor.RequestedDefinitionTokenText = token.RawText.Trim();
        }

        private void DrawTooltip(DocumentSession session, Vector2 localMouse, CodeViewToken hoveredToken, CortexShellState state, Vector2 viewportSize, bool hasMouse)
        {
            LanguageServiceHoverResponse response;
            string hoverKey;
            if (!TryResolveVisibleHover(session, hoveredToken, state, hasMouse, localMouse, out response, out hoverKey))
            {
                ClearVisibleHover(state);
                return;
            }

            var label = response.SymbolDisplay ?? string.Empty;
            var docs = response.DocumentationText ?? string.Empty;
            var tooltipText = string.IsNullOrEmpty(docs) ? label : label + "\n\n" + docs;
            if (string.IsNullOrEmpty(tooltipText))
            {
                ClearStickyHover(state);
                return;
            }

            var size = _tooltipStyle.CalcHeight(new GUIContent(tooltipText), TooltipWidth - 18f);
            var tooltipRect = BuildTooltipRect(localMouse, hoveredToken, hoverKey, viewportSize, Mathf.Min(220f, size + 14f));
            _stickyHoverTooltipRect = tooltipRect;
            SetVisibleHover(state, hoverKey, response);
            GUI.Box(tooltipRect, GUIContent.none, _tooltipStyle);
            GUI.Label(new Rect(tooltipRect.x + 8f, tooltipRect.y + 7f, tooltipRect.width - 16f, tooltipRect.height - 14f), tooltipText, _tooltipStyle);
        }

        private bool TryResolveVisibleHover(
            DocumentSession session,
            CodeViewToken hoveredToken,
            CortexShellState state,
            bool hasMouse,
            Vector2 localMouse,
            out LanguageServiceHoverResponse response,
            out string hoverKey)
        {
            response = null;
            hoverKey = string.Empty;
            if (state == null || state.Editor == null)
            {
                return false;
            }

            response = state.Editor.ActiveHoverResponse;
            hoverKey = state.Editor.ActiveHoverKey ?? string.Empty;
            if (response == null || !response.Success || string.IsNullOrEmpty(hoverKey))
            {
                ClearStickyHover(state);
                return false;
            }

            var documentPath = session != null ? session.FilePath ?? string.Empty : string.Empty;
            if (hoveredToken != null && string.Equals(hoverKey, hoveredToken.Key, StringComparison.Ordinal))
            {
                _stickyHoverKey = hoverKey;
                _stickyHoverDocumentPath = documentPath;
                _stickyHoverAnchorRect = hoveredToken.LastRect;
                RefreshStickyHoverKeepAlive();
                return true;
            }

            if (hoveredToken != null)
            {
                ClearStickyHover(state);
                return false;
            }

            if (!string.Equals(_stickyHoverKey, hoverKey, StringComparison.Ordinal) ||
                !string.Equals(_stickyHoverDocumentPath, documentPath, StringComparison.OrdinalIgnoreCase))
            {
                ClearStickyHover(state);
                return false;
            }

            if (hasMouse && IsPointerWithinHoverSurface(localMouse))
            {
                RefreshStickyHoverKeepAlive();
                return true;
            }

            if (DateTime.UtcNow <= _stickyHoverKeepAliveUtc)
            {
                return true;
            }

            ClearStickyHover(state);
            return false;
        }

        private Rect BuildTooltipRect(Vector2 localMouse, CodeViewToken hoveredToken, string hoverKey, Vector2 viewportSize, float height)
        {
            if (hoveredToken != null && string.Equals(hoverKey, hoveredToken.Key, StringComparison.Ordinal))
            {
                var tooltipRect = new Rect(localMouse.x + 18f, localMouse.y + 18f, TooltipWidth, height);
                tooltipRect.x = Mathf.Min(tooltipRect.x, Mathf.Max(8f, viewportSize.x - tooltipRect.width - 12f));
                tooltipRect.y = Mathf.Min(tooltipRect.y, Mathf.Max(8f, viewportSize.y - tooltipRect.height - 12f));
                return tooltipRect;
            }

            if (_stickyHoverTooltipRect.width > 0f && _stickyHoverTooltipRect.height > 0f)
            {
                var tooltipRect = _stickyHoverTooltipRect;
                tooltipRect.width = TooltipWidth;
                tooltipRect.height = height;
                tooltipRect.x = Mathf.Min(tooltipRect.x, Mathf.Max(8f, viewportSize.x - tooltipRect.width - 12f));
                tooltipRect.y = Mathf.Min(tooltipRect.y, Mathf.Max(8f, viewportSize.y - tooltipRect.height - 12f));
                return tooltipRect;
            }

            var fallbackRect = new Rect(localMouse.x + 18f, localMouse.y + 18f, TooltipWidth, height);
            fallbackRect.x = Mathf.Min(fallbackRect.x, Mathf.Max(8f, viewportSize.x - fallbackRect.width - 12f));
            fallbackRect.y = Mathf.Min(fallbackRect.y, Mathf.Max(8f, viewportSize.y - fallbackRect.height - 12f));
            return fallbackRect;
        }

        private bool IsPointerWithinHoverSurface(Vector2 localMouse)
        {
            return (_stickyHoverAnchorRect.width > 0f && _stickyHoverAnchorRect.height > 0f && _stickyHoverAnchorRect.Contains(localMouse)) ||
                (_stickyHoverTooltipRect.width > 0f && _stickyHoverTooltipRect.height > 0f && _stickyHoverTooltipRect.Contains(localMouse));
        }

        private void RefreshStickyHoverKeepAlive()
        {
            _stickyHoverKeepAliveUtc = DateTime.UtcNow.AddMilliseconds(StickyHoverGraceMs);
        }

        private void ClearStickyHover(CortexShellState state)
        {
            _stickyHoverKey = string.Empty;
            _stickyHoverDocumentPath = string.Empty;
            _stickyHoverAnchorRect = new Rect(0f, 0f, 0f, 0f);
            _stickyHoverTooltipRect = new Rect(0f, 0f, 0f, 0f);
            _stickyHoverKeepAliveUtc = DateTime.MinValue;
            ClearVisibleHover(state);
        }

        private static void SetVisibleHover(CortexShellState state, string hoverKey, LanguageServiceHoverResponse response)
        {
            if (state == null || state.Editor == null)
            {
                return;
            }

            state.Editor.VisibleHoverKey = hoverKey ?? string.Empty;
            state.Editor.VisibleHoverDefinitionDocumentPath = response != null
                ? response.DefinitionDocumentPath ?? string.Empty
                : string.Empty;
        }

        private static void ClearVisibleHover(CortexShellState state)
        {
            if (state == null || state.Editor == null)
            {
                return;
            }

            state.Editor.VisibleHoverKey = string.Empty;
            state.Editor.VisibleHoverDefinitionDocumentPath = string.Empty;
        }

        private void DrawContextMenu(CortexShellState state, DocumentSession session, Event current, Vector2 localMouse, Vector2 viewportSize)
        {
            if (!_contextMenuOpen || _contextToken == null)
            {
                return;
            }

            var menuRect = BuildMenuRect(viewportSize);
            GUI.Box(menuRect, GUIContent.none, _contextMenuStyle);
            GUI.Label(new Rect(menuRect.x + 8f, menuRect.y + 6f, menuRect.width - 16f, 18f), _contextToken.RawText.Trim(), _contextMenuHeaderStyle);

            var buttonY = menuRect.y + 28f;
            if (CanNavigateToDefinition(_contextToken) &&
                GUI.Button(new Rect(menuRect.x + 6f, buttonY, menuRect.width - 12f, MenuItemHeight), "Go To Definition", _contextMenuButtonStyle))
            {
                RequestDefinition(state, session, _contextToken);
                _contextMenuOpen = false;
            }

            buttonY += MenuItemHeight + 4f;
            if (GUI.Button(new Rect(menuRect.x + 6f, buttonY, menuRect.width - 12f, MenuItemHeight), "Copy Token", _contextMenuButtonStyle))
            {
                GUIUtility.systemCopyBuffer = _contextToken.RawText ?? string.Empty;
                _contextMenuOpen = false;
            }

            buttonY += MenuItemHeight + 4f;
            if (GUI.Button(new Rect(menuRect.x + 6f, buttonY, menuRect.width - 12f, MenuItemHeight), "Copy Hover Info", _contextMenuButtonStyle))
            {
                var hover = state != null && state.Editor != null ? state.Editor.ActiveHoverResponse : null;
                GUIUtility.systemCopyBuffer = hover != null && hover.Success
                    ? ((hover.SymbolDisplay ?? string.Empty) + Environment.NewLine + Environment.NewLine + (hover.DocumentationText ?? string.Empty)).Trim()
                    : string.Empty;
                _contextMenuOpen = false;
            }

            if (current != null && current.type == EventType.MouseDown && current.button == 1 && !menuRect.Contains(localMouse))
            {
                _contextMenuOpen = false;
                _contextToken = null;
            }
        }

        private Rect BuildMenuRect(Vector2 viewportSize)
        {
            var height = 28f + (MenuItemHeight * 3f) + 16f;
            var x = Mathf.Min(_contextMenuPosition.x, Mathf.Max(6f, viewportSize.x - MenuWidth - 6f));
            var y = Mathf.Min(_contextMenuPosition.y, Mathf.Max(6f, viewportSize.y - height - 6f));
            return new Rect(x, y, MenuWidth, height);
        }

        private CodeViewToken FindTokenAt(Vector2 contentMouse, float gutterWidth)
        {
            if (_layout == null || _layout.VisibleLines.Count == 0 || contentMouse.x < gutterWidth)
            {
                return null;
            }

            var line = FindVisibleLineAt(contentMouse.y);
            if (line == null)
            {
                return null;
            }

            for (var i = 0; i < line.Tokens.Count; i++)
            {
                var token = line.Tokens[i];
                var tokenStart = gutterWidth + token.X;
                var tokenEnd = tokenStart + Mathf.Max(2f, token.Width);
                if (contentMouse.x >= tokenStart && contentMouse.x <= tokenEnd)
                {
                    return IsInteractiveToken(token) ? token : null;
                }
            }

            return null;
        }

        private FoldRegion FindFoldRegionAt(Vector2 contentMouse)
        {
            if (_layout == null || _layout.VisibleLines.Count == 0 || contentMouse.x > FoldGlyphWidth + 2f)
            {
                return null;
            }

            var line = FindVisibleLineAt(contentMouse.y);
            return line != null ? line.FoldRegion : null;
        }

        private void AddTokenRuns(CodeViewLine line, int absoluteStart, string raw, string classification)
        {
            if (line == null || string.IsNullOrEmpty(raw))
            {
                return;
            }

            if (!string.IsNullOrEmpty(classification))
            {
                AddToken(line, absoluteStart, raw, classification);
                return;
            }

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

                AddToken(line, absoluteStart + offset, raw.Substring(offset, runLength), string.Empty);
                offset += runLength;
            }
        }

        private void AddToken(CodeViewLine line, int absoluteStart, string raw, string classification)
        {
            if (line == null || string.IsNullOrEmpty(raw))
            {
                return;
            }

            line.RawText = (line.RawText ?? string.Empty) + raw;

            var token = new CodeViewToken
            {
                Key = BuildTokenKey(absoluteStart, raw.Length),
                Start = absoluteStart,
                Length = raw.Length,
                Classification = classification ?? string.Empty,
                RawText = raw,
                DisplayText = NormalizeForDisplay(raw),
                LineNumber = line.LineNumber,
                Column = CalculateColumn(line)
            };
            token.Width = MeasureToken(token.DisplayText, token.Classification);
            token.X = line.Width;
            line.Width += token.Width;
            line.Tokens.Add(token);
        }

        private GUIStyle GetClassificationStyle(string classification)
        {
            var key = classification ?? string.Empty;
            GUIStyle style;
            if (_classificationStyles.TryGetValue(key, out style))
            {
                return style;
            }

            style = new GUIStyle(GUI.skin.label);
            style.font = _baseStyle.font;
            style.fontSize = _baseStyle.fontSize;
            style.fontStyle = _baseStyle.fontStyle;
            style.richText = false;
            style.wordWrap = false;
            style.alignment = TextAnchor.UpperLeft;
            style.clipping = TextClipping.Overflow;
            style.padding = new RectOffset(0, 0, 0, 0);
            style.margin = new RectOffset(0, 0, 0, 0);
            style.border = new RectOffset(0, 0, 0, 0);
            style.overflow = new RectOffset(0, 0, 0, 0);
            style.normal.background = null;
            style.hover.background = null;
            style.active.background = null;
            style.focused.background = null;
            style.onNormal.background = null;
            style.onHover.background = null;
            style.onActive.background = null;
            style.onFocused.background = null;
            GuiStyleUtil.ApplyTextColorToAllStates(style, GetClassificationColor(key));
            _classificationStyles[key] = style;
            return style;
        }

        private float MeasureToken(string displayText, string classification)
        {
            _sharedContent.text = string.IsNullOrEmpty(displayText) ? " " : displayText;
            return GetClassificationStyle(classification).CalcSize(_sharedContent).x;
        }

        private static List<NormalizedSpan> NormalizeSpans(int textLength, LanguageServiceClassifiedSpan[] spans)
        {
            var result = new List<NormalizedSpan>();
            if (spans == null || spans.Length == 0 || textLength <= 0)
            {
                return result;
            }

            for (var i = 0; i < spans.Length; i++)
            {
                var span = spans[i];
                if (span == null || span.Length <= 0)
                {
                    continue;
                }

                var start = Mathf.Clamp(span.Start, 0, textLength);
                var end = Mathf.Clamp(span.Start + span.Length, 0, textLength);
                if (end <= start)
                {
                    continue;
                }

                result.Add(new NormalizedSpan
                {
                    Start = start,
                    Length = end - start,
                    Classification = span.Classification ?? string.Empty
                });
            }

            result.Sort(delegate(NormalizedSpan left, NormalizedSpan right)
            {
                if (left.Start != right.Start)
                {
                    return left.Start.CompareTo(right.Start);
                }

                return right.Length.CompareTo(left.Length);
            });

            var normalized = new List<NormalizedSpan>(result.Count);
            var cursor = -1;
            for (var i = 0; i < result.Count; i++)
            {
                var span = result[i];
                if (span.Start < cursor)
                {
                    var adjustedLength = span.Length - (cursor - span.Start);
                    if (adjustedLength <= 0)
                    {
                        continue;
                    }

                    span.Start = cursor;
                    span.Length = adjustedLength;
                }

                normalized.Add(span);
                cursor = span.Start + span.Length;
            }

            return normalized;
        }

        private static string NormalizeForDisplay(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(raw.Length + 8);
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (c == '\t')
                {
                    for (var spaceIndex = 0; spaceIndex < TabSize; spaceIndex++)
                    {
                        builder.Append(' ');
                    }
                }
                else
                {
                    builder.Append(c);
                }
            }

            return builder.ToString();
        }

        private static int CalculateColumn(CodeViewLine line)
        {
            if (line == null || line.Tokens.Count == 0)
            {
                return 1;
            }

            var last = line.Tokens[line.Tokens.Count - 1];
            return last.Column + last.Length;
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

        private static bool IsInteractiveToken(CodeViewToken token)
        {
            return token != null && !string.IsNullOrEmpty(token.RawText) && token.RawText.Trim().Length > 0;
        }

        private static bool CanHoverToken(CodeViewToken token)
        {
            return IsInteractiveToken(token) &&
                IsHoverClassification(token.Classification, token.RawText);
        }

        private static bool CanNavigateToDefinition(CodeViewToken token)
        {
            if (!IsInteractiveToken(token) || !IsDefinitionClassification(token.Classification))
            {
                return false;
            }

            var key = (token.Classification ?? string.Empty).Trim().ToLowerInvariant();
            return key.IndexOf("local", StringComparison.OrdinalIgnoreCase) < 0 &&
                key.IndexOf("parameter", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsHoverClassification(string classification, string rawText)
        {
            var key = NormalizeClassification(classification);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (key.Contains("operator") ||
                key.Contains("punctuation") ||
                key.Contains("comment") ||
                key.Contains("xml") ||
                key.Contains("preprocessor") ||
                key.Contains("string") ||
                key.Contains("char") ||
                key.Contains("numeric") ||
                key.Contains("number"))
            {
                return false;
            }

            if (key.Contains("keyword"))
            {
                return IsPredefinedTypeKeyword(rawText);
            }

            return key.Contains("class") ||
                key.Contains("struct") ||
                key.Contains("interface") ||
                key.Contains("enum") ||
                key.Contains("delegate") ||
                key.Contains("record") ||
                key.Contains("namespace") ||
                key.Contains("method") ||
                key.Contains("property") ||
                key.Contains("event") ||
                key.Contains("field") ||
                key.Contains("constant") ||
                key.Contains("enum member") ||
                key.Contains("typeparameter") ||
                key.Contains("local") ||
                key.Contains("parameter");
        }

        private static bool IsDefinitionClassification(string classification)
        {
            var key = NormalizeClassification(classification);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            if (key.Contains("keyword") ||
                key.Contains("operator") ||
                key.Contains("punctuation") ||
                key.Contains("comment") ||
                key.Contains("xml") ||
                key.Contains("preprocessor") ||
                key.Contains("string") ||
                key.Contains("char") ||
                key.Contains("numeric") ||
                key.Contains("number"))
            {
                return false;
            }

            return key.Contains("class") ||
                key.Contains("struct") ||
                key.Contains("interface") ||
                key.Contains("enum") ||
                key.Contains("delegate") ||
                key.Contains("record") ||
                key.Contains("namespace") ||
                key.Contains("method") ||
                key.Contains("property") ||
                key.Contains("event") ||
                key.Contains("field") ||
                key.Contains("constant") ||
                key.Contains("enum member") ||
                key.Contains("typeparameter");
        }

        private static string NormalizeClassification(string classification)
        {
            return (classification ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool IsPredefinedTypeKeyword(string rawText)
        {
            var token = (rawText ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(token))
            {
                return false;
            }

            switch (token)
            {
                case "bool":
                case "byte":
                case "sbyte":
                case "short":
                case "ushort":
                case "int":
                case "uint":
                case "long":
                case "ulong":
                case "nint":
                case "nuint":
                case "float":
                case "double":
                case "decimal":
                case "char":
                case "string":
                case "object":
                case "dynamic":
                case "void":
                    return true;
                default:
                    return false;
            }
        }

        private static Color GetClassificationColor(string classification)
        {
            var key = NormalizeClassification(classification);
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

        private HashSet<string> GetCollapsedKeys(string documentPath)
        {
            var key = documentPath ?? string.Empty;
            HashSet<string> collapsed;
            if (!_collapsedRegionKeys.TryGetValue(key, out collapsed))
            {
                collapsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                _collapsedRegionKeys[key] = collapsed;
            }

            return collapsed;
        }

        private void ToggleFold(DocumentSession session, FoldRegion region)
        {
            if (session == null || region == null)
            {
                return;
            }

            var collapsed = GetCollapsedKeys(session.FilePath);
            if (!collapsed.Add(region.Key))
            {
                collapsed.Remove(region.Key);
            }

            Invalidate();
        }

        private CodeViewLine FindVisibleLineAt(float contentY)
        {
            if (_layout == null || _layout.VisibleLines.Count == 0)
            {
                return null;
            }

            var lineIndex = Mathf.FloorToInt(contentY / _lineHeight);
            if (lineIndex < 0 || lineIndex >= _layout.VisibleLines.Count)
            {
                return null;
            }

            return _layout.VisibleLines[lineIndex];
        }

        private CodeViewLine FindVisibleLineByLineNumber(int lineNumber)
        {
            if (_layout == null || _layout.VisibleLines.Count == 0 || lineNumber <= 0)
            {
                return null;
            }

            for (var i = 0; i < _layout.VisibleLines.Count; i++)
            {
                if (_layout.VisibleLines[i].LineNumber == lineNumber)
                {
                    return _layout.VisibleLines[i];
                }
            }

            return null;
        }

        private void LogDrawFailure(DocumentSession session, Exception ex)
        {
            var sessionLabel = session != null ? (session.FilePath ?? "(unknown)") : "(no session)";
            var errorKey = sessionLabel + "|" + ex.GetType().FullName + "|" + ex.Message;
            if (string.Equals(_lastDrawError, errorKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastDrawError = errorKey;
            MMLog.WriteError("[Cortex.CodeView] Draw failed for " + sessionLabel + ": " + ex);
        }

        private static CodeViewLine FindLine(List<CodeViewLine> lines, int lineNumber)
        {
            if (lines == null || lineNumber <= 0)
            {
                return null;
            }

            var index = lineNumber - 1;
            return index >= 0 && index < lines.Count ? lines[index] : null;
        }

        private float MeasureCollapsedHint(string hint)
        {
            _sharedContent.text = string.IsNullOrEmpty(hint) ? string.Empty : hint;
            return _collapsedHintStyle.CalcSize(_sharedContent).x;
        }

        private static string BuildCollapsedHint(FoldRegion region)
        {
            var lineCount = Mathf.Max(0, region.EndLineNumber - region.StartLineNumber);
            return " ... " + Mathf.Max(1, lineCount) + " line(s)";
        }

        private static string BuildFoldRegionKey(string documentPath, string kind, int startLineNumber, int endLineNumber)
        {
            return (documentPath ?? string.Empty) + "|" + (kind ?? string.Empty) + "|" + startLineNumber + "|" + endLineNumber;
        }

        private static string SanitizeLineForBraceScan(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(raw.Length);
            var inString = false;
            var quoteChar = '\0';
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (!inString && c == '/' && i + 1 < raw.Length && raw[i + 1] == '/')
                {
                    break;
                }

                if ((c == '"' || c == '\'') && (i == 0 || raw[i - 1] != '\\'))
                {
                    if (inString && quoteChar == c)
                    {
                        inString = false;
                    }
                    else if (!inString)
                    {
                        inString = true;
                        quoteChar = c;
                    }
                }

                builder.Append(inString ? ' ' : c);
            }

            return builder.ToString();
        }

        private static int ResolveBraceHeaderLine(List<CodeViewLine> lines, int currentLineIndex, int braceIndex)
        {
            if (lines == null || currentLineIndex < 0 || currentLineIndex >= lines.Count)
            {
                return currentLineIndex + 1;
            }

            var currentLine = lines[currentLineIndex];
            var raw = currentLine.RawText ?? string.Empty;
            var beforeBrace = braceIndex > 0 && braceIndex <= raw.Length ? raw.Substring(0, braceIndex) : string.Empty;
            if (!IsNullOrWhitespace(beforeBrace))
            {
                return currentLine.LineNumber;
            }

            for (var index = currentLineIndex - 1; index >= 0; index--)
            {
                var candidate = lines[index];
                if (candidate != null && !IsNullOrWhitespace(candidate.RawText))
                {
                    return candidate.LineNumber;
                }
            }

            return currentLine.LineNumber;
        }

        private static string BuildLayoutKey(DocumentSession session, string themeKey, float gutterWidth)
        {
            if (session == null)
            {
                return string.Empty;
            }

            var classificationCount = session.LanguageAnalysis != null && session.LanguageAnalysis.Classifications != null
                ? session.LanguageAnalysis.Classifications.Length
                : 0;
            return (session.FilePath ?? string.Empty) + "|" +
                   (session.Text != null ? session.Text.Length.ToString() : "0") + "|" +
                   session.LastLanguageAnalysisUtc.Ticks + "|" +
                   classificationCount + "|" +
                   (themeKey ?? string.Empty) + "|" +
                   gutterWidth.ToString("F2");
        }

        private static string BuildTokenKey(int start, int length)
        {
            return start + ":" + length;
        }

        private static Texture2D MakeFill(Color color)
        {
            var texture = new Texture2D(1, 1);
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }

        private static bool IsNullOrWhitespace(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private sealed class CodeViewLayout
        {
            public readonly List<CodeViewLine> Lines = new List<CodeViewLine>();
            public readonly List<CodeViewLine> VisibleLines = new List<CodeViewLine>();
            public readonly List<FoldRegion> FoldRegions = new List<FoldRegion>();
            public float ContentWidth = 120f;
            public float ContentHeight = 40f;
        }

        private sealed class CodeViewLine
        {
            public int LineNumber;
            public int StartOffset;
            public string RawText;
            public float Y;
            public float Width;
            public FoldRegion FoldRegion;
            public bool IsFoldCollapsed;
            public string CollapsedHintText;
            public readonly List<CodeViewToken> Tokens = new List<CodeViewToken>();
        }

        private sealed class CodeViewToken
        {
            public string Key;
            public int Start;
            public int Length;
            public string Classification;
            public string RawText;
            public string DisplayText;
            public int LineNumber;
            public int Column;
            public float X;
            public float Width;
            public Rect LastRect;
        }

        private struct NormalizedSpan
        {
            public int Start;
            public int Length;
            public string Classification;
        }

        private sealed class FoldRegion
        {
            public string Key;
            public string Kind;
            public string HeaderText;
            public int StartLineNumber;
            public int EndLineNumber;
        }

        private struct FoldStart
        {
            public int HeaderLineNumber;
            public string HeaderText;
        }

        private enum CharacterKind
        {
            Word,
            Whitespace,
            Punctuation
        }
    }
}
