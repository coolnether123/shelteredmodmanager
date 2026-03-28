using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.LanguageService.Protocol;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Modules.Editor
{
    internal sealed class CodeViewSurface
    {
        private const float MenuWidth = 190f;
        private const float MenuItemHeight = 24f;
        private const float TooltipWidth = 420f;
        private const float FoldGlyphWidth = 14f;
        private const int TabSize = 4;

        private readonly Dictionary<string, GUIStyle> _classificationStyles = new Dictionary<string, GUIStyle>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, HashSet<string>> _collapsedRegionKeys = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        private readonly GUIContent _sharedContent = new GUIContent();
        private readonly EditorSemanticPopupSurface _semanticPopupSurface;
        private readonly IEditorContextService _contextService;
        private readonly EditorCommandContextFactory _commandContextFactory = new EditorCommandContextFactory();
        private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();
        private readonly EditorSelectionInspectionService _selectionInspectionService = new EditorSelectionInspectionService();
        private readonly EditorClassificationPresentationService _classificationPresentationService = new EditorClassificationPresentationService();
        private readonly EditorContextMenuService _contextMenuService = new EditorContextMenuService();
        private readonly EditorOverlayInteractionService _overlayInteractionService = new EditorOverlayInteractionService();
        private readonly EditorMethodInspectorService _methodInspectorService;
        private readonly EditorMethodTargetOutlineService _methodTargetOutlineService = new EditorMethodTargetOutlineService();
        private readonly EditorMethodInspectorSurface _methodInspectorSurface;
        private readonly EditorHoverService _hoverService;
        private readonly IPopupMenuRenderer _popupMenuRenderer;
        private readonly IHoverTooltipRenderer _hoverTooltipRenderer;
        private readonly EditorFallbackColoringService _fallbackColoringService = new EditorFallbackColoringService();
        private readonly List<PopupMenuItemModel> _popupMenuItems = new List<PopupMenuItemModel>();
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
        private Texture2D _declarationSelectionFill;
        private Texture2D _selectedFill;
        private Texture2D _selectionOutlineFill;
        private Texture2D _lineHighlightFill;
        private Texture2D _navigationLineFill;
        private Texture2D _tooltipUnderlineFill;
        private Texture2D _methodTargetFill;
        private Texture2D _methodTargetOutlineFill;
        private Texture2D _methodTargetGlowFill;
        private float _lineHeight = 18f;
        private string _lastHoverClearLogKey = string.Empty;
        private bool _contextMenuOpen;
        private Vector2 _contextMenuPosition = Vector2.zero;
        private Rect _lastContextMenuRect = new Rect(0f, 0f, 0f, 0f);
        private Rect _lastMethodInspectorRect = new Rect(0f, 0f, 0f, 0f);
        private EditorCommandInvocation _contextInvocation;
        private string _lastDrawError = string.Empty;
        private string _lastFocusedDocumentPath = string.Empty;
        private int _lastFocusedLineNumber = -1;

        public CodeViewSurface(IEditorContextService contextService, EditorHoverService hoverService, IOverlayRendererFactory overlayRendererFactory)
        {
            _contextService = contextService;
            _semanticPopupSurface = new EditorSemanticPopupSurface(contextService);
            _methodInspectorService = new EditorMethodInspectorService(contextService);
            _methodInspectorSurface = new EditorMethodInspectorSurface(contextService);
            _hoverService = hoverService ?? new EditorHoverService(contextService);
            _popupMenuRenderer = overlayRendererFactory != null ? overlayRendererFactory.CreatePopupMenuRenderer() : null;
            _hoverTooltipRenderer = overlayRendererFactory != null ? overlayRendererFactory.CreateHoverTooltipRenderer() : null;
        }

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
            Rect blockedRect,
            float gutterWidth,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyPatchInspectionService,
            HarmonyPatchResolutionService harmonyPatchResolutionService,
            HarmonyPatchDisplayService harmonyPatchDisplayService,
            HarmonyPatchGenerationService harmonyPatchGenerationService,
            IPanelRenderer panelRenderer,
            PopupMenuThemePalette popupMenuTheme,
            HoverTooltipThemePalette hoverTooltipTheme)
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
                EnsureStyles(themeKey, baseStyle, gutterStyle, null, null, null, null);
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
                    if (_popupMenuRenderer != null)
                    {
                        _lastContextMenuRect = ToUnityRect(_popupMenuRenderer.PredictMenuRect(ToRenderPoint(_contextMenuPosition), ToRenderSize(rect.size), _popupMenuItems));
                    }
                }
                PreHandleContextMenuInput(current, localMouse);
                var pointerOnContextMenu = _contextMenuOpen && _overlayInteractionService.IsPointerWithin(_lastContextMenuRect, localMouse);
                var activeMethodInspectorTarget = _contextService.ResolveTarget(
                    state,
                    state != null && state.Editor != null && state.Editor.MethodInspector != null
                        ? state.Editor.MethodInspector.ContextKey
                        : string.Empty);
                var predictedMethodInspectorRect = _methodInspectorSurface.PredictRect(
                    state,
                    session.FilePath,
                    GetMethodInspectorViewportAnchorRect(session, activeMethodInspectorTarget, scroll, gutterWidth),
                    rect.size);
                _lastMethodInspectorRect = predictedMethodInspectorRect;
                var pointerOnMethodInspector = _overlayInteractionService.IsPointerWithin(predictedMethodInspectorRect, localMouse);
                var hoverSurfaceId = GetSurfaceId(session, state);
                var pointerOnTooltip = hasMouse && _hoverService.IsPointerWithinHoverSurface(hoverSurfaceId, ToRenderPoint(localMouse));
                var pointerOnOverlaySurface = pointerOnMethodInspector || pointerOnTooltip;
                _overlayInteractionService.TraceScrollOwner("decompiled-editor", current, pointerOnOverlaySurface, pointerOnContextMenu);
                _methodInspectorSurface.TryHandlePreDrawInput(current, predictedMethodInspectorRect, localMouse);
                var pointerOnHoverSurface = hasMouse && pointerOnOverlaySurface;
                var editorHoverActive = hasMouse && !pointerOnHoverSurface;
                var shouldUpdateHover = current == null ||
                    current.type == EventType.Repaint ||
                    current.type == EventType.MouseMove;
                var contentMouse = scroll + localMouse;
                var hoveredFoldRegion = editorHoverActive ? FindFoldRegionAt(contentMouse) : null;
                var hoveredMethodTarget = hoveredFoldRegion == null && editorHoverActive && IsMethodTargetSelectionMode()
                    ? FindMethodTargetAt(session, contentMouse, gutterWidth)
                    : null;
                var hoveredToken = hoveredFoldRegion == null && editorHoverActive ? FindTokenAt(contentMouse, gutterWidth) : null;
                hoveredToken = CanHoverToken(hoveredToken) ? hoveredToken : null;
                var hoverTarget = hoveredToken != null ? TryResolveHoverTarget(session, state, hoveredToken, scroll) : null;
                var displayHoveredToken = ResolveDisplayedHoveredToken(session, state, hoveredToken, pointerOnTooltip);
                var displayHoveredMethodTarget = ResolveDisplayedHoveredMethodTarget(session, state, hoverTarget, pointerOnTooltip);
                if (editorHoverActive &&
                    hoveredToken == null &&
                    state != null &&
                    state.Editor != null &&
                    !string.IsNullOrEmpty(state.Editor.Hover.ActiveContextKey))
                {
                    CortexDeveloperLog.WriteHoverDiagnostic(
                        "read-hit-test-miss",
                        state.Editor.Hover.RequestedKey ?? string.Empty,
                        "event=" + (current != null ? current.type.ToString() : string.Empty) +
                        ",x=" + contentMouse.x.ToString("F1") +
                        ",y=" + contentMouse.y.ToString("F1"));
                }

                if (shouldUpdateHover)
                {
                    _hoverService.UpdateHoverRequest(
                        state,
                        hoverSurfaceId,
                        hoverTarget,
                        editorHoverActive && !pointerOnTooltip,
                        hasMouse,
                        ToRenderPoint(localMouse));
                }
                HandlePointerInput(session, state, hoverSurfaceId, hoveredToken, hoverTarget, hoveredMethodTarget, hoveredFoldRegion, editorHoverActive, current, localMouse, rect.size, commandRegistry, contributionRegistry, pointerOnMethodInspector, pointerOnContextMenu);

                GUI.BeginGroup(rect);
                try
                {
                    var contentRect = new Rect(0f, 0f, Mathf.Max(rect.width - 18f, _layout.ContentWidth), Mathf.Max(rect.height - 18f, _layout.ContentHeight));
                    var preserveEditorScroll = _overlayInteractionService.ShouldPreserveEditorScroll(current, _contextMenuOpen, pointerOnOverlaySurface);
                    var scrollBeforeDraw = scroll;
                    try
                    {
                        scroll = GUI.BeginScrollView(new Rect(0f, 0f, rect.width, rect.height), scroll, contentRect);
                        try
                        {
                            DrawVisibleLines(state, session, scroll, rect.height, displayHoveredToken, displayHoveredMethodTarget, hoveredFoldRegion, gutterWidth);
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

                    DrawTooltip(navigationService, state, hoverSurfaceId, hoverTarget, localMouse, rect.size, hasMouse, hoverTooltipTheme);
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
                            _contextService.ResolveTarget(
                                state,
                                state != null && state.Editor != null && state.Editor.MethodInspector != null
                                    ? state.Editor.MethodInspector.ContextKey
                                    : string.Empty),
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
                        harmonyPatchGenerationService,
                        panelRenderer);
                    _semanticPopupSurface.DrawQuickActions(
                        state,
                        GetViewportAnchorRect(_contextService.ResolveTarget(state, state != null && state.Semantic != null && state.Semantic.QuickActions != null ? state.Semantic.QuickActions.ContextKey : string.Empty), scroll, gutterWidth),
                        rect.size,
                        commandRegistry);
                    _semanticPopupSurface.DrawRename(
                        state,
                        GetViewportAnchorRect(_contextService.ResolveTarget(state, state != null && state.Editor != null ? state.Editor.Rename.ContextKey : string.Empty), scroll, gutterWidth),
                        rect.size,
                        commandRegistry);
                    _semanticPopupSurface.DrawPeek(
                        state,
                        GetViewportAnchorRect(_contextService.ResolveTarget(state, state != null && state.Editor != null ? state.Editor.Peek.ContextKey : string.Empty), scroll, gutterWidth),
                        rect.size);
                    DrawContextMenu(state, localMouse, rect.size, commandRegistry, popupMenuTheme);
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
            _declarationSelectionFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), new Color(0.60f, 0.82f, 0.42f, 1f), 0.72f), 0.26f));
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

        private void DrawVisibleLines(CortexShellState state, DocumentSession session, Vector2 scroll, float viewHeight, CodeViewToken hoveredToken, EditorMethodTargetOutline hoveredMethodTarget, FoldRegion hoveredFoldRegion, float gutterWidth)
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
                    var isSelectedToken = IsSelectedToken(state, session, token);
                    var isDeclarationToken = !isSelectedToken && IsDeclarationToken(state, session, token);
                    var isRelatedSelection = !isSelectedToken && !isDeclarationToken && IsRelatedSelectionToken(state, session, token);
                    if (isRelatedSelection)
                    {
                        GUI.DrawTexture(tokenRect, _relatedSelectionFill);
                    }

                    if (isDeclarationToken)
                    {
                        GUI.DrawTexture(tokenRect, _declarationSelectionFill);
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
            string hoverSurfaceId,
            CodeViewToken hoveredToken,
            EditorHoverTarget hoverTarget,
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

            if (hoveredToken != null)
            {
                _hoverService.RequestHoverNow(state, hoverSurfaceId, hoverTarget);
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

            PublishSelectedTokenContext(session, state, hoveredToken);
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

            if (_popupMenuRenderer != null)
            {
                _popupMenuRenderer.TryCapturePointerInput(ToRenderRect(_lastContextMenuRect), ToRenderPoint(localMouse));
            }
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

            PublishSelectedTokenContext(session, state, token);
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

        private EditorHoverTarget TryResolveHoverTarget(DocumentSession session, CortexShellState state, CodeViewToken hoveredToken, Vector2 scroll)
        {
            if (session == null || hoveredToken == null)
            {
                return null;
            }

            EditorHoverTarget hoverTarget;
            return _hoverService.TryCreateReadOnlyHoverTarget(
                session,
                state,
                GetSurfaceId(session, state),
                state != null ? state.Workbench.FocusedContainerId : string.Empty,
                GetSurfaceKind(session),
                hoveredToken.Start,
                hoveredToken.LineNumber,
                hoveredToken.Column,
                hoveredToken.RawText,
                CanNavigateToDefinition(hoveredToken),
                _classificationPresentationService.NormalizeClassification(hoveredToken.Classification),
                ToRenderRect(ToViewportRect(hoveredToken.ContentRect, scroll)),
                out hoverTarget)
                ? hoverTarget
                : null;
        }

        private void DrawTooltip(
            CortexNavigationService navigationService,
            CortexShellState state,
            string surfaceId,
            EditorHoverTarget hoverTarget,
            Vector2 localMouse,
            Vector2 viewportSize,
            bool hasMouse,
            HoverTooltipThemePalette hoverTooltipTheme)
        {
            var current = Event.current;
            if (current != null &&
                current.type != EventType.Layout &&
                current.type != EventType.Repaint &&
                current.type != EventType.MouseMove &&
                current.type != EventType.MouseDown &&
                current.type != EventType.MouseUp)
            {
                CortexDeveloperLog.WriteHoverDiagnostic(
                    "read-draw-suppressed",
                    hoverTarget != null ? hoverTarget.HoverKey ?? string.Empty : string.Empty,
                    "event=" + current.type);
                return;
            }

            if (_hoverService.DrawHover(
                _hoverTooltipRenderer,
                navigationService,
                state,
                surfaceId,
                hoverTarget,
                ToRenderPoint(localMouse),
                ToRenderSize(viewportSize),
                hasMouse,
                hoverTooltipTheme,
                TooltipWidth,
                "decompiled-editor"))
            {
                return;
            }
        }

        private static Rect ToViewportRect(Rect contentRect, Vector2 scroll)
        {
            return new Rect(contentRect.x - scroll.x, contentRect.y - scroll.y, contentRect.width, contentRect.height);
        }

        private static Rect ClampTooltipRect(Rect rect, Vector2 viewportSize)
        {
            rect.x = Mathf.Min(rect.x, Mathf.Max(8f, viewportSize.x - rect.width - 12f));
            rect.y = Mathf.Min(rect.y, Mathf.Max(8f, viewportSize.y - rect.height - 12f));
            rect.x = Mathf.Max(8f, rect.x);
            rect.y = Mathf.Max(8f, rect.y);
            return rect;
        }

        private void ClearStickyHover(CortexShellState state)
        {
            _hoverService.ClearSurfaceHover(state, state != null && state.Documents != null && state.Documents.ActiveDocument != null ? GetSurfaceId(state.Documents.ActiveDocument, state) : string.Empty, _hoverTooltipRenderer);
        }

        private static RenderPoint ToRenderPoint(Vector2 point)
        {
            return new RenderPoint(point.x, point.y);
        }

        private static RenderSize ToRenderSize(Vector2 size)
        {
            return new RenderSize(size.x, size.y);
        }

        private static RenderRect ToRenderRect(Rect rect)
        {
            return new RenderRect(rect.x, rect.y, rect.width, rect.height);
        }

        private static Rect ToUnityRect(RenderRect rect)
        {
            return new Rect(rect.X, rect.Y, rect.Width, rect.Height);
        }

        private static bool HasArea(Rect rect)
        {
            return rect.width > 0f && rect.height > 0f;
        }

        private void DrawContextMenu(
            CortexShellState state,
            Vector2 localMouse,
            Vector2 viewportSize,
            ICommandRegistry commandRegistry,
            PopupMenuThemePalette popupMenuTheme)
        {
            if (!_contextMenuOpen || _contextInvocation == null || _contextInvocation.Target == null || _popupMenuItems.Count == 0 || _popupMenuRenderer == null)
            {
                return;
            }

            var menuResult = _popupMenuRenderer.Draw(
                ToRenderPoint(_contextMenuPosition),
                ToRenderSize(viewportSize),
                _contextInvocation.Target.SymbolText ?? string.Empty,
                _popupMenuItems,
                ToRenderPoint(localMouse),
                popupMenuTheme);
            _lastContextMenuRect = ToUnityRect(menuResult.MenuRect);

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

            PublishSelectedTokenContext(session, state, token);
            _contextMenuOpen = true;
            _contextMenuPosition = localMouse;
            _contextInvocation = invocation;
            if (_popupMenuRenderer != null)
            {
                _popupMenuRenderer.Reset();
            }
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

            var created = _commandContextFactory.TryCreateTokenInvocation(
                session,
                state,
                token.Start,
                token.LineNumber,
                token.Column,
                token.RawText,
                ResolveHoverResponse(state, token),
                CanNavigateToDefinition(token),
                out invocation);
            if (created && invocation != null)
            {
                _contextService.PublishInvocationContext(
                    state,
                    session,
                    GetSurfaceId(session, state),
                    state != null ? state.Workbench.FocusedContainerId : string.Empty,
                    GetSurfaceKind(session),
                    invocation,
                    true);
            }

            return created;
        }

        private LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, CodeViewToken token)
        {
            if (state == null || state.Editor == null || token == null)
            {
                return null;
            }

            return _contextService.ResolveHoverResponse(state, token.Key);
        }

        private bool IsRelatedSelectionToken(CortexShellState state, DocumentSession session, CodeViewToken token)
        {
            var context = GetSelectionContext(session, state);
            var selectedText = context != null ? context.FocusTokenText ?? string.Empty : string.Empty;
            if (session == null || token == null || string.IsNullOrEmpty(selectedText))
            {
                return false;
            }

            if (context == null ||
                !string.Equals(session.FilePath ?? string.Empty, context.DocumentPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!CanParticipateInRelatedSelection(token.Classification, token.RawText) ||
                !CanParticipateInRelatedSelection(context != null && context.Semantic != null ? context.Semantic.SymbolKind : string.Empty, selectedText))
            {
                return false;
            }

            return string.Equals(token.RawText ?? string.Empty, selectedText, StringComparison.Ordinal);
        }

        private bool IsDeclarationToken(CortexShellState state, DocumentSession session, CodeViewToken token)
        {
            var context = GetSelectionContext(session, state);
            if (context == null || context.Semantic == null || token == null || session == null)
            {
                return false;
            }

            var semantic = context.Semantic;
            if (semantic.DefinitionStart < 0 || semantic.DefinitionLength <= 0)
            {
                return false;
            }

            if (!string.Equals(
                semantic.DefinitionDocumentPath ?? context.DocumentPath ?? string.Empty,
                session.FilePath ?? string.Empty,
                StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return semantic.DefinitionStart == token.Start &&
                semantic.DefinitionLength == token.Length &&
                string.Equals(context.FocusTokenText ?? string.Empty, token.RawText ?? string.Empty, StringComparison.Ordinal);
        }

        private bool IsSelectedToken(CortexShellState state, DocumentSession session, CodeViewToken token)
        {
            var context = GetSelectionContext(session, state);
            if (context == null || context.Target == null || token == null)
            {
                return false;
            }

            return string.Equals(context.DocumentPath ?? string.Empty, session != null ? session.FilePath ?? string.Empty : string.Empty, StringComparison.OrdinalIgnoreCase) &&
                context.TargetStart == token.Start &&
                context.TargetLength == token.Length &&
                string.Equals(context.FocusTokenText ?? string.Empty, token.RawText ?? string.Empty, StringComparison.Ordinal);
        }

        private EditorContextSnapshot GetSurfaceContext(DocumentSession session, CortexShellState state)
        {
            return _contextService.GetSurfaceContext(state, GetSurfaceId(session, state));
        }

        private EditorContextSnapshot GetSelectionContext(DocumentSession session, CortexShellState state)
        {
            var surfaceContext = GetSurfaceContext(session, state);
            if (surfaceContext != null)
            {
                return surfaceContext;
            }

            var activeContext = _contextService.GetActiveContext(state);
            if (activeContext == null)
            {
                return null;
            }

            return string.Equals(activeContext.DocumentPath ?? string.Empty, session != null ? session.FilePath ?? string.Empty : string.Empty, StringComparison.OrdinalIgnoreCase)
                ? activeContext
                : null;
        }

        private void PublishSelectedTokenContext(DocumentSession session, CortexShellState state, CodeViewToken token)
        {
            EditorCommandInvocation invocation;
            if (!TryBuildCommandTarget(session, state, token, out invocation) || invocation == null)
            {
                return;
            }

            _contextService.PublishInvocationContext(
                state,
                session,
                GetSurfaceId(session, state),
                state != null ? state.Workbench.FocusedContainerId : string.Empty,
                GetSurfaceKind(session),
                invocation,
                true);
        }

        private string GetSurfaceId(DocumentSession session, CortexShellState state)
        {
            return _contextService.BuildSurfaceId(
                session != null ? session.FilePath ?? string.Empty : string.Empty,
                GetSurfaceKind(session),
                state != null && state.Workbench != null ? state.Workbench.FocusedContainerId : string.Empty);
        }

        private static EditorSurfaceKind GetSurfaceKind(DocumentSession session)
        {
            return session != null && session.Kind == DocumentKind.DecompiledCode
                ? EditorSurfaceKind.Decompiled
                : EditorSurfaceKind.ReadOnlyCode;
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

        private CodeViewToken ResolveDisplayedHoveredToken(DocumentSession session, CortexShellState state, CodeViewToken hoveredToken, bool pointerOnTooltip)
        {
            if (hoveredToken != null || !pointerOnTooltip)
            {
                return hoveredToken;
            }

            var activeHoverTarget = ResolveActiveHoverTarget(session, state);
            return activeHoverTarget != null ? FindTokenByTarget(activeHoverTarget) : null;
        }

        private EditorMethodTargetOutline ResolveDisplayedHoveredMethodTarget(
            DocumentSession session,
            CortexShellState state,
            EditorHoverTarget hoverTarget,
            bool pointerOnTooltip)
        {
            if (hoverTarget != null && hoverTarget.Target != null)
            {
                return _methodTargetOutlineService.FindOutline(session, hoverTarget.Target);
            }

            if (!pointerOnTooltip || !IsMethodTargetSelectionMode())
            {
                return null;
            }

            var activeHoverTarget = ResolveActiveHoverTarget(session, state);
            return activeHoverTarget != null ? _methodTargetOutlineService.FindOutline(session, activeHoverTarget) : null;
        }

        private EditorCommandTarget ResolveActiveHoverTarget(DocumentSession session, CortexShellState state)
        {
            if (session == null || state == null || state.Editor == null || state.Editor.Hover == null)
            {
                return null;
            }

            var activeContextKey = state.Editor.Hover.ActiveContextKey ?? string.Empty;
            if (string.IsNullOrEmpty(activeContextKey))
            {
                return null;
            }

            var target = _contextService.ResolveTarget(state, activeContextKey);
            return target != null &&
                string.Equals(target.DocumentPath ?? string.Empty, session.FilePath ?? string.Empty, StringComparison.OrdinalIgnoreCase)
                ? target
                : null;
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

                _popupMenuItems.Add(new PopupMenuItemModel
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
            if (_popupMenuRenderer != null)
            {
                _popupMenuRenderer.Reset();
            }
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
                float tokenStart;
                float tokenEnd;
                if (!TryGetHoverableTokenBounds(token, gutterWidth, out tokenStart, out tokenEnd))
                {
                    continue;
                }

                if (contentMouse.x >= tokenStart && contentMouse.x <= tokenEnd)
                {
                    return IsInteractiveToken(token) ? token : null;
                }
            }

            return null;
        }

        private bool TryGetHoverableTokenBounds(CodeViewToken token, float gutterWidth, out float tokenStart, out float tokenEnd)
        {
            tokenStart = 0f;
            tokenEnd = 0f;
            if (token == null || string.IsNullOrEmpty(token.DisplayText))
            {
                return false;
            }

            var displayText = token.DisplayText ?? string.Empty;
            var trimmedDisplay = displayText.Trim();
            if (string.IsNullOrEmpty(trimmedDisplay))
            {
                return false;
            }

            var leadingLength = displayText.Length - displayText.TrimStart().Length;
            var trailingLength = displayText.Length - displayText.TrimEnd().Length;
            var leadingDisplay = leadingLength > 0 ? displayText.Substring(0, leadingLength) : string.Empty;
            var trimmedLength = Math.Max(0, displayText.Length - leadingLength - trailingLength);
            var hoverDisplay = trimmedLength > 0 ? displayText.Substring(leadingLength, trimmedLength) : trimmedDisplay;

            tokenStart = gutterWidth + token.X + MeasureToken(leadingDisplay, token.Classification);
            tokenEnd = tokenStart + Mathf.Max(2f, MeasureToken(hoverDisplay, token.Classification));
            return true;
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
