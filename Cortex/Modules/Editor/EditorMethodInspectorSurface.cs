using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;
using Cortex.Services;
using UnityEngine;

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
        private readonly EditorMethodInspectorPresentationService _presentationService;
        private readonly EditorMethodPatchCreationService _patchCreationService = new EditorMethodPatchCreationService();
        private readonly EditorMethodInspectorPanelDocumentAdapter _panelDocumentAdapter = new EditorMethodInspectorPanelDocumentAdapter();

        private Vector2 _scroll = Vector2.zero;

        public EditorMethodInspectorSurface(IEditorContextService contextService)
        {
            _inspectorService = new EditorMethodInspectorService(contextService);
            _presentationService = new EditorMethodInspectorPresentationService(contextService);
        }

        public Rect Draw(
            CortexShellState state,
            DocumentSession session,
            string activeDocumentPath,
            Rect anchorRect,
            Vector2 surfaceSize,
            CortexNavigationService navigationService,
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            GUIStyle containerStyle,
            GUIStyle buttonStyle,
            GUIStyle headerStyle,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ILoadedModCatalog loadedModCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchInspectionService harmonyInspectionService,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchDisplayService harmonyDisplayService,
            HarmonyPatchGenerationService harmonyGenerationService,
            IPanelRenderer panelRenderer)
        {
            if (!_inspectorService.IsVisibleForDocument(state, activeDocumentPath))
            {
                return new Rect(0f, 0f, 0f, 0f);
            }

            var preparedView = _presentationService.Prepare(
                state,
                session,
                projectCatalog,
                loadedModCatalog,
                sourceLookupIndex,
                harmonyInspectionService,
                harmonyResolutionService,
                harmonyDisplayService,
                harmonyGenerationService);
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
                preparedView.Invocation,
                navigationService,
                documentService,
                projectCatalog,
                sourceLookupIndex,
                harmonyResolutionService,
                harmonyGenerationService);
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
            EditorCommandInvocation invocation,
            CortexNavigationService navigationService,
            IDocumentService documentService,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchGenerationService harmonyGenerationService)
        {
            if (string.IsNullOrEmpty(activatedId))
            {
                return;
            }

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

            string symbolKind;
            string metadataName;
            string containingTypeName;
            string containingAssemblyName;
            string documentationCommentId;
            if (EditorMethodInspectorNavigationActionCodec.TryParse(
                activatedId,
                out symbolKind,
                out metadataName,
                out containingTypeName,
                out containingAssemblyName,
                out documentationCommentId))
            {
                if (navigationService != null)
                {
                    navigationService.OpenLanguageSymbolTarget(
                        state,
                        metadataName,
                        symbolKind,
                        metadataName,
                        containingTypeName,
                        containingAssemblyName,
                        documentationCommentId,
                        string.Empty,
                        null,
                        "Opened relationship target.",
                        "Could not open relationship target.");
                }

                return;
            }

            if (invocation == null || invocation.Target == null)
            {
                return;
            }

            if (string.Equals(activatedId, "patch:create:prefix", StringComparison.Ordinal))
            {
                PreparePatch(state, invocation.Target, projectCatalog, sourceLookupIndex, harmonyResolutionService, harmonyGenerationService, HarmonyPatchGenerationKind.Prefix);
                return;
            }

            if (string.Equals(activatedId, "patch:create:postfix", StringComparison.Ordinal))
            {
                PreparePatch(state, invocation.Target, projectCatalog, sourceLookupIndex, harmonyResolutionService, harmonyGenerationService, HarmonyPatchGenerationKind.Postfix);
                return;
            }

            if (activatedId.StartsWith("patch:open:", StringComparison.Ordinal))
            {
                var indexText = activatedId.Substring("patch:open:".Length);
                int index;
                if (int.TryParse(indexText, out index))
                {
                    OpenInsertionTarget(state, documentService, harmonyGenerationService, index);
                }
            }
        }

        private void PreparePatch(
            CortexShellState state,
            EditorCommandTarget target,
            IProjectCatalog projectCatalog,
            ISourceLookupIndex sourceLookupIndex,
            HarmonyPatchResolutionService harmonyResolutionService,
            HarmonyPatchGenerationService harmonyGenerationService,
            HarmonyPatchGenerationKind kind)
        {
            string statusMessage;
            if (_patchCreationService.PreparePatch(
                state,
                target,
                projectCatalog,
                sourceLookupIndex,
                harmonyResolutionService,
                harmonyGenerationService,
                kind,
                out statusMessage))
            {
                state.StatusMessage = statusMessage;
            }
            else if (!string.IsNullOrEmpty(statusMessage))
            {
                state.StatusMessage = statusMessage;
            }
        }

        private void OpenInsertionTarget(CortexShellState state, IDocumentService documentService, HarmonyPatchGenerationService harmonyGenerationService, int index)
        {
            if (state == null || state.Harmony == null || state.Harmony.InsertionTargets == null || index < 0 || index >= state.Harmony.InsertionTargets.Count)
            {
                return;
            }

            var insertionTarget = state.Harmony.InsertionTargets[index];
            if (insertionTarget == null)
            {
                return;
            }

            string statusMessage;
            if (_patchCreationService.OpenInsertionTarget(state, documentService, harmonyGenerationService, insertionTarget, out statusMessage))
            {
                _inspectorService.Close(state);
            }

            if (!string.IsNullOrEmpty(statusMessage))
            {
                state.StatusMessage = statusMessage;
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
            var borderColor = CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetBorderColor(), 0.38f);
            return new PanelThemePalette
            {
                ThemeKey = "method-inspector",
                BackgroundColor = ToRenderColor(CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetBackgroundColor(), 0.22f)),
                HeaderColor = ToRenderColor(CortexIdeLayout.Blend(CortexIdeLayout.GetHeaderColor(), CortexIdeLayout.GetSurfaceColor(), 0.18f)),
                BorderColor = ToRenderColor(borderColor),
                DividerColor = ToRenderColor(CortexIdeLayout.WithAlpha(CortexIdeLayout.Blend(borderColor, CortexIdeLayout.GetTextColor(), 0.1f), 0.46f)),
                ActionFillColor = ToRenderColor(CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.72f)),
                ActionActiveFillColor = ToRenderColor(CortexIdeLayout.Blend(CortexIdeLayout.GetAccentColor(), CortexIdeLayout.GetHeaderColor(), 0.34f)),
                CardFillColor = ToRenderColor(CortexIdeLayout.Blend(CortexIdeLayout.GetSurfaceColor(), CortexIdeLayout.GetHeaderColor(), 0.52f)),
                TextColor = ToRenderColor(CortexIdeLayout.GetTextColor()),
                MutedTextColor = ToRenderColor(CortexIdeLayout.GetMutedTextColor()),
                AccentColor = ToRenderColor(CortexIdeLayout.GetAccentColor()),
                WarningColor = ToRenderColor(CortexIdeLayout.Blend(CortexIdeLayout.GetWarningColor(), CortexIdeLayout.GetTextColor(), 0.42f))
            };
        }

        private static RenderColor ToRenderColor(Color color)
        {
            return new RenderColor(color.r, color.g, color.b, color.a);
        }
    }
}
