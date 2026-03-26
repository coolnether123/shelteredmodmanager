using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.LanguageService.Protocol;
using Cortex.Services;
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
        private const double StickyHoverGraceMs = 700d;

        private readonly Dictionary<string, GUIStyle> _classificationStyles = new Dictionary<string, GUIStyle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _collapsedRegionKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly GUIContent _sharedContent = new GUIContent();
        private readonly EditorSemanticPopupSurface _semanticPopupSurface = new EditorSemanticPopupSurface();
        private readonly EditorCommandContextFactory _commandContextFactory = new EditorCommandContextFactory();
        private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();
        private readonly EditorSelectionInspectionService _selectionInspectionService = new EditorSelectionInspectionService();
        private readonly EditorClassificationPresentationService _classificationPresentationService = new EditorClassificationPresentationService();
        private readonly EditorContextMenuService _contextMenuService = new EditorContextMenuService();
        private readonly EditorOverlayInteractionService _overlayInteractionService = new EditorOverlayInteractionService();
        private readonly EditorMethodInspectorService _methodInspectorService = new EditorMethodInspectorService();
        private readonly EditorMethodTargetOutlineService _methodTargetOutlineService = new EditorMethodTargetOutlineService();
        private readonly EditorMethodInspectorSurface _methodInspectorSurface = new EditorMethodInspectorSurface();
        private readonly PopupMenuSurface _popupMenuSurface = new PopupMenuSurface();
        private readonly HoverTooltipPresenter _tooltipPresenter = new HoverTooltipPresenter();
        private readonly EditorFallbackColoringService _fallbackColoringService = new EditorFallbackColoringService();
        private readonly List<PopupMenuItem> _popupMenuItems = new List<PopupMenuItem>();
        private string _styleCacheKey = string.Empty;
        private string _layoutCacheKey = string.Empty;
        private CodeViewLayout _layout;
        private GUIStyle _baseStyle;
        private GUIStyle _gutterStyle;
        private GUIStyle _tooltipStyle;
        private GUIStyle _tooltipSignatureStyle;
        private GUIStyle _tooltipPathStyle;
        private GUIStyle _tooltipMetaStyle;
        private GUIStyle _tooltipLinkStyle;
        private GUIStyle _tooltipDetailStyle;
        private GUIStyle _contextMenuStyle;
        private GUIStyle _contextMenuButtonStyle;
        private GUIStyle _contextMenuHeaderStyle;
        private GUIStyle _collapsedHintStyle;
        private GUIStyle _foldGlyphStyle;
        private Texture2D _hoverFill;
        private Texture2D _hoverOutlineFill;
        private Texture2D _relatedSelectionFill;
        private Texture2D _selectedFill;
        private Texture2D _selectionOutlineFill;
        private Texture2D _lineHighlightFill;
        private Texture2D _navigationLineFill;
        private Texture2D _tooltipUnderlineFill;
        private Texture2D _methodTargetFill;
        private Texture2D _methodTargetOutlineFill;
        private Texture2D _methodTargetGlowFill;
        private float _lineHeight = 18f;
        private string _hoverCandidateKey = string.Empty;
        private DateTime _hoverCandidateUtc = DateTime.MinValue;
        private string _selectedTokenKey = string.Empty;
        private string _selectedTokenText = string.Empty;
        private string _selectedTokenClassification = string.Empty;
        private string _selectedDocumentPath = string.Empty;
        private string _stickyHoverKey = string.Empty;
        private string _stickyHoverDocumentPath = string.Empty;
        private Rect _stickyHoverAnchorRect = new Rect(0f, 0f, 0f, 0f);
        private Rect _stickyHoverTooltipRect = new Rect(0f, 0f, 0f, 0f);
        private Vector2 _lastValidTooltipViewport = Vector2.zero;
        private DateTime _stickyHoverKeepAliveUtc = DateTime.MinValue;
        private string _lastVisibleHoverLogKey = string.Empty;
        private string _lastHoverPlacementLogKey = string.Empty;
        private string _lastHoverClearLogKey = string.Empty;
        private string _lastHoverRetargetLogKey = string.Empty;
        private string _pressedTooltipPartKey = string.Empty;
        private bool _contextMenuOpen;
        private Vector2 _contextMenuPosition = Vector2.zero;
        private Rect _lastContextMenuRect = new Rect(0f, 0f, 0f, 0f);
        private Rect _lastMethodInspectorRect = new Rect(0f, 0f, 0f, 0f);
        private EditorCommandInvocation _contextInvocation;
        private string _lastDrawError = string.Empty;
        private string _lastFocusedDocumentPath = string.Empty;
        private int _lastFocusedLineNumber = -1;

        public Vector2 Draw(
            Rect rect,
            Vector2 scroll,
            DocumentSession session,
            IDocumentService documentService,
            CortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            CortexShellState state,
            string themeKey,
            GUIStyle baseStyle,
            GUIStyle gutterStyle,
            GUIStyle tooltipStyle,
            GUIStyle contextMenuStyle,
            GUIStyle contextMenuButtonStyle,
            GUIStyle contextMenuHeaderStyle,
            Rect blockedRect,
            float gutterWidth,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyPatchInspectionService,
            HarmonyPatchResolutionService harmonyPatchResolutionService,
            HarmonyPatchDisplayService harmonyPatchDisplayService,
            HarmonyPatchGenerationService harmonyPatchGenerationService)
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
                var mouseBlocked = current != null && blockedRect.width > 0f && blockedRect.height > 0f && blockedRect.Contains(current.mousePosition);
                var hasMouse = current != null && rect.Contains(current.mousePosition) && !mouseBlocked;
                if (_contextMenuOpen && _popupMenuItems.Count > 0)
                {
                    _lastContextMenuRect = _popupMenuSurface.PredictMenuRect(_contextMenuPosition, rect.size, _popupMenuItems);
                }
                PreHandleContextMenuInput(current, localMouse);
                var pointerOnContextMenu = _contextMenuOpen && _overlayInteractionService.IsPointerWithin(_lastContextMenuRect, localMouse);
                var activeMethodInspectorTarget = state != null &&
                    state.Editor != null &&
                    state.Editor.MethodInspector != null &&
                    state.Editor.MethodInspector.Invocation != null
                        ? state.Editor.MethodInspector.Invocation.Target
                        : null;
                var predictedMethodInspectorRect = _methodInspectorSurface.PredictRect(
                    state,
                    session.FilePath,
                    GetMethodInspectorViewportAnchorRect(session, activeMethodInspectorTarget, scroll, gutterWidth),
                    rect.size);
                _lastMethodInspectorRect = predictedMethodInspectorRect;
                var pointerOnMethodInspector = _overlayInteractionService.IsPointerWithin(predictedMethodInspectorRect, localMouse);
                _overlayInteractionService.TraceScrollOwner("decompiled-editor", current, pointerOnMethodInspector, pointerOnContextMenu);
                _methodInspectorSurface.TryHandlePreDrawInput(current, predictedMethodInspectorRect, localMouse);
                var pointerOnTooltip = hasMouse && IsPointerWithinTooltip(localMouse);
                var pointerOnHoverSurface = hasMouse && (pointerOnTooltip || pointerOnMethodInspector || IsPointerWithinHoverSurface(localMouse));
                var editorHoverActive = hasMouse && !pointerOnHoverSurface;
                var contentMouse = scroll + localMouse;
                var hoveredFoldRegion = editorHoverActive ? FindFoldRegionAt(contentMouse) : null;
                var hoveredMethodTarget = hoveredFoldRegion == null && editorHoverActive && IsMethodTargetSelectionMode()
                    ? FindMethodTargetAt(session, contentMouse, gutterWidth)
                    : null;
                var hoveredToken = hoveredFoldRegion == null && editorHoverActive ? FindTokenAt(contentMouse, gutterWidth) : null;
                hoveredToken = CanHoverToken(hoveredToken) ? hoveredToken : null;
                UpdateHoverRequest(session, state, hoveredToken, editorHoverActive && !pointerOnTooltip);
                HandlePointerInput(session, state, hoveredToken, hoveredMethodTarget, hoveredFoldRegion, editorHoverActive, current, localMouse, rect.size, commandRegistry, contributionRegistry, pointerOnMethodInspector, pointerOnContextMenu);

                GUI.BeginGroup(rect);
                try
                {
                    var contentRect = new Rect(0f, 0f, Mathf.Max(rect.width - 18f, _layout.ContentWidth), Mathf.Max(rect.height - 18f, _layout.ContentHeight));
                    var preserveEditorScroll = _overlayInteractionService.ShouldPreserveEditorScroll(current, _contextMenuOpen, pointerOnMethodInspector);
                    var scrollBeforeDraw = scroll;
                    try
                    {
                        scroll = GUI.BeginScrollView(new Rect(0f, 0f, rect.width, rect.height), scroll, contentRect);
                        try
                        {
                            DrawVisibleLines(session, scroll, rect.height, hoveredToken, hoveredMethodTarget, hoveredFoldRegion, gutterWidth);
                        }
                        finally
                        {
                            GUI.EndScrollView();
                        }
                    }
                    finally
                    {
                        if (preserveEditorScroll)
                        {
                            scroll = scrollBeforeDraw;
                        }
                    }

                    DrawTooltip(session, scroll, localMouse, hoveredToken, navigationService, state, rect.size, hasMouse);
                    DrawHarmonyBadge(
                        session,
                        state,
                        rect.size,
                        commandRegistry,
                        projectCatalog,
                        loadedModCatalog,
                        sourceLookupIndex,
                        harmonyPatchInspectionService,
                        harmonyPatchResolutionService,
                        harmonyPatchDisplayService);
                    _lastMethodInspectorRect = _methodInspectorSurface.Draw(
                        state,
                        session,
                        session.FilePath,
                        GetMethodInspectorViewportAnchorRect(
                            session,
                            state != null && state.Editor != null && state.Editor.MethodInspector != null && state.Editor.MethodInspector.Invocation != null
                                ? state.Editor.MethodInspector.Invocation.Target
                                : null,
                            scroll,
                            gutterWidth),
                        rect.size,
                        commandRegistry,
                        contributionRegistry,
                        _contextMenuStyle,
                        _contextMenuButtonStyle,
                        _contextMenuHeaderStyle,
                        documentService,
                        projectCatalog,
                        loadedModCatalog,
                        sourceLookupIndex,
                        harmonyPatchInspectionService,
                        harmonyPatchResolutionService,
                        harmonyPatchDisplayService,
                        harmonyPatchGenerationService);
                    _semanticPopupSurface.DrawQuickActions(state, GetViewportAnchorRect(state != null && state.Semantic != null ? state.Semantic.QuickActionsTarget : null, scroll, gutterWidth), rect.size, commandRegistry);
                    _semanticPopupSurface.DrawRename(state, GetViewportAnchorRect(state != null && state.Editor != null ? state.Editor.ActiveRenameTarget : null, scroll, gutterWidth), rect.size, commandRegistry);
                    _semanticPopupSurface.DrawPeek(state, GetViewportAnchorRect(state != null && state.Editor != null ? state.Editor.ActivePeekTarget : null, scroll, gutterWidth), rect.size);
                    DrawContextMenu(state, current, localMouse, rect.size, commandRegistry);
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
                CloseContextMenu();
                ClearStickyHover(state);
                Invalidate();
                return scroll;
            }
        }

        public void Invalidate()
        {
            _layoutCacheKey = string.Empty;
            _layout = null;
            CloseContextMenu();
        }

        private void DrawHarmonyBadge(
            DocumentSession session,
            CortexShellState state,
            Vector2 viewportSize,
            ICommandRegistry commandRegistry,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyPatchInspectionService,
            HarmonyPatchResolutionService harmonyPatchResolutionService,
            HarmonyPatchDisplayService harmonyPatchDisplayService)
        {
            if (session == null ||
                state == null ||
                commandRegistry == null ||
                harmonyPatchInspectionService == null ||
                harmonyPatchResolutionService == null ||
                harmonyPatchDisplayService == null ||
                !CortexModuleUtil.IsDecompilerDocumentPath(state, session.FilePath))
            {
                return;
            }

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!harmonyPatchResolutionService.TryResolveFromDocument(state, sourceLookupIndex, projectCatalog, session, out resolvedTarget, out reason) ||
                resolvedTarget == null ||
                resolvedTarget.InspectionRequest == null)
            {
                return;
            }

            string statusMessage;
            var summary = harmonyPatchInspectionService.GetCachedSummary(
                state,
                resolvedTarget.InspectionRequest,
                loadedModCatalog,
                projectCatalog,
                true,
                out statusMessage);
            if (summary == null || !summary.IsPatched)
            {
                return;
            }

            var label = harmonyPatchDisplayService.BuildBadgeText(summary);
            if (string.IsNullOrEmpty(label))
            {
                label = "H";
            }

            var buttonWidth = Mathf.Max(72f, GUI.skin.button.CalcSize(new GUIContent(label)).x + 18f);
            var buttonRect = new Rect(
                Mathf.Max(8f, viewportSize.x - buttonWidth - 16f),
                8f,
                buttonWidth,
                24f);
            var content = new GUIContent(label, harmonyPatchDisplayService.BuildCountBreakdown(summary.Counts));
            if (GUI.Button(buttonRect, content))
            {
                state.Harmony.ActiveInspectionRequest = resolvedTarget.InspectionRequest;
                state.Harmony.ActiveSummaryKey = harmonyPatchInspectionService.BuildKey(resolvedTarget.InspectionRequest);
                state.Harmony.ActiveSummary = summary;
                state.Harmony.ResolutionFailureReason = string.Empty;
                state.Workbench.AssignHost(CortexWorkbenchIds.HarmonyContainer, WorkbenchHostLocation.SecondarySideHost);
                commandRegistry.Execute("cortex.window.harmony", new CommandExecutionContext
                {
                    ActiveContainerId = state.Workbench.FocusedContainerId,
                    ActiveDocumentId = state.Documents.ActiveDocumentPath,
                    FocusedRegionId = state.Workbench.FocusedContainerId
                });
            }
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
            _tooltipSignatureStyle = new GUIStyle(_baseStyle);
            _tooltipSignatureStyle.wordWrap = false;
            _tooltipSignatureStyle.clipping = TextClipping.Overflow;
            _tooltipSignatureStyle.padding = new RectOffset(0, 0, 0, 0);
            _tooltipSignatureStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyTextColorToAllStates(_tooltipSignatureStyle, CortexIdeLayout.GetTextColor());
            _tooltipPathStyle = new GUIStyle(_baseStyle);
            _tooltipPathStyle.wordWrap = false;
            _tooltipPathStyle.clipping = TextClipping.Clip;
            _tooltipPathStyle.padding = new RectOffset(0, 0, 0, 0);
            _tooltipPathStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyTextColorToAllStates(_tooltipPathStyle, CortexIdeLayout.GetMutedTextColor());
            _tooltipMetaStyle = new GUIStyle(_baseStyle);
            _tooltipMetaStyle.wordWrap = true;
            _tooltipMetaStyle.clipping = TextClipping.Clip;
            _tooltipMetaStyle.padding = new RectOffset(0, 0, 0, 0);
            _tooltipMetaStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyTextColorToAllStates(_tooltipMetaStyle, CortexIdeLayout.Blend(CortexIdeLayout.GetTextColor(), CortexIdeLayout.GetMutedTextColor(), 0.38f));
            _tooltipLinkStyle = new GUIStyle(_tooltipSignatureStyle);
            GuiStyleUtil.ApplyTextColorToAllStates(_tooltipLinkStyle, CortexIdeLayout.GetAccentColor());
            _tooltipDetailStyle = new GUIStyle(_baseStyle);
            _tooltipDetailStyle.wordWrap = true;
            _tooltipDetailStyle.clipping = TextClipping.Clip;
            _tooltipDetailStyle.padding = new RectOffset(0, 0, 0, 0);
            _tooltipDetailStyle.margin = new RectOffset(0, 0, 0, 0);
            GuiStyleUtil.ApplyTextColorToAllStates(_tooltipDetailStyle, CortexIdeLayout.GetTextColor());
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
            _hoverOutlineFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.78f));
            _relatedSelectionFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.14f));
            _selectedFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.28f));
            _selectionOutlineFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.95f));
            _lineHighlightFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetSurfaceColor(), 0.35f));
            _navigationLineFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.16f));
            _tooltipUnderlineFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.9f));
            _methodTargetFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.10f));
            _methodTargetOutlineFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.94f));
            _methodTargetGlowFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), Color.white, 0.18f), 0.16f));
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
            var documentIdentity = session != null ? session.FilePath ?? string.Empty : string.Empty;
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
                    AppendSegment(layout, documentIdentity, text, cursor, span.Start - cursor, string.Empty);
                }

                AppendSegment(layout, documentIdentity, text, span.Start, span.Length, span.Classification);
                cursor = span.Start + span.Length;
            }

            if (cursor < text.Length)
            {
                AppendSegment(layout, documentIdentity, text, cursor, text.Length - cursor, string.Empty);
            }

            BuildFoldRegions(session, layout);
            ApplyCollapsedVisibility(session, layout, gutterWidth);
            return layout;
        }

        private void AppendSegment(CodeViewLayout layout, string documentIdentity, string text, int start, int length, string classification)
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
                    AddTokenRuns(currentLine, documentIdentity, index, raw, classification ?? string.Empty);
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

        private void DrawVisibleLines(DocumentSession session, Vector2 scroll, float viewHeight, CodeViewToken hoveredToken, EditorMethodTargetOutline hoveredMethodTarget, FoldRegion hoveredFoldRegion, float gutterWidth)
        {
            if (_layout == null || _layout.VisibleLines.Count == 0)
            {
                return;
            }

            var showMethodTargets = IsMethodTargetSelectionMode();
            var firstLine = Mathf.Max(0, Mathf.FloorToInt(scroll.y / _lineHeight));
            var lastLine = Mathf.Min(_layout.VisibleLines.Count - 1, Mathf.CeilToInt((scroll.y + viewHeight) / _lineHeight) + 1);
            var hoveredLine = hoveredToken != null ? hoveredToken.LineNumber : -1;
            var highlightedLine = session != null ? session.HighlightedLine : 0;
            if (showMethodTargets)
            {
                DrawMethodTargetOutlines(session, firstLine, lastLine, gutterWidth, hoveredMethodTarget);
            }

            for (var i = firstLine; i <= lastLine; i++)
            {
                var line = _layout.VisibleLines[i];
                var lineRect = new Rect(0f, line.Y, _layout.ContentWidth, _lineHeight);
                var isNavigationLine = highlightedLine > 0 && highlightedLine == line.LineNumber;
                if (isNavigationLine)
                {
                    GUI.DrawTexture(lineRect, _navigationLineFill);
                }

                if (hoveredLine == line.LineNumber || (hoveredFoldRegion != null && hoveredFoldRegion.StartLineNumber == line.LineNumber))
                {
                    GUI.DrawTexture(lineRect, _lineHighlightFill);
                }

                DrawFoldGlyph(line, hoveredFoldRegion);
                var gutterRect = new Rect(FoldGlyphWidth + 2f, line.Y, gutterWidth - FoldGlyphWidth - 10f, _lineHeight);
                if (isNavigationLine)
                {
                    GUI.DrawTexture(new Rect(gutterRect.x - 4f, gutterRect.y + 2f, 2f, Mathf.Max(2f, gutterRect.height - 4f)), _tooltipUnderlineFill);
                }

                GUI.Label(gutterRect, line.LineNumber.ToString("D4"), _gutterStyle);
                for (var tokenIndex = 0; tokenIndex < line.Tokens.Count; tokenIndex++)
                {
                    var token = line.Tokens[tokenIndex];
                    var tokenRect = new Rect(gutterWidth + token.X, line.Y, Mathf.Max(2f, token.Width), _lineHeight);
                    token.ContentRect = tokenRect;
                    var effectiveClassification = GetEffectiveTokenClassification(line, tokenIndex);
                    var isSelectedToken = string.Equals(token.Key, _selectedTokenKey, StringComparison.Ordinal);
                    var isRelatedSelection = !isSelectedToken && IsRelatedSelectionToken(session, token);
                    if (isRelatedSelection)
                    {
                        GUI.DrawTexture(tokenRect, _relatedSelectionFill);
                    }

                    if (isSelectedToken)
                    {
                        GUI.DrawTexture(tokenRect, _selectedFill);
                    }
                    else if (hoveredToken != null && string.Equals(token.Key, hoveredToken.Key, StringComparison.Ordinal))
                    {
                        GUI.DrawTexture(tokenRect, _hoverFill);
                    }

                    GUI.Label(tokenRect, token.DisplayText, GetClassificationStyle(effectiveClassification));
                    if (isSelectedToken)
                    {
                        DrawSelectionOutline(tokenRect);
                    }
                    else if (hoveredToken != null &&
                        string.Equals(token.Key, hoveredToken.Key, StringComparison.Ordinal) &&
                        CanNavigateToDefinition(token))
                    {
                        DrawHoverOutline(tokenRect);
                    }
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

        private void HandlePointerInput(
            DocumentSession session,
            CortexShellState state,
            CodeViewToken hoveredToken,
            EditorMethodTargetOutline hoveredMethodTarget,
            FoldRegion hoveredFoldRegion,
            bool hasMouse,
            Event current,
            Vector2 localMouse,
            Vector2 viewportSize,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            bool pointerOnMethodInspector,
            bool pointerOnContextMenu)
        {
            if (current == null)
            {
                return;
            }

            _overlayInteractionService.TracePointerRouting(
                "decompiled-editor",
                current,
                pointerOnMethodInspector,
                pointerOnContextMenu,
                hasMouse,
                state != null &&
                    state.Editor != null &&
                    state.Editor.MethodInspector != null &&
                    state.Editor.MethodInspector.IsVisible);

            if (_overlayInteractionService.ShouldBypassSurfaceInput(current, pointerOnMethodInspector, pointerOnContextMenu))
            {
                return;
            }

            if (_overlayInteractionService.ShouldCloseMethodInspectorOnPointerDown(current, pointerOnMethodInspector, pointerOnContextMenu, state))
            {
                _methodInspectorService.Close(state);
            }

            if (!hasMouse || current.type != EventType.MouseDown)
            {
                return;
            }

            if (current.button == 0 && state != null && state.Harmony != null && state.Harmony.IsInsertionPickActive)
            {
                state.Harmony.GenerationStatusMessage = "Select the Harmony insertion point from a writable source editor, not decompiled code.";
                state.StatusMessage = state.Harmony.GenerationStatusMessage;
                MMLog.WriteWarning("[Cortex.Harmony] Rejected insertion-point pick from decompiled editor '" +
                    (session != null ? session.FilePath ?? string.Empty : string.Empty) + "'.");
                current.Use();
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
                OpenContextMenu(session, state, hoveredToken, localMouse, commandRegistry, contributionRegistry);
                current.Use();
                return;
            }

            if (current.button == 0 &&
                current.control &&
                current.clickCount == 1 &&
                hoveredMethodTarget != null)
            {
                EditorCommandInvocation invocation;
                var opened = TryOpenMethodTargetInspector(session, state, hoveredMethodTarget, out invocation);
                MMLog.WriteInfo("[Cortex.MethodTargets] Ctrl+click on decompiled method target. Document='" +
                    (session != null ? session.FilePath ?? string.Empty : string.Empty) +
                    "', Symbol='" + (hoveredMethodTarget.SymbolText ?? string.Empty) +
                    "', Lines=" + hoveredMethodTarget.StartLineNumber + "-" + hoveredMethodTarget.EndLineNumber +
                    ", Opened=" + opened + ".");
                if (opened)
                {
                    current.Use();
                    return;
                }
            }

            if (current.button != 0 || hoveredToken == null)
            {
                return;
            }

            SetSelectedToken(session, hoveredToken);
            _selectionInspectionService.ApplySelection(
                state,
                session != null ? session.FilePath ?? string.Empty : string.Empty,
                hoveredToken.Key,
                hoveredToken.RawText,
                hoveredToken.Classification,
                hoveredToken.LineNumber,
                hoveredToken.Column,
                ResolveHoverResponse(state, hoveredToken));
            if (current.control && current.clickCount == 1)
            {
                EditorCommandInvocation invocation;
                if (TryBuildCommandTarget(session, state, hoveredToken, out invocation) &&
                    _methodInspectorService.TryOpen(state, invocation, hoveredToken.Classification))
                {
                    current.Use();
                    return;
                }
            }

            if (current.clickCount >= 2)
            {
                    EditorCommandInvocation invocation;
                    if (TryBuildCommandTarget(session, state, hoveredToken, out invocation) &&
                        invocation != null &&
                        invocation.Target != null &&
                        invocation.Target.CanGoToDefinition)
                    {
                        _symbolInteractionService.RequestDefinition(state, invocation.Target);
                    }
            }

            current.Use();
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

        private bool IsMethodTargetSelectionMode()
        {
            var current = Event.current;
            return (current != null && current.control) ||
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);
        }

        private void DrawMethodTargetOutlines(DocumentSession session, int firstVisibleLineIndex, int lastVisibleLineIndex, float gutterWidth, EditorMethodTargetOutline hoveredMethodTarget)
        {
            var outlines = _methodTargetOutlineService.GetOutlines(session);
            for (var i = 0; i < outlines.Length; i++)
            {
                var outline = outlines[i];
                if (outline == null ||
                    outline.EndLineNumber < 1 ||
                    outline.StartLineNumber > outline.EndLineNumber)
                {
                    continue;
                }

                var startLineIndex = FindVisibleLineIndexByLineNumber(outline.StartLineNumber);
                var endLineIndex = FindVisibleLineIndexByLineNumber(outline.EndLineNumber);
                if (startLineIndex < 0 || endLineIndex < 0 || endLineIndex < firstVisibleLineIndex || startLineIndex > lastVisibleLineIndex)
                {
                    continue;
                }

                Rect blockRect;
                if (!TryBuildMethodTargetRect(outline, gutterWidth, out blockRect))
                {
                    continue;
                }

                DrawMethodTargetGlow(blockRect);
                GUI.DrawTexture(blockRect, _methodTargetFill);
                DrawMethodTargetOutline(blockRect);
                if (hoveredMethodTarget != null &&
                    hoveredMethodTarget.AnchorStart == outline.AnchorStart &&
                    hoveredMethodTarget.StartLineNumber == outline.StartLineNumber &&
                    hoveredMethodTarget.EndLineNumber == outline.EndLineNumber)
                {
                    DrawHoverOutline(blockRect);
                }
            }
        }

        private EditorMethodTargetOutline FindMethodTargetAt(DocumentSession session, Vector2 contentMouse, float gutterWidth)
        {
            var outlines = _methodTargetOutlineService.GetOutlines(session);
            EditorMethodTargetOutline bestMatch = null;
            var bestHeight = float.MaxValue;
            for (var i = 0; i < outlines.Length; i++)
            {
                var outline = outlines[i];
                Rect blockRect;
                if (outline == null ||
                    !TryBuildMethodTargetRect(outline, gutterWidth, out blockRect) ||
                    !blockRect.Contains(contentMouse))
                {
                    continue;
                }

                if (bestMatch == null || blockRect.height < bestHeight)
                {
                    bestMatch = outline;
                    bestHeight = blockRect.height;
                }
            }

            return bestMatch;
        }

        private bool TryOpenMethodTargetInspector(DocumentSession session, CortexShellState state, EditorMethodTargetOutline outline, out EditorCommandInvocation invocation)
        {
            invocation = null;
            var token = FindMethodTargetToken(outline);
            if (token == null)
            {
                return false;
            }

            SetSelectedToken(session, token);
            _selectionInspectionService.ApplySelection(
                state,
                session != null ? session.FilePath ?? string.Empty : string.Empty,
                token.Key,
                token.RawText,
                outline != null ? outline.Classification : token.Classification,
                token.LineNumber,
                token.Column,
                ResolveHoverResponse(state, token));
            return TryBuildCommandTarget(session, state, token, out invocation) &&
                _methodInspectorService.TryOpen(state, invocation, outline != null ? outline.Classification : token.Classification);
        }

        private CodeViewToken FindMethodTargetToken(EditorMethodTargetOutline outline)
        {
            if (outline == null || _layout == null)
            {
                return null;
            }

            for (var lineIndex = 0; lineIndex < _layout.VisibleLines.Count; lineIndex++)
            {
                var line = _layout.VisibleLines[lineIndex];
                if (line == null || line.LineNumber != outline.AnchorLineNumber)
                {
                    continue;
                }

                for (var tokenIndex = 0; tokenIndex < line.Tokens.Count; tokenIndex++)
                {
                    var token = line.Tokens[tokenIndex];
                    if (token != null &&
                        token.Start == outline.AnchorStart &&
                        token.Length == outline.AnchorLength)
                    {
                        return token;
                    }
                }
            }

            for (var lineIndex = 0; lineIndex < _layout.VisibleLines.Count; lineIndex++)
            {
                var line = _layout.VisibleLines[lineIndex];
                if (line == null || line.LineNumber != outline.AnchorLineNumber)
                {
                    continue;
                }

                for (var tokenIndex = 0; tokenIndex < line.Tokens.Count; tokenIndex++)
                {
                    var token = line.Tokens[tokenIndex];
                    if (token != null &&
                        string.Equals((token.RawText ?? string.Empty).Trim(), outline.SymbolText ?? string.Empty, StringComparison.Ordinal))
                    {
                        return token;
                    }
                }
            }

            return null;
        }

        private bool TryBuildMethodTargetRect(EditorMethodTargetOutline outline, float gutterWidth, out Rect rect)
        {
            rect = new Rect(0f, 0f, 0f, 0f);
            if (outline == null || _layout == null || _layout.VisibleLines.Count == 0)
            {
                return false;
            }

            var startIndex = FindVisibleLineIndexByLineNumber(outline.StartLineNumber);
            var endIndex = FindVisibleLineIndexByLineNumber(outline.EndLineNumber);
            if (startIndex < 0 || endIndex < startIndex)
            {
                return false;
            }

            var top = _layout.VisibleLines[startIndex].Y + 1f;
            var bottom = _layout.VisibleLines[endIndex].Y + _lineHeight - 1f;
            var left = float.MaxValue;
            var right = gutterWidth + 8f;
            for (var lineIndex = startIndex; lineIndex <= endIndex; lineIndex++)
            {
                var line = _layout.VisibleLines[lineIndex];
                if (line == null)
                {
                    continue;
                }

                left = Mathf.Min(left, GetMethodTargetLineLeft(line, gutterWidth));
                right = Mathf.Max(right, gutterWidth + line.Width + 8f);
            }

            if (left == float.MaxValue)
            {
                left = gutterWidth;
            }

            rect = new Rect(left, top, Mathf.Max(24f, right - left), Mathf.Max(_lineHeight - 2f, bottom - top));
            return true;
        }

        private float GetMethodTargetLineLeft(CodeViewLine line, float gutterWidth)
        {
            if (line == null || line.Tokens.Count == 0)
            {
                return gutterWidth;
            }

            for (var i = 0; i < line.Tokens.Count; i++)
            {
                var token = line.Tokens[i];
                if (token != null && !string.IsNullOrEmpty((token.RawText ?? string.Empty).Trim()))
                {
                    return gutterWidth + token.X - 4f;
                }
            }

            return gutterWidth;
        }

        private void DrawMethodTargetOutline(Rect blockRect)
        {
            GUI.DrawTexture(new Rect(blockRect.x, blockRect.y, blockRect.width, 1f), _methodTargetOutlineFill);
            GUI.DrawTexture(new Rect(blockRect.x, blockRect.yMax - 1f, blockRect.width, 1f), _methodTargetOutlineFill);
            GUI.DrawTexture(new Rect(blockRect.x, blockRect.y, 1f, blockRect.height), _methodTargetOutlineFill);
            GUI.DrawTexture(new Rect(blockRect.xMax - 1f, blockRect.y, 1f, blockRect.height), _methodTargetOutlineFill);
        }

        private void DrawMethodTargetGlow(Rect blockRect)
        {
            if (_methodTargetGlowFill == null)
            {
                return;
            }

            var glowRect = new Rect(blockRect.x - 3f, blockRect.y - 3f, blockRect.width + 6f, blockRect.height + 6f);
            GUI.DrawTexture(glowRect, _methodTargetGlowFill);
        }

        private int FindVisibleLineIndexByLineNumber(int lineNumber)
        {
            if (_layout == null || _layout.VisibleLines.Count == 0 || lineNumber <= 0)
            {
                return -1;
            }

            for (var i = 0; i < _layout.VisibleLines.Count; i++)
            {
                var line = _layout.VisibleLines[i];
                if (line != null && line.LineNumber == lineNumber)
                {
                    return i;
                }
            }

            return -1;
        }

        private void UpdateHoverRequest(DocumentSession session, CortexShellState state, CodeViewToken hoveredToken, bool hasMouse)
        {
            if (!hasMouse)
            {
                return;
            }

            var hoverKey = hoveredToken != null ? hoveredToken.Key : string.Empty;
            if (string.IsNullOrEmpty(hoverKey))
            {
                _hoverCandidateKey = string.Empty;
                _hoverCandidateUtc = DateTime.MinValue;
                return;
            }

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

            if (!string.IsNullOrEmpty(_stickyHoverKey) &&
                !string.IsNullOrEmpty(state.Editor.ActiveHoverKey) &&
                !string.Equals(hoverKey, _stickyHoverKey, StringComparison.Ordinal))
            {
                LogHoverRetargetSuppressed(hoveredToken, hoverKey);
                _hoverCandidateKey = string.Empty;
                _hoverCandidateUtc = DateTime.MinValue;
                return;
            }

            if (string.Equals(state.Editor.ActiveHoverKey, hoverKey, StringComparison.Ordinal) &&
                state.Editor.ActiveHoverResponse != null &&
                state.Editor.ActiveHoverResponse.Success)
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
            state.Editor.RequestedHoverAbsolutePosition = hoveredToken.Start;
            state.Editor.RequestedHoverTokenText = hoveredToken.RawText.Trim();
            EditorInteractionLog.WriteHover("Queued hover request for " +
                state.Editor.RequestedHoverTokenText +
                " @ " + state.Editor.RequestedHoverLine + ":" + state.Editor.RequestedHoverColumn + ".");
        }

        private void DrawTooltip(DocumentSession session, Vector2 scroll, Vector2 localMouse, CodeViewToken hoveredToken, CortexNavigationService navigationService, CortexShellState state, Vector2 viewportSize, bool hasMouse)
        {
            var current = Event.current;
            var effectiveViewportSize = ResolveTooltipViewportSize(viewportSize);
            if (!IsUsableTooltipViewport(effectiveViewportSize))
            {
                return;
            }

            var tooltipModel = BuildHoverTooltipModel(session, scroll, hoveredToken, state);
            HoverTooltipRenderResult tooltipResult;
            if (!_tooltipPresenter.DrawRichTooltip(
                tooltipModel,
                current,
                localMouse,
                effectiveViewportSize,
                hasMouse,
                CreateHoverTooltipStyleSet(),
                out tooltipResult))
            {
                _tooltipPresenter.ClearRichState();
                _stickyHoverKey = string.Empty;
                _stickyHoverDocumentPath = string.Empty;
                _stickyHoverAnchorRect = new Rect(0f, 0f, 0f, 0f);
                _stickyHoverTooltipRect = new Rect(0f, 0f, 0f, 0f);
                ClearVisibleHover(state);
                return;
            }

            var response = tooltipResult.Model != null ? tooltipResult.Model.Response : null;
            var hoverKey = tooltipResult.Model != null ? tooltipResult.Model.Key ?? string.Empty : string.Empty;
            _stickyHoverKey = hoverKey;
            _stickyHoverDocumentPath = tooltipResult.Model != null ? tooltipResult.Model.DocumentPath ?? string.Empty : string.Empty;
            _stickyHoverAnchorRect = tooltipResult.Model != null ? tooltipResult.Model.AnchorRect : new Rect(0f, 0f, 0f, 0f);
            _stickyHoverTooltipRect = tooltipResult.TooltipRect;
            SetVisibleHover(state, hoverKey, response, tooltipResult.HoveredPart);
            if (current == null || current.type == EventType.Repaint)
            {
                LogHoverPlacement(hoverKey, response, hoveredToken, tooltipResult.TooltipRect, localMouse, effectiveViewportSize);
            }
            LogVisibleHover(hoverKey, response, hoveredToken);
            PreloadTooltipTargets(navigationService, state, response, tooltipResult.HoveredPart);
            HandleTooltipPartNavigation(navigationService, state, tooltipResult.ActivatedPart);
        }

        private static void PreloadTooltipTargets(
            CortexNavigationService navigationService,
            CortexShellState state,
            LanguageServiceHoverResponse response,
            LanguageServiceHoverDisplayPart hoveredPart)
        {
            if (navigationService == null || state == null)
            {
                return;
            }

            if (hoveredPart != null && hoveredPart.IsInteractive)
            {
                navigationService.PreloadHoverDisplayPartTarget(state, hoveredPart);
                return;
            }

            navigationService.PreloadHoverResponseTarget(state, response);
        }

        private bool TryResolveVisibleHover(
            DocumentSession session,
            Vector2 scroll,
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
                if (HasAnyHoverState(state))
                {
                    LogHoverClear("active-hover-missing", hoverKey, localMouse);
                    ClearStickyHover(state);
                }

                return false;
            }

            var documentPath = session != null ? session.FilePath ?? string.Empty : string.Empty;
            if (hoveredToken != null && string.Equals(hoverKey, hoveredToken.Key, StringComparison.Ordinal))
            {
                _stickyHoverKey = hoverKey;
                _stickyHoverDocumentPath = documentPath;
                _stickyHoverAnchorRect = ToViewportRect(hoveredToken.ContentRect, scroll);
                RefreshStickyHoverKeepAlive();
                return true;
            }

            if (hoveredToken != null)
            {
                if (IsPointerWithinHoverSurface(localMouse))
                {
                    RefreshStickyHoverKeepAlive();
                    return true;
                }

                if (DateTime.UtcNow <= _stickyHoverKeepAliveUtc)
                {
                    return true;
                }

                LogHoverClear("retarget-left-surface", hoverKey, localMouse);
                ClearStickyHover(state);
                return false;
            }

            if (!string.Equals(_stickyHoverKey, hoverKey, StringComparison.Ordinal) ||
                !string.Equals(_stickyHoverDocumentPath, documentPath, StringComparison.OrdinalIgnoreCase))
            {
                if (HasStickyHoverState())
                {
                    LogHoverClear("sticky-context-mismatch", hoverKey, localMouse);
                    ClearStickyHover(state);
                }

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

            LogHoverClear("sticky-expired", hoverKey, localMouse);
            ClearStickyHover(state);
            return false;
        }

        private Rect BuildTooltipRect(Vector2 scroll, Vector2 localMouse, CodeViewToken hoveredToken, string hoverKey, Vector2 viewportSize, float height)
        {
            if (hoveredToken != null && string.Equals(hoverKey, hoveredToken.Key, StringComparison.Ordinal))
            {
                return ClampTooltipRect(BuildTooltipRectFromAnchor(ToViewportRect(hoveredToken.ContentRect, scroll), viewportSize, height), viewportSize);
            }

            if (_stickyHoverTooltipRect.width > 0f && _stickyHoverTooltipRect.height > 0f)
            {
                var stickyRect = _stickyHoverTooltipRect;
                stickyRect.width = TooltipWidth;
                stickyRect.height = height;
                return ClampTooltipRect(stickyRect, viewportSize);
            }

            return ClampTooltipRect(new Rect(localMouse.x + 18f, localMouse.y + 18f, TooltipWidth, height), viewportSize);
        }

        private static Rect BuildTooltipRectFromAnchor(Rect anchorRect, Vector2 viewportSize, float height)
        {
            var x = anchorRect.xMin - 4f;
            var y = anchorRect.yMax - 3f;
            if (y + height > viewportSize.y - 12f)
            {
                y = anchorRect.yMin - height + 3f;
            }

            return new Rect(x, y, TooltipWidth, height);
        }

        private static Rect ToViewportRect(Rect contentRect, Vector2 scroll)
        {
            return new Rect(contentRect.x - scroll.x, contentRect.y - scroll.y, contentRect.width, contentRect.height);
        }

        private LanguageServiceHoverDisplayPart[] GetTooltipDisplayParts(LanguageServiceHoverResponse response)
        {
            if (response != null && response.DisplayParts != null && response.DisplayParts.Length > 0)
            {
                return response.DisplayParts;
            }

            return new[]
            {
                new LanguageServiceHoverDisplayPart
                {
                    Text = response != null ? response.SymbolDisplay ?? string.Empty : string.Empty,
                    Classification = string.Empty,
                    IsInteractive = false
                }
            };
        }

        private static string GetTooltipQualifiedPath(LanguageServiceHoverResponse response)
        {
            if (response == null)
            {
                return string.Empty;
            }

            return response.QualifiedSymbolDisplay ?? string.Empty;
        }

        private HoverTooltipStyleSet CreateHoverTooltipStyleSet()
        {
            return new HoverTooltipStyleSet
            {
                Width = TooltipWidth,
                ContainerStyle = _tooltipStyle,
                PathStyle = _tooltipPathStyle,
                MetaStyle = _tooltipMetaStyle,
                SignatureStyle = _tooltipSignatureStyle,
                LinkStyle = _tooltipLinkStyle,
                DetailStyle = _tooltipDetailStyle,
                HoverFill = _hoverFill,
                UnderlineFill = _tooltipUnderlineFill,
                BorderFill = _hoverOutlineFill
            };
        }

        private HoverTooltipRenderModel BuildHoverTooltipModel(DocumentSession session, Vector2 scroll, CodeViewToken hoveredToken, CortexShellState state)
        {
            var response = ResolveHoverResponse(state, hoveredToken);
            if (hoveredToken == null || response == null || !response.Success)
            {
                return null;
            }

            var tokenClassification = _classificationPresentationService.NormalizeClassification(hoveredToken.Classification);
            return new HoverTooltipRenderModel
            {
                Key = hoveredToken.Key ?? string.Empty,
                DocumentPath = session != null ? session.FilePath ?? string.Empty : string.Empty,
                AnchorRect = ToViewportRect(hoveredToken.ContentRect, scroll),
                QualifiedPath = GetTooltipQualifiedPath(response),
                Response = response,
                SignatureParts = GetTooltipDisplayParts(response),
                BuildMetaText = delegate(LanguageServiceHoverDisplayPart part)
                {
                    return BuildTooltipMetaText(response, part, tokenClassification);
                },
                BuildDocumentationText = delegate(LanguageServiceHoverDisplayPart part)
                {
                    return part != null
                        ? part.DocumentationText ?? string.Empty
                        : response.DocumentationText ?? string.Empty;
                },
                GetSupplementalSections = delegate(LanguageServiceHoverDisplayPart part)
                {
                    return part != null
                        ? part.SupplementalSections
                        : response.SupplementalSections;
                }
            };
        }

        private float LayoutTooltipParts(Rect bounds, LanguageServiceHoverDisplayPart[] parts, List<TooltipPartVisual> visuals, bool draw)
        {
            var x = bounds.x;
            var y = bounds.y;
            var maxX = bounds.x + Mathf.Max(8f, bounds.width);
            var lineHeight = Mathf.Max(18f, _tooltipSignatureStyle.CalcSize(new GUIContent("Ag")).y + 2f);

            for (var i = 0; parts != null && i < parts.Length; i++)
            {
                var part = parts[i];
                var text = part != null ? part.Text ?? string.Empty : string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var style = part.IsInteractive ? _tooltipLinkStyle : _tooltipSignatureStyle;
                var width = Mathf.Max(2f, style.CalcSize(new GUIContent(text)).x);
                if (x > bounds.x && x + width > maxX)
                {
                    x = bounds.x;
                    y += lineHeight;
                }

                var rect = new Rect(x, y, width, lineHeight);
                if (visuals != null)
                {
                    visuals.Add(new TooltipPartVisual { Part = part, Rect = rect });
                }

                if (draw)
                {
                    GUI.Label(rect, text, style);
                }

                x += width;
            }

            return Mathf.Max(lineHeight, (y - bounds.y) + lineHeight);
        }

        private static TooltipPartVisual FindHoveredTooltipPart(List<TooltipPartVisual> visuals, Vector2 localMouse)
        {
            if (visuals == null)
            {
                return null;
            }

            for (var i = 0; i < visuals.Count; i++)
            {
                var visual = visuals[i];
                if (visual != null && visual.Part != null && visual.Part.IsInteractive && visual.Rect.Contains(localMouse))
                {
                    return visual;
                }
            }

            return null;
        }

        private float BuildTooltipHeight(float pathHeight, float signatureHeight, string detailText)
        {
            return BuildTooltipHeight(pathHeight, 0f, signatureHeight, detailText);
        }

        private float BuildTooltipHeight(float pathHeight, float metaHeight, float signatureHeight, string detailText)
        {
            var detailHeight = string.IsNullOrEmpty(detailText)
                ? 0f
                : _tooltipDetailStyle.CalcHeight(new GUIContent(detailText), TooltipWidth - 16f);
            return Mathf.Min(360f, 14f + pathHeight + metaHeight + signatureHeight + (detailHeight > 0f ? detailHeight + 8f : 0f));
        }

        private static Rect BuildTooltipPathRect(Rect tooltipRect, float pathHeight)
        {
            return new Rect(
                tooltipRect.x + 8f,
                tooltipRect.y + 7f,
                tooltipRect.width - 16f,
                Mathf.Max(0f, pathHeight));
        }

        private static Rect BuildTooltipMetaRect(Rect tooltipRect, float pathHeight, float metaHeight)
        {
            return new Rect(
                tooltipRect.x + 8f,
                tooltipRect.y + 7f + pathHeight,
                tooltipRect.width - 16f,
                Mathf.Max(0f, metaHeight));
        }

        private static Rect BuildTooltipSignatureRect(Rect tooltipRect, float pathHeight, float metaHeight)
        {
            return new Rect(
                tooltipRect.x + 8f,
                tooltipRect.y + 7f + pathHeight + metaHeight + (metaHeight > 0f ? 4f : 0f),
                tooltipRect.width - 16f,
                Mathf.Max(0f, tooltipRect.height - pathHeight - metaHeight - 14f));
        }

        private static Rect BuildTooltipDetailRect(Rect tooltipRect, float pathHeight, float metaHeight, float signatureHeight)
        {
            return new Rect(
                tooltipRect.x + 8f,
                tooltipRect.y + 7f + pathHeight + metaHeight + (metaHeight > 0f ? 4f : 0f) + signatureHeight + 6f,
                tooltipRect.width - 16f,
                Mathf.Max(0f, tooltipRect.height - pathHeight - metaHeight - signatureHeight - 20f));
        }

        private static string BuildTooltipDetailText(LanguageServiceHoverResponse response, LanguageServiceHoverDisplayPart hoveredPart, string tokenClassification)
        {
            if (hoveredPart != null)
            {
                var partDocs = hoveredPart.DocumentationText ?? string.Empty;
                return partDocs;
            }

            var docs = response != null ? response.DocumentationText ?? string.Empty : string.Empty;
            if (!string.IsNullOrEmpty(docs))
            {
                return docs;
            }

            return !string.IsNullOrEmpty(tokenClassification)
                ? "Classification: " + tokenClassification + "."
                : string.Empty;
        }

        private static string BuildTooltipMetaText(LanguageServiceHoverResponse response, LanguageServiceHoverDisplayPart hoveredPart, string tokenClassification)
        {
            var kind = hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.SymbolKind)
                ? hoveredPart.SymbolKind
                : (response != null ? response.SymbolKind ?? string.Empty : string.Empty);
            var containingType = hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.ContainingTypeName)
                ? hoveredPart.ContainingTypeName
                : (response != null ? response.ContainingTypeName ?? string.Empty : string.Empty);
            var assembly = hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.ContainingAssemblyName)
                ? hoveredPart.ContainingAssemblyName
                : (response != null ? response.ContainingAssemblyName ?? string.Empty : string.Empty);
            var metadataName = hoveredPart != null && !string.IsNullOrEmpty(hoveredPart.MetadataName)
                ? hoveredPart.MetadataName
                : (response != null ? response.MetadataName ?? string.Empty : string.Empty);

            var lines = new List<string>();
            if (!string.IsNullOrEmpty(kind))
            {
                lines.Add("Kind: " + kind);
            }
            else if (!string.IsNullOrEmpty(tokenClassification))
            {
                lines.Add("Classification: " + tokenClassification);
            }

            if (!string.IsNullOrEmpty(containingType))
            {
                lines.Add("Type: " + containingType);
            }

            if (!string.IsNullOrEmpty(assembly))
            {
                lines.Add("Assembly: " + assembly);
            }

            if (!string.IsNullOrEmpty(metadataName))
            {
                lines.Add("Metadata: " + metadataName);
            }

            return lines.Count > 0 ? string.Join("\n", lines.ToArray()) : string.Empty;
        }

        private void HandleTooltipPartNavigation(CortexNavigationService navigationService, CortexShellState state, LanguageServiceHoverDisplayPart part)
        {
            if (part == null || navigationService == null || state == null)
            {
                return;
            }

            CortexDeveloperLog.WriteSymbolNavigation("decompiled-editor", part);
            var definitionRange = part.DefinitionRange;
            EditorInteractionLog.WriteHover("Opening tooltip part. Symbol=" + (part.SymbolDisplay ?? part.Text ?? string.Empty) +
                ", DefinitionPath=" + (part.DefinitionDocumentPath ?? string.Empty) +
                ", DefinitionLine=" + (definitionRange != null ? definitionRange.StartLine : 0) +
                ", DefinitionColumn=" + (definitionRange != null ? definitionRange.StartColumn : 0) + ".");

            if (navigationService.OpenHoverDisplayPart(
                state,
                part,
                "Opened definition: " + (part.SymbolDisplay ?? part.Text ?? string.Empty),
                "Unable to open definition for " + (part.SymbolDisplay ?? part.Text ?? string.Empty) + "."))
            {
                EditorInteractionLog.WriteHover("Opened tooltip symbol target for " + (part.SymbolDisplay ?? part.Text ?? string.Empty) + ".");
            }
        }

        private static Rect ClampTooltipRect(Rect rect, Vector2 viewportSize)
        {
            rect.x = Mathf.Min(rect.x, Mathf.Max(8f, viewportSize.x - rect.width - 12f));
            rect.y = Mathf.Min(rect.y, Mathf.Max(8f, viewportSize.y - rect.height - 12f));
            rect.x = Mathf.Max(8f, rect.x);
            rect.y = Mathf.Max(8f, rect.y);
            return rect;
        }

        private bool IsPointerWithinHoverSurface(Vector2 localMouse)
        {
            if (IsPointerWithinTooltip(localMouse))
            {
                return true;
            }

            if (_stickyHoverAnchorRect.width > 0f && _stickyHoverAnchorRect.height > 0f && _stickyHoverAnchorRect.Contains(localMouse))
            {
                return true;
            }

            var bridgeRect = BuildHoverBridgeRect();
            if (bridgeRect.width > 0f && bridgeRect.height > 0f && bridgeRect.Contains(localMouse))
            {
                return true;
            }

            return false;
        }

        private bool IsPointerWithinTooltip(Vector2 localMouse)
        {
            return _stickyHoverTooltipRect.width > 0f &&
                _stickyHoverTooltipRect.height > 0f &&
                _stickyHoverTooltipRect.Contains(localMouse);
        }

        private Rect BuildHoverBridgeRect()
        {
            if (_stickyHoverAnchorRect.width <= 0f || _stickyHoverAnchorRect.height <= 0f ||
                _stickyHoverTooltipRect.width <= 0f || _stickyHoverTooltipRect.height <= 0f)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            var overlapLeft = Mathf.Max(_stickyHoverAnchorRect.xMin, _stickyHoverTooltipRect.xMin);
            var overlapRight = Mathf.Min(_stickyHoverAnchorRect.xMax, _stickyHoverTooltipRect.xMax);
            if (overlapRight > overlapLeft)
            {
                return Rect.MinMaxRect(
                    overlapLeft - 6f,
                    Mathf.Min(_stickyHoverAnchorRect.yMin, _stickyHoverTooltipRect.yMin) - 4f,
                    overlapRight + 6f,
                    Mathf.Max(_stickyHoverAnchorRect.yMax, _stickyHoverTooltipRect.yMax) + 4f);
            }

            var overlapTop = Mathf.Max(_stickyHoverAnchorRect.yMin, _stickyHoverTooltipRect.yMin);
            var overlapBottom = Mathf.Min(_stickyHoverAnchorRect.yMax, _stickyHoverTooltipRect.yMax);
            if (overlapBottom > overlapTop)
            {
                return Rect.MinMaxRect(
                    Mathf.Min(_stickyHoverAnchorRect.xMin, _stickyHoverTooltipRect.xMin) - 4f,
                    overlapTop - 6f,
                    Mathf.Max(_stickyHoverAnchorRect.xMax, _stickyHoverTooltipRect.xMax) + 4f,
                    overlapBottom + 6f);
            }

            var anchorCenter = _stickyHoverAnchorRect.center;
            var tooltipCenter = _stickyHoverTooltipRect.center;
            return Rect.MinMaxRect(
                Mathf.Min(anchorCenter.x, tooltipCenter.x) - 8f,
                Mathf.Min(anchorCenter.y, tooltipCenter.y) - 8f,
                Mathf.Max(anchorCenter.x, tooltipCenter.x) + 8f,
                Mathf.Max(anchorCenter.y, tooltipCenter.y) + 8f);
        }

        private void RefreshStickyHoverKeepAlive()
        {
            _stickyHoverKeepAliveUtc = DateTime.UtcNow.AddMilliseconds(StickyHoverGraceMs);
        }

        private void ClearStickyHover(CortexShellState state)
        {
            _tooltipPresenter.ClearRichState();
            _stickyHoverKey = string.Empty;
            _stickyHoverDocumentPath = string.Empty;
            _stickyHoverAnchorRect = new Rect(0f, 0f, 0f, 0f);
            _stickyHoverTooltipRect = new Rect(0f, 0f, 0f, 0f);
            _lastValidTooltipViewport = Vector2.zero;
            _stickyHoverKeepAliveUtc = DateTime.MinValue;
            _lastVisibleHoverLogKey = string.Empty;
            _lastHoverPlacementLogKey = string.Empty;
            _lastHoverRetargetLogKey = string.Empty;
            _pressedTooltipPartKey = string.Empty;
            ClearVisibleHover(state);
            ClearActiveHover(state);
        }

        private bool HasAnyHoverState(CortexShellState state)
        {
            if (!string.IsNullOrEmpty(_stickyHoverKey) ||
                !string.IsNullOrEmpty(_stickyHoverDocumentPath) ||
                HasArea(_stickyHoverAnchorRect) ||
                HasArea(_stickyHoverTooltipRect) ||
                _stickyHoverKeepAliveUtc > DateTime.MinValue)
            {
                return true;
            }

            return state != null &&
                state.Editor != null &&
                (!string.IsNullOrEmpty(state.Editor.ActiveHoverKey) ||
                 state.Editor.ActiveHoverResponse != null ||
                 !string.IsNullOrEmpty(state.Editor.VisibleHoverKey));
        }

        private bool HasStickyHoverState()
        {
            return !string.IsNullOrEmpty(_stickyHoverKey) ||
                !string.IsNullOrEmpty(_stickyHoverDocumentPath) ||
                HasArea(_stickyHoverAnchorRect) ||
                HasArea(_stickyHoverTooltipRect) ||
                _stickyHoverKeepAliveUtc > DateTime.MinValue;
        }

        private static void ClearActiveHover(CortexShellState state)
        {
            if (state == null || state.Editor == null)
            {
                return;
            }

            state.Editor.ActiveHoverKey = string.Empty;
            state.Editor.ActiveHoverResponse = null;
        }

        private static bool HasArea(Rect rect)
        {
            return rect.width > 0f && rect.height > 0f;
        }

        private void LogVisibleHover(string hoverKey, LanguageServiceHoverResponse response, CodeViewToken hoveredToken)
        {
            if (string.IsNullOrEmpty(hoverKey) || string.Equals(_lastVisibleHoverLogKey, hoverKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastVisibleHoverLogKey = hoverKey;
            var symbolText = !string.IsNullOrEmpty(response != null ? response.SymbolDisplay : string.Empty)
                ? response.SymbolDisplay
                : (hoveredToken != null ? hoveredToken.RawText.Trim() : string.Empty);
            CortexDeveloperLog.WriteSymbolTooltipVisible("decompiled-editor", hoverKey, symbolText, response);
        }

        private void LogHoverPlacement(
            string hoverKey,
            LanguageServiceHoverResponse response,
            CodeViewToken hoveredToken,
            Rect tooltipRect,
            Vector2 localMouse,
            Vector2 viewportSize)
        {
            if (string.IsNullOrEmpty(hoverKey))
            {
                return;
            }

            var placementKey = hoverKey + "|" +
                FormatRect(_stickyHoverAnchorRect) + "|" +
                FormatRect(tooltipRect);
            if (string.Equals(_lastHoverPlacementLogKey, placementKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastHoverPlacementLogKey = placementKey;
            EditorInteractionLog.WriteHover("Hover placement key=" + hoverKey +
                ", symbol=" + (!string.IsNullOrEmpty(response != null ? response.SymbolDisplay : string.Empty)
                    ? response.SymbolDisplay
                    : (hoveredToken != null ? hoveredToken.RawText.Trim() : string.Empty)) +
                ", anchor=" + FormatRect(_stickyHoverAnchorRect) +
                ", tooltip=" + FormatRect(tooltipRect) +
                ", bridge=" + FormatRect(BuildHoverBridgeRect()) +
                ", viewport=(" + Mathf.RoundToInt(viewportSize.x) + "x" + Mathf.RoundToInt(viewportSize.y) + ").");
        }

        private Vector2 ResolveTooltipViewportSize(Vector2 viewportSize)
        {
            if (IsUsableTooltipViewport(viewportSize))
            {
                _lastValidTooltipViewport = viewportSize;
                return viewportSize;
            }

            return _lastValidTooltipViewport;
        }

        private static bool IsUsableTooltipViewport(Vector2 viewportSize)
        {
            return viewportSize.x >= 64f && viewportSize.y >= 64f;
        }

        private void LogHoverClear(string reason, string hoverKey, Vector2 localMouse)
        {
            var clearKey = (reason ?? string.Empty) + "|" +
                (_stickyHoverKey ?? string.Empty) + "|" +
                (hoverKey ?? string.Empty) + "|" +
                FormatRect(_stickyHoverAnchorRect) + "|" +
                FormatRect(_stickyHoverTooltipRect);
            if (string.Equals(_lastHoverClearLogKey, clearKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastHoverClearLogKey = clearKey;
            EditorInteractionLog.WriteHover("Clearing sticky hover. Reason=" + (reason ?? string.Empty) +
                ", stickyKey=" + (_stickyHoverKey ?? string.Empty) +
                ", activeKey=" + (hoverKey ?? string.Empty) +
                ", anchor=" + FormatRect(_stickyHoverAnchorRect) +
                ", tooltip=" + FormatRect(_stickyHoverTooltipRect) +
                ", bridge=" + FormatRect(BuildHoverBridgeRect()) + ".");
        }

        private void LogHoverRetargetSuppressed(CodeViewToken hoveredToken, string hoverKey)
        {
            var suppressionKey = (_stickyHoverKey ?? string.Empty) + "|" + (hoverKey ?? string.Empty);
            if (string.Equals(_lastHoverRetargetLogKey, suppressionKey, StringComparison.Ordinal))
            {
                return;
            }

            _lastHoverRetargetLogKey = suppressionKey;
            EditorInteractionLog.WriteHover("Suppressed hover retarget from sticky key " +
                (_stickyHoverKey ?? string.Empty) + " to " + (hoverKey ?? string.Empty) +
                " for token " + (hoveredToken != null ? hoveredToken.RawText.Trim() : string.Empty) +
                " @ " + (hoveredToken != null ? hoveredToken.LineNumber : 0) + ":" + (hoveredToken != null ? hoveredToken.Column : 0) +
                ". Anchor=" + FormatRect(_stickyHoverAnchorRect) +
                ", Tooltip=" + FormatRect(_stickyHoverTooltipRect) + ".");
        }

        private static void SetVisibleHover(
            CortexShellState state,
            string hoverKey,
            LanguageServiceHoverResponse response,
            LanguageServiceHoverDisplayPart hoveredPart)
        {
            if (state == null || state.Editor == null)
            {
                return;
            }

            state.Editor.VisibleHoverKey = hoverKey ?? string.Empty;
            state.Editor.VisibleHoverDefinitionDocumentPath =
                hoveredPart != null && hoveredPart.IsInteractive && !string.IsNullOrEmpty(hoveredPart.DefinitionDocumentPath)
                    ? hoveredPart.DefinitionDocumentPath
                    : (response != null ? response.DefinitionDocumentPath ?? string.Empty : string.Empty);
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

        private void DrawContextMenu(
            CortexShellState state,
            Event current,
            Vector2 localMouse,
            Vector2 viewportSize,
            ICommandRegistry commandRegistry)
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
                _contextMenuStyle,
                _contextMenuButtonStyle,
                _contextMenuHeaderStyle);
            _lastContextMenuRect = menuResult.MenuRect;

            if (!string.IsNullOrEmpty(menuResult.ActivatedCommandId))
            {
                _contextMenuService.Execute(state, commandRegistry, _contextInvocation, menuResult.ActivatedCommandId);
                CloseContextMenu();
                return;
            }

            if (menuResult.ShouldClose)
            {
                CloseContextMenu();
            }
        }

        private void OpenContextMenu(
            DocumentSession session,
            CortexShellState state,
            CodeViewToken token,
            Vector2 localMouse,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry)
        {
            EditorCommandInvocation invocation;
            if (!TryBuildCommandTarget(session, state, token, out invocation))
            {
                MMLog.WriteWarning("[Cortex.Harmony] Context menu target creation failed for decompiled token. Document='" +
                    (session != null ? session.FilePath ?? string.Empty : string.Empty) +
                    "', Token='" + (token != null ? token.RawText ?? string.Empty : string.Empty) + "'.");
                CloseContextMenu();
                return;
            }

            var target = invocation != null ? invocation.Target : null;
            MMLog.WriteInfo("[Cortex.Harmony] Opening editor context menu. Document='" +
                (target != null ? target.DocumentPath ?? string.Empty : string.Empty) +
                "', Symbol='" + (target != null ? target.SymbolText ?? string.Empty : string.Empty) +
                "', Position=" + (target != null ? target.AbsolutePosition : -1) +
                ", Line=" + (target != null ? target.Line : 0) +
                ", Column=" + (target != null ? target.Column : 0) + ".");

            var items = _contextMenuService.BuildItems(state, commandRegistry, contributionRegistry, invocation);
            if (items == null || items.Count == 0)
            {
                MMLog.WriteInfo("[Cortex.Harmony] Context menu produced no visible items for symbol '" +
                    (target != null ? target.SymbolText ?? string.Empty : string.Empty) + "'.");
                CloseContextMenu();
                return;
            }

            SetSelectedToken(session, token);
            _contextMenuOpen = true;
            _contextMenuPosition = localMouse;
            _contextInvocation = invocation;
            _popupMenuSurface.Reset();
            PopulatePopupMenuItems(items);
        }

        private bool TryBuildCommandTarget(
            DocumentSession session,
            CortexShellState state,
            CodeViewToken token,
            out EditorCommandInvocation invocation)
        {
            invocation = null;
            if (session == null || token == null)
            {
                return false;
            }

            return _commandContextFactory.TryCreateTokenInvocation(
                session,
                state,
                token.Start,
                token.LineNumber,
                token.Column,
                token.RawText,
                ResolveHoverResponse(state, token),
                CanNavigateToDefinition(token),
                out invocation);
        }

        private LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, CodeViewToken token)
        {
            if (state == null || state.Editor == null || token == null)
            {
                return null;
            }

            return string.Equals(state.Editor.ActiveHoverKey, token.Key, StringComparison.Ordinal)
                ? state.Editor.ActiveHoverResponse
                : null;
        }

        private void SetSelectedToken(DocumentSession session, CodeViewToken token)
        {
            if (token == null)
            {
                _selectedTokenKey = string.Empty;
                _selectedTokenText = string.Empty;
                _selectedTokenClassification = string.Empty;
                _selectedDocumentPath = string.Empty;
                return;
            }

            _selectedTokenKey = token.Key ?? string.Empty;
            _selectedTokenText = token.RawText ?? string.Empty;
            _selectedTokenClassification = token.Classification ?? string.Empty;
            _selectedDocumentPath = session != null ? session.FilePath ?? string.Empty : string.Empty;
        }

        private bool IsRelatedSelectionToken(DocumentSession session, CodeViewToken token)
        {
            if (session == null || token == null || string.IsNullOrEmpty(_selectedTokenText))
            {
                return false;
            }

            if (!string.Equals(session.FilePath ?? string.Empty, _selectedDocumentPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!CanParticipateInRelatedSelection(token.Classification, token.RawText) ||
                !CanParticipateInRelatedSelection(_selectedTokenClassification, _selectedTokenText))
            {
                return false;
            }

            return string.Equals(token.RawText ?? string.Empty, _selectedTokenText, StringComparison.Ordinal);
        }

        private bool CanParticipateInRelatedSelection(string classification, string rawText)
        {
            var normalizedText = NormalizeForDisplay(rawText ?? string.Empty).Trim();
            if (normalizedText.Length <= 1 || !_classificationPresentationService.CanNavigateToDefinition(classification, normalizedText))
            {
                return false;
            }

            var normalizedClassification = _classificationPresentationService.NormalizeClassification(classification);
            return normalizedClassification.IndexOf("keyword", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private void DrawSelectionOutline(Rect rect)
        {
            if (_selectionOutlineFill == null || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            const float thickness = 1f;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, thickness), _selectionOutlineFill);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - thickness, rect.width, thickness), _selectionOutlineFill);
            GUI.DrawTexture(new Rect(rect.x, rect.y, thickness, rect.height), _selectionOutlineFill);
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.y, thickness, rect.height), _selectionOutlineFill);
        }

        private void DrawHoverOutline(Rect rect)
        {
            if (_hoverOutlineFill == null || rect.width <= 0f || rect.height <= 0f)
            {
                return;
            }

            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 1f), _hoverOutlineFill);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), _hoverOutlineFill);
        }

        private Rect GetViewportAnchorRect(EditorCommandTarget target, Vector2 scroll, float gutterWidth)
        {
            var contentRect = GetTargetContentRect(target, gutterWidth);
            return new Rect(contentRect.x - scroll.x, contentRect.y - scroll.y, contentRect.width, contentRect.height);
        }

        private Rect GetMethodInspectorViewportAnchorRect(DocumentSession session, EditorCommandTarget target, Vector2 scroll, float gutterWidth)
        {
            Rect blockRect;
            if (TryBuildMethodTargetRect(session, target, gutterWidth, out blockRect))
            {
                return new Rect(blockRect.x - scroll.x, blockRect.y - scroll.y, blockRect.width, blockRect.height);
            }

            return GetViewportAnchorRect(target, scroll, gutterWidth);
        }

        private Rect GetTargetContentRect(EditorCommandTarget target, float gutterWidth)
        {
            if (target == null)
            {
                return new Rect(gutterWidth, 0f, 2f, _lineHeight);
            }

            var token = FindTokenByTarget(target);
            if (token != null && token.ContentRect.width > 0f && token.ContentRect.height > 0f)
            {
                return token.ContentRect;
            }

            var line = FindVisibleLineByLineNumber(target.Line);
            if (line != null)
            {
                return new Rect(gutterWidth, line.Y, 2f, _lineHeight);
            }

            return new Rect(gutterWidth, 0f, 2f, _lineHeight);
        }

        private CodeViewToken FindTokenByTarget(EditorCommandTarget target)
        {
            if (target == null || _layout == null || _layout.VisibleLines.Count == 0)
            {
                return null;
            }

            for (var lineIndex = 0; lineIndex < _layout.VisibleLines.Count; lineIndex++)
            {
                var line = _layout.VisibleLines[lineIndex];
                if (line == null || line.Tokens.Count == 0)
                {
                    continue;
                }

                for (var tokenIndex = 0; tokenIndex < line.Tokens.Count; tokenIndex++)
                {
                    var token = line.Tokens[tokenIndex];
                    if (token == null)
                    {
                        continue;
                    }

                    if (target.AbsolutePosition >= token.Start && target.AbsolutePosition < token.Start + Mathf.Max(1, token.Length))
                    {
                        return token;
                    }

                    if (token.LineNumber == target.Line && token.Column == target.Column)
                    {
                        return token;
                    }
                }
            }

            return null;
        }

        private bool TryBuildMethodTargetRect(DocumentSession session, EditorCommandTarget target, float gutterWidth, out Rect rect)
        {
            rect = new Rect(0f, 0f, 0f, 0f);
            var outline = _methodTargetOutlineService.FindOutline(session, target);
            return TryBuildMethodTargetRect(outline, gutterWidth, out rect);
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

        private void CloseContextMenu()
        {
            _contextMenuOpen = false;
            _contextInvocation = null;
            _lastContextMenuRect = new Rect(0f, 0f, 0f, 0f);
            _popupMenuSurface.Reset();
            _popupMenuItems.Clear();
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

        private void AddTokenRuns(CodeViewLine line, string documentIdentity, int absoluteStart, string raw, string classification)
        {
            if (line == null || string.IsNullOrEmpty(raw))
            {
                return;
            }

            if (!string.IsNullOrEmpty(classification))
            {
                AddToken(line, documentIdentity, absoluteStart, raw, classification);
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

                AddToken(line, documentIdentity, absoluteStart + offset, raw.Substring(offset, runLength), string.Empty);
                offset += runLength;
            }
        }

        private void AddToken(CodeViewLine line, string documentIdentity, int absoluteStart, string raw, string classification)
        {
            if (line == null || string.IsNullOrEmpty(raw))
            {
                return;
            }

            line.RawText = (line.RawText ?? string.Empty) + raw;

            var token = new CodeViewToken
            {
                Key = BuildTokenKey(documentIdentity, absoluteStart, raw.Length),
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
            GuiStyleUtil.ApplyTextColorToAllStates(style, _classificationPresentationService.GetColor(key));
            _classificationStyles[key] = style;
            return style;
        }

        private float MeasureToken(string displayText, string classification)
        {
            _sharedContent.text = string.IsNullOrEmpty(displayText) ? " " : displayText;
            return GetClassificationStyle(classification).CalcSize(_sharedContent).x;
        }

        private List<NormalizedSpan> NormalizeSpans(int textLength, LanguageServiceClassifiedSpan[] spans)
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
                    Classification = _classificationPresentationService.ResolvePresentationClassification(
                        span.Classification,
                        span.SemanticTokenType)
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

        private bool CanHoverToken(CodeViewToken token)
        {
            return IsInteractiveToken(token) &&
                _classificationPresentationService.IsHoverCandidate(
                    token != null ? token.Classification : string.Empty,
                    token != null ? token.RawText : string.Empty);
        }

        private bool CanNavigateToDefinition(CodeViewToken token)
        {
            return IsInteractiveToken(token) &&
                _classificationPresentationService.CanNavigateToDefinition(
                    token != null ? token.Classification : string.Empty,
                    token != null ? token.RawText : string.Empty);
        }

        private string GetEffectiveTokenClassification(CodeViewLine line, int tokenIndex)
        {
            if (line == null || tokenIndex < 0 || tokenIndex >= line.Tokens.Count)
            {
                return string.Empty;
            }

            var token = line.Tokens[tokenIndex];
            return _fallbackColoringService.GetEffectiveCodeViewClassification(
                token != null ? token.Classification : string.Empty,
                token != null ? token.RawText : string.Empty,
                GetAdjacentTokenText(line, tokenIndex, -1),
                GetAdjacentTokenText(line, tokenIndex, 1),
                GetAdjacentTokenText(line, tokenIndex, 2));
        }

        private static string GetAdjacentTokenText(CodeViewLine line, int tokenIndex, int offset)
        {
            if (line == null || line.Tokens == null || line.Tokens.Count == 0 || offset == 0)
            {
                return string.Empty;
            }

            var direction = offset > 0 ? 1 : -1;
            var remaining = Math.Abs(offset);
            for (var index = tokenIndex + direction; index >= 0 && index < line.Tokens.Count; index += direction)
            {
                var token = line.Tokens[index];
                var rawText = token != null ? token.RawText : string.Empty;
                if (string.IsNullOrEmpty(rawText) || rawText.Trim().Length == 0)
                {
                    continue;
                }

                remaining--;
                if (remaining == 0)
                {
                    return rawText;
                }
            }

            return string.Empty;
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

        private static string BuildTokenKey(string documentIdentity, int start, int length)
        {
            return (documentIdentity ?? string.Empty).ToLowerInvariant() + "|" + start + ":" + length;
        }

        private static string BuildTooltipPartKey(LanguageServiceHoverDisplayPart part)
        {
            if (part == null)
            {
                return string.Empty;
            }

            var definitionRange = part.DefinitionRange;

            return (part.SymbolDisplay ?? string.Empty) + "|" +
                (part.DocumentationCommentId ?? string.Empty) + "|" +
                (part.DefinitionDocumentPath ?? string.Empty) + "|" +
                (definitionRange != null ? definitionRange.Start : 0) + ":" +
                (definitionRange != null ? definitionRange.Length : 0);
        }

        private static string FormatRect(Rect rect)
        {
            return "(" +
                Mathf.RoundToInt(rect.x) + "," +
                Mathf.RoundToInt(rect.y) + "," +
                Mathf.RoundToInt(rect.width) + "," +
                Mathf.RoundToInt(rect.height) + ")";
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
            public Rect ContentRect;
        }

        private sealed class TooltipPartVisual
        {
            public LanguageServiceHoverDisplayPart Part;
            public Rect Rect;
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
