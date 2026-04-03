using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Services.Inspector;
using Cortex.Services.Inspector.Actions;
using Cortex.Services.Inspector.Lifecycle;
using Cortex.Services.Navigation;
using Cortex.Services.Semantics.Context;
using UnityEngine;
using Cortex.Shell.Unity.Imgui;

namespace Cortex.Modules.Editor
{
    internal sealed class EditorMethodInspectorSurface
    {
        private const float PreferredPanelWidth = 430f;
        private const float MinimumPanelWidth = 320f;
        private const float PreferredPanelHeight = 520f;
        private const float MinimumPanelHeight = 260f;
        private const float PopupMargin = 8f;
        private const float PopupGap = 12f;
        private const float ScrollWheelStep = 28f;

        private readonly EditorMethodInspectorService _inspectorService;
        private readonly EditorMethodInspectorNavigationActionHandler _navigationActionHandler = new EditorMethodInspectorNavigationActionHandler();
        private readonly EditorMethodInspectorPanelDocumentAdapter _panelDocumentAdapter = new EditorMethodInspectorPanelDocumentAdapter();

        private Vector2 _scroll = Vector2.zero;

        public EditorMethodInspectorSurface(IEditorContextService contextService)
        {
            _inspectorService = new EditorMethodInspectorService(contextService);
        }

        public Rect Draw(
            CortexShellState state,
            DocumentSession session,
            string activeDocumentPath,
            Rect anchorRect,
            Vector2 surfaceSize,
            ICortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            GUIStyle containerStyle,
            GUIStyle buttonStyle,
            GUIStyle headerStyle,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            IEditorContributionRuntime extensionRuntime,
            IPanelRenderer panelRenderer)
        {
            if (!_inspectorService.IsVisibleForDocument(state, activeDocumentPath))
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            var preparedView = extensionRuntime != null
                ? extensionRuntime.PrepareInspector(
                    state,
                    session)
                : null;
            if (preparedView == null || preparedView.ViewModel == null)
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            var popupRect = ResolvePanelRect(anchorRect, surfaceSize);
            var panelDocument = _panelDocumentAdapter.Build(preparedView.ViewModel);
            var renderResult = panelRenderer != null && panelDocument != null
                ? panelRenderer.Draw(
                    new RenderRect(popupRect.x, popupRect.y, popupRect.width, popupRect.height),
                    panelDocument,
                    new RenderPoint(_scroll.x, _scroll.y),
                    BuildThemePalette())
                : null;
            _scroll = renderResult != null ? new Vector2(renderResult.Scroll.X, renderResult.Scroll.Y) : _scroll;
            HandleActivation(
                renderResult != null ? renderResult.ActivatedId : string.Empty,
                state,
                preparedView,
                navigationService,
                extensionRuntime);
            return popupRect;
        }

        public Rect PredictRect(CortexShellState state, string activeDocumentPath, Rect anchorRect, Vector2 surfaceSize)
        {
            if (!_inspectorService.IsVisibleForDocument(state, activeDocumentPath))
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            return ResolvePanelRect(anchorRect, surfaceSize);
        }

        public bool TryHandlePreDrawInput(Event current, Rect panelRect, Vector2 localPointer)
        {
            if (current == null ||
                panelRect.width <= 0f ||
                panelRect.height <= 0f ||
                !panelRect.Contains(localPointer))
            {
                return false;
            }

            if (current.type == EventType.ScrollWheel)
            {
                _scroll.y = Mathf.Max(0f, _scroll.y + (current.delta.y * ScrollWheelStep));
                current.Use();
            }

            return current.type == EventType.MouseDown ||
                current.type == EventType.MouseUp ||
                current.type == EventType.MouseDrag ||
                current.type == EventType.ScrollWheel ||
                current.type == EventType.ContextClick;
        }

        private void HandleActivation(
            string activatedId,
            CortexShellState state,
            EditorMethodInspectorPreparedView preparedView,
            ICortexNavigationService navigationService,
            IEditorContributionRuntime extensionRuntime)
        {
            if (string.IsNullOrEmpty(activatedId))
            {
                return;
            }

            MMLog.WriteInfo("[Cortex.Inspector] Activated inspector action. Id='" + activatedId + "'.");

            if (string.Equals(activatedId, PanelCommandIds.Close, StringComparison.Ordinal))
            {
                MMLog.WriteInfo("[Cortex.Overlay] Method inspector closed. Reason='close-button'.");
                _inspectorService.Close(state);
                return;
            }

            string sectionId;
            if (PanelCommandIds.TryGetSectionToggle(activatedId, out sectionId))
            {
                _inspectorService.ToggleSection(state, sectionId);
                return;
            }

            if (_navigationActionHandler.TryHandle(state, navigationService, activatedId))
            {
                return;
            }

            if (preparedView == null || preparedView.Invocation == null || preparedView.Invocation.Target == null)
            {
                return;
            }

            if (extensionRuntime != null)
            {
                var result = extensionRuntime.HandleInspectorAction(activatedId, preparedView);
                if (result != null && result.Handled)
                {
                    if (result.CloseInspector)
                    {
                        _inspectorService.Close(state);
                    }

                    return;
                }
            }
        }

        private static Rect ResolvePanelRect(Rect anchorRect, Vector2 viewportSize)
        {
            var panelSize = ResolvePanelSize(viewportSize);
            var panelRect = new Rect(
                anchorRect.xMax + PopupGap,
                anchorRect.y - 2f,
                panelSize.x,
                panelSize.y);

            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            {
                return panelRect;
            }

            var maxX = Mathf.Max(PopupMargin, viewportSize.x - panelRect.width - PopupMargin);
            var maxY = Mathf.Max(PopupMargin, viewportSize.y - panelRect.height - PopupMargin);
            if (panelRect.x > maxX)
            {
                var leftX = anchorRect.x - panelRect.width - PopupGap;
                panelRect.x = leftX >= PopupMargin ? leftX : maxX;
            }

            panelRect.x = Mathf.Max(PopupMargin, panelRect.x);
            panelRect.y = Mathf.Clamp(panelRect.y, PopupMargin, maxY);
            return panelRect;
        }

        private static Vector2 ResolvePanelSize(Vector2 viewportSize)
        {
            if (viewportSize.x <= 0f || viewportSize.y <= 0f)
            {
                return new Vector2(PreferredPanelWidth, PreferredPanelHeight);
            }

            var availableWidth = Mathf.Max(180f, viewportSize.x - (PopupMargin * 2f));
            var availableHeight = Mathf.Max(MinimumPanelHeight, viewportSize.y - (PopupMargin * 2f));
            var width = availableWidth < MinimumPanelWidth
                ? availableWidth
                : Mathf.Min(PreferredPanelWidth, availableWidth);
            var height = availableHeight < MinimumPanelHeight
                ? availableHeight
                : Mathf.Min(PreferredPanelHeight, availableHeight);
            return new Vector2(width, height);
        }

        private static PanelThemePalette BuildThemePalette()
        {
            var borderColor = ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetBorderColor(), 0.38f);
            return new PanelThemePalette
            {
                ThemeKey = "method-inspector",
                BackgroundColor = ToRenderColor(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), ImguiWorkbenchLayout.GetBackgroundColor(), 0.22f)),
                HeaderColor = ToRenderColor(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetHeaderColor(), ImguiWorkbenchLayout.GetSurfaceColor(), 0.18f)),
                BorderColor = ToRenderColor(borderColor),
                DividerColor = ToRenderColor(ImguiWorkbenchLayout.WithAlpha(ImguiWorkbenchLayout.Blend(borderColor, ImguiWorkbenchLayout.GetTextColor(), 0.1f), 0.46f)),
                ActionFillColor = ToRenderColor(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.72f)),
                ActionActiveFillColor = ToRenderColor(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetAccentColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.34f)),
                CardFillColor = ToRenderColor(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetSurfaceColor(), ImguiWorkbenchLayout.GetHeaderColor(), 0.52f)),
                TextColor = ToRenderColor(ImguiWorkbenchLayout.GetTextColor()),
                MutedTextColor = ToRenderColor(ImguiWorkbenchLayout.GetMutedTextColor()),
                AccentColor = ToRenderColor(ImguiWorkbenchLayout.GetAccentColor()),
                WarningColor = ToRenderColor(ImguiWorkbenchLayout.Blend(ImguiWorkbenchLayout.GetWarningColor(), ImguiWorkbenchLayout.GetTextColor(), 0.42f))
            };
        }

        private static RenderColor ToRenderColor(Color color)
        {
            return new RenderColor(color.r, color.g, color.b, color.a);
        }
    }
}
