using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
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
        private const double DoubleClickThresholdSeconds = 0.32d;
        private const int CompletionVisibleItemCount = 8;
        private static readonly Rect EmptyRect = new Rect(0f, 0f, 0f, 0f);

        private readonly IEditorService _editorService = new EditorService();
        private readonly IEditorKeybindingService _keybindingService = new EditorKeybindingService();
        private readonly DocumentLanguageAnalysisService _documentLanguageAnalysisService = new DocumentLanguageAnalysisService();
        private readonly EditorCompletionService _editorCompletionService = new EditorCompletionService();
        private readonly EditorSignatureHelpService _editorSignatureHelpService = new EditorSignatureHelpService();
        private readonly EditorSemanticPopupSurface _semanticPopupSurface;
        private readonly EditorCommandContextFactory _commandContextFactory = new EditorCommandContextFactory();
        private readonly IEditorContextService _contextService;
        private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();
        private readonly EditorContextMenuService _contextMenuService = new EditorContextMenuService();
        private readonly EditorOverlayInteractionService _overlayInteractionService = new EditorOverlayInteractionService();
        private readonly EditorMethodInspectorService _methodInspectorService;
        private readonly EditorMethodTargetOutlineService _methodTargetOutlineService = new EditorMethodTargetOutlineService();
        private readonly IEditorMethodInspectorOverlayController _methodInspectorOverlayController;
        private readonly EditorToolbarService _toolbarService = new EditorToolbarService();
        private readonly SourceEditorCommandRouterService _commandRouterService = new SourceEditorCommandRouterService();
        private readonly EditorHoverService _hoverService;
        private readonly EditorClassificationPresentationService _classificationPresentationService = new EditorClassificationPresentationService();
        private readonly EditorFallbackColoringService _fallbackColoringService = new EditorFallbackColoringService();
        private readonly EditorCaretIndicatorPresentationService _caretIndicatorPresentationService = new EditorCaretIndicatorPresentationService();
        private readonly IPopupMenuRenderer _popupMenuRenderer;
        private readonly IHoverTooltipRenderer _hoverTooltipRenderer;
        private readonly GUIContent _measureContent = new GUIContent();
        private readonly List<NormalizedSpan> _orderedSpans = new List<NormalizedSpan>();
        private readonly Dictionary<string, GUIStyle> _classificationStyles = new Dictionary<string, GUIStyle>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PopupMenuItemModel> _popupMenuItems = new List<PopupMenuItemModel>();

        private GUIStyle _codeStyle;
        private GUIStyle _gutterStyle;
        private GUIStyle _completionPopupStyle;
        private GUIStyle _completionItemStyle;
        private GUIStyle _completionItemSelectedStyle;
        private GUIStyle _completionDetailStyle;
        private GUIStyle _documentationStyle;
        private GUIStyle _inlineSuggestionStyle;
        private Texture2D _selectionFill;
        private Texture2D _caretFill;
        private Texture2D _caretReadyFill;
        private Texture2D _caretIndicatorFill;
        private Texture2D _currentLineFill;
        private Texture2D _currentLineEdgeFill;
        private Texture2D _relatedSelectionFill;
        private Texture2D _declarationOccurrenceFill;
        private Texture2D _selectedOccurrenceFill;
        private Texture2D _selectionOutlineFill;
        private Texture2D _surfaceFill;
        private Texture2D _completionPopupFill;
        private Texture2D _completionSelectedFill;
        private Texture2D _completionBorderFill;
        private Texture2D _methodTargetFill;
        private Texture2D _methodTargetOutlineFill;
        private Texture2D _methodTargetGlowFill;
        private string _styleCacheKey = string.Empty;
        private float _lineHeight = DefaultLineHeight;
        private bool _hasFocus;
        private bool _isDraggingSelection;
        private int _dragAnchorIndex;
        private int _lastClickIndex = -1;
        private DateTime _lastClickUtc = DateTime.MinValue;
        private bool _contextMenuOpen;
        private Vector2 _contextMenuPosition = Vector2.zero;
        private EditorCommandInvocation _contextInvocation;
        private string _lastDrawError = string.Empty;
        private DateTime _lastDrawErrorUtc = DateTime.MinValue;
        private LayoutCache _layout;
        private Rect _lastContextMenuRect;
        private Rect _lastMethodInspectorRect;

        public EditableCodeViewSurface(
            IEditorContextService contextService,
            EditorHoverService hoverService,
            IOverlayRendererFactory overlayRendererFactory,
            IEditorMethodInspectorOverlayController methodInspectorOverlayController = null)
        {
            _contextService = contextService;
            _semanticPopupSurface = new EditorSemanticPopupSurface(contextService);
            _methodInspectorService = new EditorMethodInspectorService(contextService);
            _methodInspectorOverlayController = methodInspectorOverlayController ?? new EditorMethodInspectorOverlayController(contextService);
            _hoverService = hoverService ?? new EditorHoverService(contextService);
            _popupMenuRenderer = overlayRendererFactory != null ? overlayRendererFactory.CreatePopupMenuRenderer() : null;
            _hoverTooltipRenderer = overlayRendererFactory != null ? overlayRendererFactory.CreateHoverTooltipRenderer() : null;
        }

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
            public float LineWidth;
        }

        public Vector2 Draw(
            Rect rect,
            Vector2 scroll,
            DocumentSession session,
            bool editingEnabled,
            EditorSurfaceServices services,
            EditorSurfaceRenderContext renderContext)
        {
            if (session == null || !session.SupportsEditing || rect.width <= 0f || rect.height <= 0f)
            {
                _hasFocus = false;
                _isDraggingSelection = false;
                return scroll;
            }

            try
            {
                var documentService = services != null ? services.DocumentService : null;
                var commandRegistry = services != null ? services.CommandRegistry : null;
                var contributionRegistry = services != null ? services.ContributionRegistry : null;
                var state = services != null ? services.State : null;
                var themeKey = renderContext != null ? renderContext.ThemeKey : string.Empty;
                var codeStyle = renderContext != null ? renderContext.CodeStyle : null;
                var gutterStyle = renderContext != null ? renderContext.GutterStyle : null;
                var panelRenderer = renderContext != null ? renderContext.PanelRenderer : null;
                var blockedRect = renderContext != null ? ToUnityRect(renderContext.BlockedRect) : new Rect(0f, 0f, 0f, 0f);
                var gutterWidth = renderContext != null ? renderContext.GutterWidth : 52f;
                var popupMenuTheme = renderContext != null ? renderContext.PopupMenuTheme : null;
                var hoverTooltipTheme = renderContext != null ? renderContext.HoverTooltipTheme : null;
                var harmonyPatchGenerationService = services != null ? services.HarmonyPatchGenerationService : null;
                var generatedTemplateNavigationService = services != null ? services.GeneratedTemplateNavigationService : null;
                var projectCatalog = services != null ? services.ProjectCatalog : null;
                var loadedModCatalog = services != null ? services.LoadedModCatalog : null;
                var sourceLookupIndex = services != null ? services.SourceLookupIndex : null;
                var harmonyPatchInspectionService = services != null ? services.HarmonyPatchInspectionService : null;
                var harmonyPatchResolutionService = services != null ? services.HarmonyPatchResolutionService : null;
                var harmonyPatchDisplayService = services != null ? services.HarmonyPatchDisplayService : null;

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
                EnsureStyles(themeKey, codeStyle, gutterStyle);
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
                    _lastContextMenuRect = ToUnityRect(_popupMenuRenderer.PredictMenuRect(ToRenderPoint(_contextMenuPosition), ToRenderSize(rect.size), _popupMenuItems));
                }
                PreHandleContextMenuInput(current, localMouse);
                HandleKeyboardInput(session, state, editingEnabled, documentService, commandRegistry, current, generatedTemplateNavigationService, projectCatalog, harmonyPatchResolutionService, harmonyPatchGenerationService);
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
                var activeMethodInspectorTarget = _contextService.ResolveTarget(
                    state,
                    state != null && state.Editor != null && state.Editor.MethodInspector != null
                        ? state.Editor.MethodInspector.ContextKey
                        : string.Empty);
                var predictedMethodInspectorRect = _methodInspectorOverlayController.PredictRect(
                    state,
                    session,
                    GetMethodInspectorViewportAnchorRect(session, activeMethodInspectorTarget, scroll, gutterWidth),
                    rect.size);
                _lastMethodInspectorRect = predictedMethodInspectorRect;
                var pointerOnHoverSurface = hasMouse && _hoverService.IsPointerWithinHoverSurface(GetSurfaceId(session, state), ToRenderPoint(localMouse));
                var overlayPointerState = _overlayInteractionService.ResolvePointerState(
                    predictedMethodInspectorRect,
                    _lastContextMenuRect,
                    _contextMenuOpen,
                    pointerOnHoverSurface,
                    hasMouse,
                    localMouse);
                _overlayInteractionService.TraceScrollOwner("source-editor", current, overlayPointerState);
                _methodInspectorOverlayController.HandlePreDrawInput(current, predictedMethodInspectorRect, localMouse);
                var pointerContext = BuildPointerContext(current, rect, hasMouse, localMouse, scroll, true);
                var shouldUpdateHover = current == null ||
                    current.type == EventType.Repaint ||
                    current.type == EventType.MouseMove;
                var hoveredMethodTarget = hasMouse && !_isDraggingSelection && !pointerOnHoverSurface && IsMethodTargetSelectionMode()
                    ? FindMethodTargetAt(session, gutterWidth, pointerContext.ContentMouse)
                    : null;
                var hoverTarget = hasMouse && !_isDraggingSelection && !pointerOnHoverSurface
                    ? TryResolveHoverTarget(session, state, editingEnabled, pointerContext.ContentMouse, scroll, gutterWidth)
                    : null;
                var displayHoveredMethodTarget = ResolveDisplayedHoveredMethodTarget(session, state, hoverTarget, pointerOnHoverSurface);
                HandlePointerInput(session, state, editingEnabled, current, pointerContext, hoveredMethodTarget, gutterWidth, commandRegistry, contributionRegistry, harmonyPatchGenerationService, overlayPointerState);
                RefreshActiveContext(session, state, editingEnabled);
                if (shouldUpdateHover)
                {
                    _hoverService.UpdateHoverRequest(
                        state,
                        GetSurfaceId(session, state),
                        hoverTarget,
                        hasMouse && !_contextMenuOpen && !_isDraggingSelection && !pointerOnHoverSurface,
                        hasMouse,
                        ToRenderPoint(pointerContext.SurfaceMouse));
                }

                GUI.BeginGroup(rect);
                try
                {
                    var contentRect = new Rect(0f, 0f, Mathf.Max(rect.width - 18f, _layout.ContentWidth), Mathf.Max(rect.height - 18f, _layout.ContentHeight));
                    var preserveEditorScroll = _overlayInteractionService.ShouldPreserveEditorScroll(current, _contextMenuOpen, overlayPointerState);
                    var scrollBeforeDraw = scroll;
                    try
                    {
                        scroll = GUI.BeginScrollView(new Rect(0f, 0f, rect.width, rect.height), scroll, contentRect);
                        GUI.DrawTexture(new Rect(0f, 0f, contentRect.width, contentRect.height), _surfaceFill);
                        DrawLines(session, state, editingEnabled, scroll, rect.height, gutterWidth, displayHoveredMethodTarget);
                    }
                    finally
                    {
                        GUI.EndScrollView();
                    }

                    if (preserveEditorScroll)
                    {
                        scroll = scrollBeforeDraw;
                    }

                    _semanticPopupSurface.DrawQuickActions(
                        state,
                        GetViewportAnchorRect(session, _contextService.ResolveTarget(state, state != null && state.Semantic != null && state.Semantic.QuickActions != null ? state.Semantic.QuickActions.ContextKey : string.Empty), scroll, gutterWidth),
                        rect.size,
                        commandRegistry);
                    _lastMethodInspectorRect = _methodInspectorOverlayController.Draw(
                        state,
                        session,
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
                        null,
                        null,
                        null,
                        documentService,
                        projectCatalog,
                        loadedModCatalog,
                        sourceLookupIndex,
                        harmonyPatchInspectionService,
                        harmonyPatchResolutionService,
                        harmonyPatchDisplayService,
                        harmonyPatchGenerationService,
                        panelRenderer);
                    if (editingEnabled)
                    {
                        DrawCompletionPopup(session, state, scroll, rect.size, gutterWidth);
                    }
                        DrawSignatureHelpPopup(session, state, scroll, rect.size, gutterWidth);
                    _semanticPopupSurface.DrawRename(
                        state,
                        GetViewportAnchorRect(session, _contextService.ResolveTarget(state, state != null && state.Editor != null ? state.Editor.Rename.ContextKey : string.Empty), scroll, gutterWidth),
                        rect.size,
                        commandRegistry);
                    _semanticPopupSurface.DrawPeek(
                        state,
                        GetViewportAnchorRect(session, _contextService.ResolveTarget(state, state != null && state.Editor != null ? state.Editor.Peek.ContextKey : string.Empty), scroll, gutterWidth),
                        rect.size);
                    DrawHoverTooltip(services.NavigationService, state, GetSurfaceId(session, state), hoverTarget, pointerContext.SurfaceMouse, rect.size, hasMouse, hoverTooltipTheme);
                    DrawContextMenu(state, pointerContext.SurfaceMouse, rect.size, commandRegistry, popupMenuTheme);
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
                _documentationStyle != null &&
                _inlineSuggestionStyle != null &&
                _selectionFill != null &&
                _caretFill != null &&
                _caretReadyFill != null &&
                _caretIndicatorFill != null &&
                _currentLineFill != null &&
                _currentLineEdgeFill != null &&
                _relatedSelectionFill != null &&
                _selectedOccurrenceFill != null &&
                _selectionOutlineFill != null &&
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
            _caretReadyFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.38f));
            _caretIndicatorFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), Color.white, 0.18f), 0.96f));
            _currentLineFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetSurfaceColor(), 0.16f));
            _currentLineEdgeFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.28f));
            _relatedSelectionFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.14f));
            _declarationOccurrenceFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), new Color(0.60f, 0.82f, 0.42f, 1f), 0.72f), 0.26f));
            _selectedOccurrenceFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.28f));
            _selectionOutlineFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.92f));
            _completionPopupFill = MakeFill(CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.55f));
            _completionSelectedFill = MakeFill(CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetSurfaceColor(), 0.22f));
            _completionBorderFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.38f));
            _methodTargetFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.10f));
            _methodTargetOutlineFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.GetAccentColor(), 0.94f));
            _methodTargetGlowFill = MakeFill(CortexIdeLayout.WithAlpha(CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), Color.white, 0.18f), 0.16f));
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
            _documentationStyle = new GUIStyle(_codeStyle);
            _documentationStyle.wordWrap = true;
            _documentationStyle.clipping = TextClipping.Clip;
            GuiStyleUtil.ApplyTextColorToAllStates(_documentationStyle, CortexIdeLayout.GetTextColor());
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
            if (!_fallbackColoringService.ShouldApplyFallback(normalizedClassification))
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
                    ? _fallbackColoringService.GetEffectiveLineTokenClassification(
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
                    Classification = _classificationPresentationService.ResolvePresentationClassification(
                        span.Classification,
                        span.SemanticTokenType)
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

        private void DrawLines(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            Vector2 scroll,
            float viewportHeight,
            float gutterWidth,
            EditorMethodTargetOutline hoveredMethodTarget)
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
            var hasNamedImGuiFocus = HasNamedImGuiFocus();
            var isEditorContainerFocused = IsEditorContainerFocused(state);
            var isReadyForInput = _caretIndicatorPresentationService.IsReadyForInput(
                _hasFocus,
                editingEnabled,
                hasNamedImGuiFocus,
                isEditorContainerFocused);
            var hasEditorFocus = _hasFocus && !hasNamedImGuiFocus;
            var shouldDrawCaret = hasEditorFocus;
            var showTypingIndicator = _caretIndicatorPresentationService.ShouldDrawIndicator(
                _hasFocus,
                editingEnabled,
                hasNamedImGuiFocus,
                isEditorContainerFocused);
            if (IsMethodTargetSelectionMode())
            {
                DrawMethodTargetOutlines(session, firstLine, lastLine, gutterWidth, hoveredMethodTarget);
            }

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
                DrawCode(session, state, line, gutterWidth);
                if (shouldDrawCaret)
                {
                    for (var selectionIndex = 0; selectionIndex < selections.Length; selectionIndex++)
                    {
                        var caret = _editorService.GetCaretPosition(session, selections[selectionIndex].CaretIndex);
                        if (caret.Line == i)
                        {
                            DrawCaret(line, gutterWidth, selections[selectionIndex].CaretIndex, isReadyForInput);
                        }
                    }
                }

                if (showTypingIndicator && primaryCaret.Line == i)
                {
                    DrawTypingIndicator(line, gutterWidth, primarySelection.CaretIndex);
                }
            }

            if (hasEditorFocus && primaryCaret.Line >= 0 && primaryCaret.Line < _layout.Lines.Count)
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

        private void DrawCode(DocumentSession session, CortexShellState state, EditableLineLayout line, float gutterWidth)
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
                var segmentRect = new Rect(x, line.Y, width + 2f, _lineHeight);
                var isSelectedOccurrence = IsSelectedOccurrence(state, session, line, segment);
                var isDeclarationOccurrence = !isSelectedOccurrence && IsDeclarationOccurrence(state, session, line, segment);
                var isRelatedOccurrence = !isSelectedOccurrence && !isDeclarationOccurrence && IsRelatedOccurrence(state, session, line, segment);
                if (isRelatedOccurrence)
                {
                    GUI.DrawTexture(segmentRect, _relatedSelectionFill);
                }

                if (isDeclarationOccurrence)
                {
                    GUI.DrawTexture(segmentRect, _declarationOccurrenceFill);
                }

                if (isSelectedOccurrence)
                {
                    GUI.DrawTexture(segmentRect, _selectedOccurrenceFill);
                }

                GUI.Label(segmentRect, segment.DisplayText, style);
                if (isSelectedOccurrence)
                {
                    DrawSelectionOutline(segmentRect);
                }

                x += width;
            }
        }

        private void DrawCaret(EditableLineLayout line, float gutterWidth, int caretIndex, bool isReadyForInput)
        {
            var rawColumn = Mathf.Max(0, Mathf.Min(line.RawText.Length, caretIndex - line.StartIndex));
            var prefix = rawColumn > 0 ? line.RawText.Substring(0, rawColumn) : string.Empty;
            var x = gutterWidth + Measure(ExpandTabs(prefix));
            GUI.DrawTexture(new Rect(x, line.Y + 1f, 1.5f, _lineHeight - 2f), isReadyForInput ? _caretReadyFill : _caretFill);
        }

        private void DrawTypingIndicator(EditableLineLayout line, float gutterWidth, int caretIndex)
        {
            if (_caretIndicatorFill == null)
            {
                return;
            }

            var rawColumn = Mathf.Max(0, Mathf.Min(line.RawText.Length, caretIndex - line.StartIndex));
            var prefix = rawColumn > 0 ? line.RawText.Substring(0, rawColumn) : string.Empty;
            var x = gutterWidth + Measure(ExpandTabs(prefix));
            GUI.DrawTexture(new Rect(x - 0.25f, line.Y + 1f, 2f, _lineHeight - 2f), _caretIndicatorFill);
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

        private bool IsSelectedOccurrence(CortexShellState state, DocumentSession session, EditableLineLayout line, EditableSegment segment)
        {
            var context = GetSelectionContext(session, state);
            if (context == null || context.Target == null || line == null || segment == null)
            {
                return false;
            }

            var rawText = GetSegmentRawText(line, segment);
            if (string.IsNullOrEmpty(rawText))
            {
                return false;
            }

            var segmentStart = line.StartIndex + segment.StartInLine;
            return string.Equals(context.DocumentPath ?? string.Empty, session != null ? session.FilePath ?? string.Empty : string.Empty, StringComparison.OrdinalIgnoreCase) &&
                context.TargetStart == segmentStart &&
                context.TargetLength == segment.Length &&
                string.Equals(context.FocusTokenText ?? string.Empty, rawText, StringComparison.Ordinal);
        }

        private bool IsRelatedOccurrence(CortexShellState state, DocumentSession session, EditableLineLayout line, EditableSegment segment)
        {
            var context = GetSelectionContext(session, state);
            var selectedText = context != null ? context.FocusTokenText ?? string.Empty : string.Empty;
            if (session == null || line == null || segment == null || string.IsNullOrEmpty(selectedText))
            {
                return false;
            }

            if (context == null ||
                !string.Equals(session.FilePath ?? string.Empty, context.DocumentPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var rawText = GetSegmentRawText(line, segment);
            if (!CanParticipateInRelatedSelection(segment.Classification, rawText) ||
                !CanParticipateInRelatedSelection(context.Semantic != null ? context.Semantic.SymbolKind : string.Empty, selectedText))
            {
                return false;
            }

            return string.Equals(rawText, selectedText, StringComparison.Ordinal);
        }

        private bool IsDeclarationOccurrence(CortexShellState state, DocumentSession session, EditableLineLayout line, EditableSegment segment)
        {
            var context = GetSelectionContext(session, state);
            if (context == null || context.Semantic == null || session == null || line == null || segment == null)
            {
                return false;
            }

            var rawText = GetSegmentRawText(line, segment);
            if (string.IsNullOrEmpty(rawText))
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

            var segmentStart = line.StartIndex + segment.StartInLine;
            return semantic.DefinitionStart == segmentStart &&
                semantic.DefinitionLength == segment.Length &&
                string.Equals(context.FocusTokenText ?? string.Empty, rawText, StringComparison.Ordinal);
        }

        private EditorContextSnapshot GetSelectionContext(DocumentSession session, CortexShellState state)
        {
            var surfaceContext = _contextService.GetSurfaceContext(state, GetSurfaceId(session, state));
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

        private static string GetSegmentRawText(EditableLineLayout line, EditableSegment segment)
        {
            if (line == null || segment == null || string.IsNullOrEmpty(line.RawText) || segment.Length <= 0)
            {
                return string.Empty;
            }

            var start = Mathf.Max(0, Mathf.Min(line.RawText.Length, segment.StartInLine));
            var length = Mathf.Max(0, Mathf.Min(segment.Length, line.RawText.Length - start));
            return length > 0
                ? line.RawText.Substring(start, length)
                : string.Empty;
        }

        private bool IsMethodTargetSelectionMode()
        {
            var current = Event.current;
            return (current != null && current.control) ||
                Input.GetKey(KeyCode.LeftControl) ||
                Input.GetKey(KeyCode.RightControl);
        }

        private void DrawMethodTargetOutlines(
            DocumentSession session,
            int firstLineIndex,
            int lastLineIndex,
            float gutterWidth,
            EditorMethodTargetOutline hoveredMethodTarget)
        {
            var outlines = _methodTargetOutlineService.GetOutlines(session);
            for (var i = 0; i < outlines.Length; i++)
            {
                var outline = outlines[i];
                Rect blockRect;
                if (outline == null ||
                    !TryBuildMethodTargetRect(outline, gutterWidth, out blockRect))
                {
                    continue;
                }

                var startIndex = outline.StartLineNumber - 1;
                var endIndex = outline.EndLineNumber - 1;
                if (endIndex < firstLineIndex || startIndex > lastLineIndex)
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
                    DrawSelectionOutline(blockRect);
                }
            }
        }

        private void DrawInlineSuggestion(DocumentSession session, CortexShellState state, EditableLineLayout line, float gutterWidth)
        {
            if (session == null || state == null || state.Editor == null || line.RawText == null)
            {
                return;
            }

            string suffixText;
            if (!_editorCompletionService.TryGetInlineSuggestionSuffix(state.Editor.Completion, session, out suffixText) ||
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

            var completion = state.Editor.Completion;
            var response = completion.Response;
            if (!_editorCompletionService.IsVisibleForSession(completion, session))
            {
                if (HasVisibleCompletion(state))
                {
                    _editorCompletionService.ClearPopupCompletion(completion);
                }

                return;
            }

            var caret = _editorService.GetCaretPosition(session, session.EditorState.CaretIndex);
            if (caret.Line < 0 || caret.Line >= _layout.Lines.Count)
            {
                return;
            }

            _editorCompletionService.SyncSelection(completion);
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

            var selectedIndex = completion.SelectedIndex;
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

            var signatureHelp = state.Editor.SignatureHelp;
            if (!_editorSignatureHelpService.HasVisibleSignatureHelp(signatureHelp, session))
            {
                if (signatureHelp.Response != null)
                {
                    ClearSignatureHelp(state);
                }
                return;
            }

            var response = signatureHelp.Response;
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
                popupHeight += Mathf.Max(_lineHeight, _documentationStyle.CalcHeight(new GUIContent(detailText), popupWidth - 20f)) + 8f;
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
                GUI.Label(new Rect(popupRect.x + 8f, popupRect.y + _lineHeight + 8f, popupRect.width - 16f, popupRect.height - _lineHeight - 12f), detailText, _documentationStyle);
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
            EditorMethodTargetOutline hoveredMethodTarget,
            float gutterWidth,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            HarmonyPatchGenerationService harmonyPatchGenerationService,
            EditorOverlayPointerState overlayPointerState)
        {
            if (current == null)
            {
                return;
            }

            _overlayInteractionService.TracePointerRouting(
                "source-editor",
                current,
                overlayPointerState,
                state != null &&
                    state.Editor != null &&
                    state.Editor.MethodInspector != null &&
                    state.Editor.MethodInspector.IsVisible);

            if (_overlayInteractionService.ShouldBypassSurfaceInput(current, overlayPointerState))
            {
                return;
            }

            if (_overlayInteractionService.ShouldCloseMethodInspectorOnPointerDown(current, overlayPointerState, state))
            {
                _methodInspectorService.Close(state);
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

                if (current.control && current.clickCount == 1 && hoveredMethodTarget != null)
                {
                    _editorService.SetSelection(session, hoveredMethodTarget.AnchorStart, hoveredMethodTarget.AnchorStart);
                    _dragAnchorIndex = hoveredMethodTarget.AnchorStart;
                    session.EditorState.ScrollToCaretPending = false;
                    _isDraggingSelection = false;
                    EditorCommandInvocation methodInvocation;
                    var opened = TryBuildCommandTarget(session, state, editingEnabled, hoveredMethodTarget.AnchorStart, out methodInvocation) &&
                        _methodInspectorService.TryOpen(state, methodInvocation, hoveredMethodTarget.Classification);
                    MMLog.WriteInfo("[Cortex.MethodTargets] Ctrl+click on source method target. Document='" +
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

                var hitTest = GetCharacterIndexAt(session, pointerContext.ContentMouse, gutterWidth);
                var selectionAction = ApplyPointerSelection(session, current, hitTest.CharacterIndex);
                LogPointerSelection(session, selectionAction, pointerContext, gutterWidth, hitTest);
                session.EditorState.ScrollToCaretPending = false;
                _isDraggingSelection = true;
                if (HandleHarmonyInsertionPick(session, state, hitTest, harmonyPatchGenerationService))
                {
                    current.Use();
                    return;
                }

                if (current.control && current.clickCount == 1)
                {
                    EditorCommandInvocation invocation;
                    if (TryBuildCommandTarget(session, state, editingEnabled, hitTest.CharacterIndex, out invocation) &&
                        _methodInspectorService.TryOpen(state, invocation, GetClassificationAt(session, hitTest.CharacterIndex)))
                    {
                        current.Use();
                        return;
                    }
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

        private EditorMethodTargetOutline FindMethodTargetAt(DocumentSession session, float gutterWidth, Vector2 contentMouse)
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

        private bool TryBuildMethodTargetRect(EditorMethodTargetOutline outline, float gutterWidth, out Rect rect)
        {
            rect = EmptyRect;
            if (outline == null ||
                _layout == null ||
                _layout.Lines.Count == 0 ||
                outline.StartLineNumber <= 0 ||
                outline.EndLineNumber < outline.StartLineNumber)
            {
                return false;
            }

            var startIndex = outline.StartLineNumber - 1;
            var endIndex = outline.EndLineNumber - 1;
            if (startIndex < 0 || endIndex >= _layout.Lines.Count || endIndex < startIndex)
            {
                return false;
            }

            var top = _layout.Lines[startIndex].Y + 1f;
            var bottom = _layout.Lines[endIndex].Y + _lineHeight - 1f;
            var left = float.MaxValue;
            var right = gutterWidth + 8f;
            for (var lineIndex = startIndex; lineIndex <= endIndex; lineIndex++)
            {
                var line = _layout.Lines[lineIndex];
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

        private bool TryBuildMethodTargetRect(DocumentSession session, EditorCommandTarget target, float gutterWidth, out Rect rect)
        {
            rect = EmptyRect;
            var outline = _methodTargetOutlineService.FindOutline(session, target);
            return TryBuildMethodTargetRect(outline, gutterWidth, out rect);
        }

        private EditorMethodTargetOutline ResolveDisplayedHoveredMethodTarget(
            DocumentSession session,
            CortexShellState state,
            EditorHoverTarget hoverTarget,
            bool pointerOnHoverSurface)
        {
            if (hoverTarget != null && hoverTarget.Target != null)
            {
                return _methodTargetOutlineService.FindOutline(session, hoverTarget.Target);
            }

            if (!pointerOnHoverSurface || !IsMethodTargetSelectionMode())
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

        private float GetMethodTargetLineLeft(EditableLineLayout line, float gutterWidth)
        {
            if (line == null || line.Segments.Count == 0)
            {
                return gutterWidth;
            }

            for (var i = 0; i < line.Segments.Count; i++)
            {
                var segment = line.Segments[i];
                if (segment != null && !string.IsNullOrEmpty((segment.DisplayText ?? string.Empty).Trim()))
                {
                    var prefix = segment.StartInLine > 0
                        ? ExpandTabs(line.RawText.Substring(0, Math.Min(segment.StartInLine, line.RawText.Length)))
                        : string.Empty;
                    return gutterWidth + Measure(prefix) - 4f;
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

            if (_popupMenuRenderer != null)
            {
                _popupMenuRenderer.TryCapturePointerInput(ToRenderRect(_lastContextMenuRect), ToRenderPoint(localMouse));
            }
            if (current != null && current.type == EventType.ScrollWheel && !_lastContextMenuRect.Contains(localMouse))
            {
                current.Use();
            }
        }

        private EditorHoverTarget TryResolveHoverTarget(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            Vector2 contentMouse,
            Vector2 scroll,
            float gutterWidth)
        {
            PointerHitTestResult hitTest;
            Rect anchorRect;
            string classification;
            if (!TryGetHoverHitTest(session, contentMouse, gutterWidth, out hitTest, out anchorRect, out classification))
            {
                return null;
            }

            EditorCommandTarget interactionTarget;
            if (!_hoverService.TryCreateInteractionTarget(session, state, editingEnabled, hitTest.CharacterIndex, out interactionTarget) ||
                interactionTarget == null)
            {
                return null;
            }

            EditorHoverTarget hoverTarget;
            return _hoverService.TryCreateSourceHoverTarget(
                session,
                state,
                editingEnabled,
                GetSurfaceId(session, state),
                state != null ? state.Workbench.FocusedContainerId : string.Empty,
                EditorSurfaceKind.Source,
                interactionTarget.AbsolutePosition,
                ToRenderRect(new Rect(anchorRect.x - scroll.x, anchorRect.y - scroll.y, anchorRect.width, anchorRect.height)),
                classification,
                out hoverTarget)
                ? hoverTarget
                : null;
        }

        private bool TryGetHoverHitTest(
            DocumentSession session,
            Vector2 contentMouse,
            float gutterWidth,
            out PointerHitTestResult hitTest,
            out Rect anchorRect,
            out string classification)
        {
            hitTest = new PointerHitTestResult();
            anchorRect = EmptyRect;
            classification = string.Empty;
            if (_layout == null || _layout.Lines.Count == 0)
            {
                return false;
            }

            var lineIndex = Mathf.Clamp(Mathf.FloorToInt(contentMouse.y / _lineHeight), 0, _layout.Lines.Count - 1);
            var line = _layout.Lines[lineIndex];
            if (line == null || line.Segments.Count == 0 || contentMouse.x <= gutterWidth)
            {
                return false;
            }

            var x = gutterWidth;
            for (var i = 0; i < line.Segments.Count; i++)
            {
                var segment = line.Segments[i];
                if (segment == null || string.IsNullOrEmpty(segment.DisplayText))
                {
                    continue;
                }

                var width = Measure(segment.DisplayText);
                var segmentRect = new Rect(x, line.Y, width + 2f, _lineHeight);
                x += width;
                if (!segmentRect.Contains(contentMouse))
                {
                    continue;
                }

                var rawText = GetSegmentRawText(line, segment);
                if (!_classificationPresentationService.IsHoverCandidate(segment.Classification, rawText))
                {
                    return false;
                }

                classification = segment.Classification ?? string.Empty;
                anchorRect = segmentRect;
                hitTest = GetCharacterIndexAt(session, contentMouse, gutterWidth);
                return true;
            }

            return false;
        }

        private void DrawHoverTooltip(
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
                420f,
                "source-editor"))
            {
                return;
            }

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
            var hoverResponse = _hoverService.ResolveHoverResponse(state, session, target);
            var items = _contextMenuService.BuildItems(state, commandRegistry, contributionRegistry, invocation);
            if (items == null || items.Count == 0)
            {
                CloseContextMenu("no items available");
                return;
            }

            _contextMenuOpen = true;
            _contextMenuPosition = localMouse;
            _contextInvocation = invocation;
            if (_popupMenuRenderer != null)
            {
                _popupMenuRenderer.Reset();
            }
            PopulatePopupMenuItems(items);
            var enabledCount = CountEnabledPopupMenuItems();
            EditorInteractionLog.WriteContextMenu(
                "Opened context menu for '" + (target != null ? target.SymbolText ?? string.Empty : string.Empty) +
                "'. Items=" + _popupMenuItems.Count +
                ", Enabled=" + enabledCount +
                ", Mouse=(" + localMouse.x.ToString("F1") + "," + localMouse.y.ToString("F1") + ")" +
                ", TargetSymbol='" + (target != null ? target.SymbolText ?? string.Empty : string.Empty) + "'" +
                ", AbsolutePosition=" + absolutePosition + ".");
            CortexDeveloperLog.WriteSymbolContextTarget(
                "source-editor",
                target != null ? target.SymbolText ?? string.Empty : string.Empty,
                hoverResponse,
                absolutePosition);
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
            if (invocation != null)
            {
                _contextService.PublishInvocationContext(
                    state,
                    session,
                    GetSurfaceId(session, state),
                    state != null ? state.Workbench.FocusedContainerId : string.Empty,
                    EditorSurfaceKind.Source,
                    invocation,
                    true);
            }
            return invocation != null;
        }

        private EditorCommandInvocation BuildDocumentInvocation(DocumentSession session, CortexShellState state, bool editingEnabled, int absolutePosition)
        {
            var invocation = _commandContextFactory.CreateDocumentInvocation(session, state, editingEnabled, absolutePosition);
            if (invocation != null)
            {
                _contextService.PublishInvocationContext(
                    state,
                    session,
                    GetSurfaceId(session, state),
                    state != null ? state.Workbench.FocusedContainerId : string.Empty,
                    EditorSurfaceKind.Source,
                    invocation,
                    true);
            }

            return invocation;
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

        private Rect GetMethodInspectorViewportAnchorRect(DocumentSession session, EditorCommandTarget target, Vector2 scroll, float gutterWidth)
        {
            Rect blockRect;
            if (TryBuildMethodTargetRect(session, target, gutterWidth, out blockRect))
            {
                return new Rect(blockRect.x - scroll.x, blockRect.y - scroll.y, blockRect.width, blockRect.height);
            }

            return GetViewportAnchorRect(session, target, scroll, gutterWidth);
        }

        private string GetSurfaceId(DocumentSession session, CortexShellState state)
        {
            return _contextService.BuildSurfaceId(
                session != null ? session.FilePath ?? string.Empty : string.Empty,
                EditorSurfaceKind.Source,
                state != null && state.Workbench != null ? state.Workbench.FocusedContainerId : string.Empty);
        }

        private void RefreshActiveContext(DocumentSession session, CortexShellState state, bool editingEnabled)
        {
            if (session == null || state == null || session.EditorState == null)
            {
                return;
            }

            EditorCommandInvocation invocation;
            if (!TryBuildCommandTarget(session, state, editingEnabled, session.EditorState.CaretIndex, out invocation))
            {
                BuildDocumentInvocation(session, state, editingEnabled, session.EditorState.CaretIndex);
            }
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
                    ", TargetSymbol='" + (_contextInvocation != null && _contextInvocation.Target != null ? (_contextInvocation.Target.SymbolText ?? string.Empty) : string.Empty) + "'.");
            }

            _contextMenuOpen = false;
            _contextInvocation = null;
            _lastContextMenuRect = EmptyRect;
            if (_popupMenuRenderer != null)
            {
                _popupMenuRenderer.Reset();
            }
            _popupMenuItems.Clear();
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

        private void HandleKeyboardInput(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            IDocumentService documentService,
            ICommandRegistry commandRegistry,
            Event current,
            GeneratedTemplateNavigationService generatedTemplateNavigationService,
            IProjectCatalog projectCatalog,
            HarmonyPatchResolutionService harmonyPatchResolutionService,
            HarmonyPatchGenerationService harmonyPatchGenerationService)
        {
            if (current == null || current.type != EventType.KeyDown)
            {
                return;
            }

            if (!_caretIndicatorPresentationService.HasKeyboardOwnership(
                _hasFocus,
                HasNamedImGuiFocus(),
                IsEditorContainerFocused(state)))
            {
                return;
            }

            var selectionCountBefore = session != null && session.EditorState != null ? session.EditorState.Selections.Count : 0;
            var caretIndexBefore = session != null && session.EditorState != null ? session.EditorState.CaretIndex : 0;
            if (editingEnabled &&
                TryHandleHarmonyInsertionKeyboard(
                    session,
                    state,
                    current,
                    documentService,
                    projectCatalog,
                    harmonyPatchResolutionService,
                    harmonyPatchGenerationService,
                    generatedTemplateNavigationService))
            {
                current.Use();
                return;
            }

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

        private bool TryHandleHarmonyInsertionKeyboard(
            DocumentSession session,
            CortexShellState state,
            Event current,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            HarmonyPatchResolutionService harmonyPatchResolutionService,
            HarmonyPatchGenerationService harmonyPatchGenerationService,
            GeneratedTemplateNavigationService generatedTemplateNavigationService)
        {
            if (session == null ||
                state == null ||
                state.Harmony == null ||
                current == null ||
                current.keyCode != KeyCode.Tab ||
                current.shift ||
                current.control ||
                current.alt ||
                !state.Harmony.IsInsertionPickActive ||
                state.Harmony.GenerationRequest == null ||
                harmonyPatchGenerationService == null ||
                harmonyPatchResolutionService == null ||
                documentService == null)
            {
                return false;
            }

            var caretIndex = session.EditorState != null ? session.EditorState.CaretIndex : 0;
            var caret = _editorService.GetCaretPosition(session, caretIndex);
            string statusMessage;
            if (!harmonyPatchGenerationService.TryApplyEditorInsertionSelection(state, session, caret.Line + 1, caretIndex, out statusMessage))
            {
                state.StatusMessage = statusMessage;
                return true;
            }

            HarmonyResolvedMethodTarget resolvedTarget;
            string reason;
            if (!harmonyPatchResolutionService.TryResolveFromInspectionRequest(projectCatalog, state.Harmony.ActiveInspectionRequest, out resolvedTarget, out reason) ||
                resolvedTarget == null ||
                !harmonyPatchGenerationService.TryValidateGenerationTarget(state, resolvedTarget, out reason))
            {
                state.StatusMessage = reason;
                return true;
            }

            var preview = harmonyPatchGenerationService.BuildPreview(state, resolvedTarget, state.Harmony.GenerationRequest);
            if (preview == null || !preview.CanApply)
            {
                state.Harmony.GenerationPreview = preview;
                state.StatusMessage = preview != null ? preview.StatusMessage ?? string.Empty : "Harmony patch preview is not ready to apply.";
                return true;
            }

            DocumentSession appliedSession;
            if (!harmonyPatchGenerationService.Apply(state, documentService, state.Harmony.GenerationRequest, preview, out appliedSession, out statusMessage))
            {
                state.StatusMessage = statusMessage;
                return true;
            }

            state.Harmony.GenerationPreview = preview;
            if (appliedSession != null && appliedSession.EditorState != null)
            {
                appliedSession.EditorState.EditModeEnabled = true;
            }

            if (generatedTemplateNavigationService != null && appliedSession != null)
            {
                generatedTemplateNavigationService.StartSession(
                    state,
                    appliedSession,
                    preview.Placeholders,
                    preview.InsertionOffset,
                    preview.InsertionOffset + ((preview.SnippetText ?? string.Empty).Length));
            }

            harmonyPatchGenerationService.ClearEditorInsertionPick(state);
            state.StatusMessage = statusMessage;
            return true;
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

            var hasInlineSuggestion = _editorCompletionService.HasVisibleInlineSuggestion(state != null && state.Editor != null ? state.Editor.Completion : null, session);
            var response = state != null && state.Editor != null ? state.Editor.Completion.Response : null;
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

            _editorCompletionService.SyncSelection(state.Editor.Completion);
            switch (current.keyCode)
            {
                case KeyCode.UpArrow:
                    _editorCompletionService.MoveSelection(state.Editor.Completion, -1);
                    return true;
                case KeyCode.DownArrow:
                    _editorCompletionService.MoveSelection(state.Editor.Completion, 1);
                    return true;
                case KeyCode.PageUp:
                    _editorCompletionService.MoveSelection(state.Editor.Completion, -CompletionVisibleItemCount);
                    return true;
                case KeyCode.PageDown:
                    _editorCompletionService.MoveSelection(state.Editor.Completion, CompletionVisibleItemCount);
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
            if (state == null || state.Editor == null || !_editorSignatureHelpService.HasVisibleSignatureHelp(state.Editor.SignatureHelp, session))
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

        private string GetClassificationAt(DocumentSession session, int absolutePosition)
        {
            if (session == null || session.LanguageAnalysis == null || session.LanguageAnalysis.Classifications == null)
            {
                return string.Empty;
            }

            for (var i = 0; i < session.LanguageAnalysis.Classifications.Length; i++)
            {
                var span = session.LanguageAnalysis.Classifications[i];
                if (span == null)
                {
                    continue;
                }

                var start = span.Start;
                var end = span.Start + Math.Max(0, span.Length);
                if (absolutePosition >= start && absolutePosition < end)
                {
                    return _classificationPresentationService.ResolvePresentationClassification(
                        span.Classification,
                        span.SemanticTokenType);
                }
            }

            return string.Empty;
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
                    _editorSignatureHelpService.HasVisibleSignatureHelp(state.Editor.SignatureHelp, session))
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
                (HasVisibleCompletion(state) || _editorCompletionService.HasVisibleInlineSuggestion(state != null && state.Editor != null ? state.Editor.Completion : null, session)))
            {
                ClearCompletion(state);
            }

            if (_editorSignatureHelpService.HasVisibleSignatureHelp(state.Editor.SignatureHelp, session) && movementCommand)
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
                state != null && state.Editor != null ? state.Editor.Completion : null,
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
                state != null && state.Editor != null ? state.Editor.Completion : null,
                _editorService);
        }

        private bool ApplyInlineSuggestion(DocumentSession session, CortexShellState state)
        {
            return _editorCompletionService.ApplyInlineSuggestion(
                session,
                state != null && state.Editor != null ? state.Editor.Completion : null,
                _editorService);
        }

        private bool HasVisibleCompletion(CortexShellState state)
        {
            return state != null && state.Editor != null && _editorCompletionService.HasVisibleCompletion(state.Editor.Completion);
        }

        private void ClearCompletion(CortexShellState state)
        {
            _editorCompletionService.Reset(state != null && state.Editor != null ? state.Editor.Completion : null);
        }

        private void QueueSignatureHelpRequest(DocumentSession session, CortexShellState state, bool explicitInvocation, string triggerCharacter)
        {
            if (!_editorSignatureHelpService.QueueRequest(
                session,
                state != null && state.Editor != null ? state.Editor.SignatureHelp : null,
                _editorService,
                explicitInvocation,
                triggerCharacter))
            {
                ClearSignatureHelp(state);
            }
        }

        private void ClearSignatureHelp(CortexShellState state)
        {
            _editorSignatureHelpService.Reset(state != null && state.Editor != null ? state.Editor.SignatureHelp : null);
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
            result.LineWidth = line.Width;
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

        private static string NormalizeForDisplay(string raw)
        {
            return ExpandTabs(raw);
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

        private static bool HasNamedImGuiFocus()
        {
            return GUIUtility.keyboardControl != 0 &&
                !string.IsNullOrEmpty(GUI.GetNameOfFocusedControl());
        }

        private static bool IsEditorContainerFocused(CortexShellState state)
        {
            return state != null &&
                state.Workbench != null &&
                string.Equals(
                    state.Workbench.FocusedContainerId ?? string.Empty,
                    state.Workbench.EditorContainerId ?? string.Empty,
                    StringComparison.OrdinalIgnoreCase);
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
