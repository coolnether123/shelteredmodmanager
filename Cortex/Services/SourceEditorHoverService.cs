using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Rendering.Abstractions;
using Cortex.Rendering.Models;

namespace Cortex.Services
{
    internal sealed class EditorHoverTarget
    {
        public string SurfaceId = string.Empty;
        public string PaneId = string.Empty;
        public EditorSurfaceKind SurfaceKind;
        public string HoverKey = string.Empty;
        public string TokenClassification = string.Empty;
        public RenderRect AnchorRect = new RenderRect(0f, 0f, 0f, 0f);
        public EditorCommandTarget Target;
    }

    internal sealed class EditorHoverService
    {
        private const float HoverDelaySeconds = 0.5f;
        private const double StickyHoverGraceMs = 120d;
        private const double PendingHoverTargetLifetimeMs = 10000d;
        private const double SurfaceStateStaleMs = 600000d;
        private const int MaxTrackedSurfaceStates = 32;

        private readonly EditorCommandContextFactory _contextFactory = new EditorCommandContextFactory();
        private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();
        private readonly IEditorContextService _contextService;
        private readonly Dictionary<string, SurfaceHoverState> _surfaceStates = new Dictionary<string, SurfaceHoverState>(StringComparer.OrdinalIgnoreCase);

        public EditorHoverService(IEditorContextService contextService)
        {
            _contextService = contextService;
        }

        public bool TryCreateInteractionTarget(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            int absolutePosition,
            out EditorCommandTarget target)
        {
            target = null;
            EditorCommandInvocation invocation;
            if (!_contextFactory.TryCreateSourceInteractionInvocation(session, state, editingEnabled, absolutePosition, null, out invocation) ||
                invocation == null ||
                invocation.Target == null)
            {
                return false;
            }

            target = invocation.Target;
            ApplyInteractionCapabilities(target, ResolveHoverResponse(state, BuildHoverKey(session, target)));
            return true;
        }

        public bool TryCreateSourceHoverTarget(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            string surfaceId,
            string paneId,
            EditorSurfaceKind surfaceKind,
            int absolutePosition,
            RenderRect anchorRect,
            string tokenClassification,
            out EditorHoverTarget hoverTarget)
        {
            hoverTarget = null;
            EditorCommandTarget target;
            if (!TryCreateInteractionTarget(session, state, editingEnabled, absolutePosition, out target))
            {
                LogHoverTargetCreated(
                    false,
                    surfaceId,
                    surfaceKind,
                    session != null ? session.FilePath : string.Empty,
                    session != null ? session.TextVersion : 0,
                    absolutePosition,
                    string.Empty,
                    string.Empty,
                    "source-interaction-target-unavailable");
                return false;
            }

            var published = TryPublishHoverTarget(
                state,
                session,
                surfaceId,
                paneId,
                surfaceKind,
                target,
                anchorRect,
                tokenClassification,
                out hoverTarget);
            LogHoverTargetCreated(
                published,
                surfaceId,
                surfaceKind,
                session != null ? session.FilePath : string.Empty,
                session != null ? session.TextVersion : 0,
                absolutePosition,
                hoverTarget != null ? hoverTarget.HoverKey ?? string.Empty : BuildHoverKey(session, target),
                target != null ? target.SymbolText ?? string.Empty : string.Empty,
                published ? string.Empty : "source-target-publish-failed");
            return published;
        }

        public bool TryCreateReadOnlyHoverTarget(
            DocumentSession session,
            CortexShellState state,
            string surfaceId,
            string paneId,
            EditorSurfaceKind surfaceKind,
            int absolutePosition,
            int line,
            int column,
            string tokenText,
            bool canNavigateToDefinition,
            string tokenClassification,
            RenderRect anchorRect,
            out EditorHoverTarget hoverTarget)
        {
            hoverTarget = null;
            EditorCommandInvocation invocation;
            var hoverKey = BuildHoverKey(session != null ? session.FilePath : string.Empty, absolutePosition);
            var hoverResponse = ResolveHoverResponse(state, hoverKey);
            if (!_contextFactory.TryCreateTokenInvocation(
                session,
                state,
                absolutePosition,
                line,
                column,
                tokenText,
                hoverResponse,
                canNavigateToDefinition,
                out invocation) ||
                invocation == null ||
                invocation.Target == null)
            {
                LogHoverTargetCreated(
                    false,
                    surfaceId,
                    surfaceKind,
                    session != null ? session.FilePath : string.Empty,
                    session != null ? session.TextVersion : 0,
                    absolutePosition,
                    hoverKey,
                    tokenText,
                    "readonly-token-target-unavailable");
                return false;
            }

            var published = TryPublishHoverTarget(
                state,
                session,
                surfaceId,
                paneId,
                surfaceKind,
                invocation.Target,
                anchorRect,
                tokenClassification,
                out hoverTarget);
            LogHoverTargetCreated(
                published,
                surfaceId,
                surfaceKind,
                session != null ? session.FilePath : string.Empty,
                session != null ? session.TextVersion : 0,
                absolutePosition,
                hoverTarget != null ? hoverTarget.HoverKey ?? string.Empty : hoverKey,
                invocation.Target != null ? invocation.Target.SymbolText ?? string.Empty : tokenText,
                published ? string.Empty : "readonly-target-publish-failed");
            return published;
        }

        public void UpdateHoverRequest(
            CortexShellState state,
            string surfaceId,
            EditorHoverTarget hoverTarget,
            bool allowHover,
            bool hasMouse,
            RenderPoint pointerPosition)
        {
            var surfaceState = GetSurfaceState(surfaceId);
            if (!allowHover || state == null || state.Editor == null || hoverTarget == null || hoverTarget.Target == null || string.IsNullOrEmpty(hoverTarget.HoverKey))
            {
                ResetCandidate(surfaceState);
                return;
            }

            CapturePendingHoverTarget(surfaceState, hoverTarget);
            if (ShouldSuppressRetarget(surfaceState, hoverTarget.HoverKey, hasMouse, pointerPosition))
            {
                LogRetargetSuppressed(surfaceState, surfaceId, hoverTarget);
                ResetCandidate(surfaceState);
                return;
            }

            if (!string.Equals(surfaceState.HoverCandidateKey, hoverTarget.HoverKey, StringComparison.Ordinal))
            {
                surfaceState.HoverCandidateKey = hoverTarget.HoverKey;
                surfaceState.HoverCandidateUtc = DateTime.UtcNow;
                return;
            }

            if ((DateTime.UtcNow - surfaceState.HoverCandidateUtc).TotalSeconds < HoverDelaySeconds)
            {
                return;
            }

            var response = ResolveHoverResponse(state, hoverTarget);
            if (response != null && response.Success)
            {
                return;
            }

            if (string.Equals(state.Editor.Hover.RequestedKey, hoverTarget.HoverKey, StringComparison.Ordinal))
            {
                return;
            }

            QueueHoverRequest(state, surfaceId, hoverTarget);
        }

        public void RequestHoverNow(
            CortexShellState state,
            string surfaceId,
            EditorHoverTarget hoverTarget)
        {
            if (state == null ||
                state.Editor == null ||
                hoverTarget == null ||
                hoverTarget.Target == null ||
                string.IsNullOrEmpty(hoverTarget.HoverKey))
            {
                return;
            }

            var response = ResolveHoverResponse(state, hoverTarget);
            if (response != null && response.Success)
            {
                return;
            }

            if (string.Equals(state.Editor.Hover.RequestedKey, hoverTarget.HoverKey, StringComparison.Ordinal))
            {
                return;
            }

            var surfaceState = GetSurfaceState(surfaceId);
            surfaceState.HoverCandidateKey = hoverTarget.HoverKey;
            surfaceState.HoverCandidateUtc = DateTime.UtcNow;
            CapturePendingHoverTarget(surfaceState, hoverTarget);
            QueueHoverRequest(state, surfaceId, hoverTarget);
        }

        private void QueueHoverRequest(
            CortexShellState state,
            string surfaceId,
            EditorHoverTarget hoverTarget)
        {
            state.Editor.Hover.RequestedKey = hoverTarget.HoverKey;
            state.Editor.Hover.RequestedContextKey = hoverTarget.Target.ContextKey ?? string.Empty;
            state.Editor.Hover.RequestedDocumentPath = hoverTarget.Target.DocumentPath ?? string.Empty;
            state.Editor.Hover.RequestedLine = hoverTarget.Target.Line;
            state.Editor.Hover.RequestedColumn = hoverTarget.Target.Column;
            state.Editor.Hover.RequestedAbsolutePosition = hoverTarget.Target.AbsolutePosition;
            state.Editor.Hover.RequestedTokenText = hoverTarget.Target.SymbolText ?? string.Empty;
            LogHoverRequestQueued(true, surfaceId, hoverTarget, state, string.Empty);
        }

        public bool DrawHover(
            IHoverTooltipRenderer hoverTooltipRenderer,
            CortexNavigationService navigationService,
            CortexShellState state,
            string surfaceId,
            EditorHoverTarget hoverTarget,
            RenderPoint pointerPosition,
            RenderSize viewportSize,
            bool hasMouse,
            HoverTooltipThemePalette theme,
            float tooltipWidth,
            string telemetrySurfaceKind)
        {
            var surfaceState = GetSurfaceState(surfaceId);
            if (hoverTooltipRenderer == null || state == null || string.IsNullOrEmpty(surfaceId))
            {
                ClearSurfaceHover(state, surfaceId, hoverTooltipRenderer);
                return false;
            }

            EditorHoverTarget visibleTarget;
            EditorResolvedHoverContent hoverContent;
            LanguageServiceHoverResponse hoverResponse;
            bool usedStickyHover;
            string stickyReason;
            string resolutionDetail;
            if (!TryResolveVisibleHover(state, surfaceState, hoverTarget, pointerPosition, viewportSize, hasMouse, out visibleTarget, out hoverContent, out hoverResponse, out usedStickyHover, out stickyReason, out resolutionDetail))
            {
                LogHoverDiagnostic("draw-no-visible-hover", hoverTarget, surfaceState, resolutionDetail);
                LogHoverDrawResolved(false, surfaceId, telemetrySurfaceKind, hoverTarget, surfaceState, state, null, "no-visible-hover");
                ClearVisibleHover(state, surfaceId, hoverTooltipRenderer, "no-visible-hover");
                return false;
            }

            var model = BuildRenderModel(visibleTarget, hoverContent);
            HoverTooltipRenderResult tooltipResult;
            if (!hoverTooltipRenderer.DrawRichTooltip(model, pointerPosition, viewportSize, hasMouse, theme, tooltipWidth, out tooltipResult))
            {
                var hiddenReason = !string.IsNullOrEmpty(tooltipResult.HiddenReason)
                    ? tooltipResult.HiddenReason
                    : "renderer-hidden";
                LogHoverDiagnostic("renderer-hidden", visibleTarget, surfaceState, hiddenReason);
                LogHoverDrawResolved(false, surfaceId, telemetrySurfaceKind, visibleTarget, surfaceState, state, hoverResponse, hiddenReason);
                return false;
            }

            surfaceState.VisibleHoverKey = model.Key ?? string.Empty;
            surfaceState.VisibleContextKey = model.ContextKey ?? string.Empty;
            surfaceState.VisibleDocumentPath = model.DocumentPath ?? string.Empty;
            surfaceState.VisibleAnchorRect = model.AnchorRect;
            surfaceState.VisibleTooltipRect = tooltipResult.TooltipRect;
            surfaceState.PendingHoverTarget = null;
            surfaceState.PendingHoverUtc = DateTime.MinValue;

            if ((hoverTarget != null && string.Equals(surfaceState.VisibleHoverKey, hoverTarget.HoverKey, StringComparison.Ordinal)) ||
                (hasMouse && IsPointerWithinHoverSurface(surfaceState, pointerPosition)))
            {
                RefreshStickyHoverKeepAlive(surfaceState);
            }

            PublishVisibleHover(state, visibleTarget, tooltipResult.HoveredPart);
            LogHoverDrawResolved(true, surfaceId, telemetrySurfaceKind, visibleTarget, surfaceState, state, hoverResponse, usedStickyHover ? stickyReason : "direct");
            LogVisibleHover(surfaceState, surfaceId, telemetrySurfaceKind, visibleTarget, hoverResponse, hoverContent, usedStickyHover, stickyReason);
            ClearVisualRefreshRequest(state, visibleTarget != null ? visibleTarget.HoverKey ?? string.Empty : string.Empty);
            PreloadHoverTarget(navigationService, state, tooltipResult.HoveredPart, hoverContent);
            OpenActivatedHoverTarget(navigationService, state, visibleTarget, model.Key, tooltipResult.ActivatedPart);
            return tooltipResult.Visible;
        }

        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey)
        {
            return _contextService != null ? _contextService.ResolveHoverResponse(state, hoverKey) : null;
        }

        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, DocumentSession session, EditorCommandTarget target)
        {
            if (target == null)
            {
                return null;
            }

            return ResolveHoverResponse(state, BuildHoverKey(session, target));
        }

        public bool IsPointerWithinHoverSurface(string surfaceId, RenderPoint pointerPosition)
        {
            return IsPointerWithinHoverSurface(GetSurfaceState(surfaceId), pointerPosition);
        }

        public void ClearSurfaceHover(CortexShellState state, string surfaceId, IHoverTooltipRenderer hoverTooltipRenderer, string reason = "")
        {
            if (!string.IsNullOrEmpty(surfaceId))
            {
                SurfaceHoverState surfaceState;
                if (_surfaceStates.TryGetValue(surfaceId, out surfaceState) && surfaceState != null)
                {
                    ResetVisibleHover(state, surfaceState, surfaceId, reason);
                    surfaceState.HoverCandidateKey = string.Empty;
                    surfaceState.HoverCandidateUtc = DateTime.MinValue;
                    surfaceState.PendingHoverTarget = null;
                    surfaceState.PendingHoverUtc = DateTime.MinValue;
                    surfaceState.LastRetargetSuppressionLogKey = string.Empty;
                }
            }

            if (hoverTooltipRenderer != null)
            {
                hoverTooltipRenderer.ClearRichState();
            }
        }

        private void ClearVisibleHover(CortexShellState state, string surfaceId, IHoverTooltipRenderer hoverTooltipRenderer, string reason)
        {
            if (!string.IsNullOrEmpty(surfaceId))
            {
                SurfaceHoverState surfaceState;
                if (_surfaceStates.TryGetValue(surfaceId, out surfaceState) && surfaceState != null)
                {
                    ResetVisibleHover(state, surfaceState, surfaceId, reason);
                }
            }

            if (hoverTooltipRenderer != null)
            {
                hoverTooltipRenderer.ClearRichState();
            }
        }

        private void ResetVisibleHover(CortexShellState state, SurfaceHoverState surfaceState, string surfaceId, string reason)
        {
            if (surfaceState == null)
            {
                return;
            }

            LogHoverCleared(surfaceState, surfaceId, reason);
            if (state != null &&
                state.Editor != null &&
                string.Equals(state.Editor.Hover.ActiveContextKey ?? string.Empty, surfaceState.VisibleContextKey ?? string.Empty, StringComparison.Ordinal))
            {
                state.Editor.Hover.ActiveContextKey = string.Empty;
            }

            ClearVisualRefreshRequest(state, surfaceState.VisibleHoverKey ?? string.Empty);

            if (state != null &&
                state.EditorContext != null &&
                string.Equals(state.EditorContext.HoveredContextKey ?? string.Empty, surfaceState.VisibleContextKey ?? string.Empty, StringComparison.Ordinal))
            {
                _contextService.ClearHoveredContext(state);
            }

            surfaceState.VisibleHoverKey = string.Empty;
            surfaceState.VisibleContextKey = string.Empty;
            surfaceState.VisibleDocumentPath = string.Empty;
            surfaceState.VisibleAnchorRect = new RenderRect(0f, 0f, 0f, 0f);
            surfaceState.VisibleTooltipRect = new RenderRect(0f, 0f, 0f, 0f);
            surfaceState.KeepAliveUtc = DateTime.MinValue;
        }

        private bool TryPublishHoverTarget(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            string paneId,
            EditorSurfaceKind surfaceKind,
            EditorCommandTarget target,
            RenderRect anchorRect,
            string tokenClassification,
            out EditorHoverTarget hoverTarget)
        {
            hoverTarget = null;
            if (_contextService == null || state == null || target == null)
            {
                return false;
            }

            var snapshot = _contextService.PublishTargetContext(
                state,
                session,
                surfaceId,
                paneId,
                surfaceKind,
                target,
                false);
            if (snapshot == null || snapshot.Target == null)
            {
                return false;
            }

            hoverTarget = new EditorHoverTarget
            {
                SurfaceId = surfaceId ?? string.Empty,
                PaneId = paneId ?? string.Empty,
                SurfaceKind = surfaceKind,
                HoverKey = snapshot.HoverKey ?? BuildHoverKey(session, snapshot.Target),
                TokenClassification = tokenClassification ?? string.Empty,
                AnchorRect = anchorRect,
                Target = snapshot.Target
            };
            return true;
        }

        private LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, EditorHoverTarget hoverTarget)
        {
            if (hoverTarget == null)
            {
                return null;
            }

            if (_contextService != null && hoverTarget.Target != null)
            {
                var response = _contextService.ResolveHoverResponse(state, hoverTarget.Target.ContextKey ?? string.Empty, hoverTarget.HoverKey);
                if (response != null)
                {
                    return response;
                }
            }

            return ResolveHoverResponse(state, hoverTarget.HoverKey);
        }

        private EditorResolvedHoverContent ResolveHoverContent(
            CortexShellState state,
            EditorHoverTarget hoverTarget,
            LanguageServiceHoverResponse hoverResponse)
        {
            if (hoverTarget == null || hoverTarget.Target == null || _contextService == null || hoverResponse == null || !hoverResponse.Success)
            {
                return null;
            }

            var hoverContent = _contextService.ResolveHoverContent(state, hoverTarget.Target.ContextKey ?? string.Empty, hoverTarget.HoverKey);
            if (hoverContent != null)
            {
                return hoverContent;
            }

            EnsureHoverResponseOnContext(state, hoverTarget.Target.ContextKey ?? string.Empty, hoverTarget.HoverKey, hoverResponse);
            hoverContent = BuildHoverContent(hoverTarget, hoverResponse);
            _contextService.ApplyHoverContent(state, hoverTarget.Target.ContextKey ?? string.Empty, hoverTarget.HoverKey, hoverContent);
            return hoverContent;
        }

        private EditorResolvedHoverContent ResolveStickyHoverContent(
            CortexShellState state,
            SurfaceHoverState surfaceState,
            out EditorHoverTarget stickyTarget,
            out LanguageServiceHoverResponse hoverResponse)
        {
            stickyTarget = null;
            hoverResponse = null;
            if (surfaceState == null ||
                string.IsNullOrEmpty(surfaceState.VisibleContextKey) ||
                string.IsNullOrEmpty(surfaceState.VisibleHoverKey) ||
                _contextService == null)
            {
                return null;
            }

            var snapshot = _contextService.GetContext(state, surfaceState.VisibleContextKey);
            if (snapshot == null || snapshot.Target == null)
            {
                return null;
            }

            hoverResponse = _contextService.ResolveHoverResponse(state, surfaceState.VisibleContextKey, surfaceState.VisibleHoverKey);
            if (hoverResponse == null || !hoverResponse.Success)
            {
                hoverResponse = _contextService.ResolveHoverResponse(state, surfaceState.VisibleHoverKey);
                EnsureHoverResponseOnContext(state, surfaceState.VisibleContextKey, surfaceState.VisibleHoverKey, hoverResponse);
            }

            if (hoverResponse == null || !hoverResponse.Success)
            {
                return null;
            }

            stickyTarget = new EditorHoverTarget
            {
                SurfaceId = snapshot.SurfaceId ?? string.Empty,
                PaneId = snapshot.PaneId ?? string.Empty,
                SurfaceKind = snapshot.SurfaceKind,
                HoverKey = surfaceState.VisibleHoverKey ?? string.Empty,
                AnchorRect = surfaceState.VisibleAnchorRect,
                Target = snapshot.Target
            };

            var hoverContent = _contextService.ResolveHoverContent(state, surfaceState.VisibleContextKey, surfaceState.VisibleHoverKey);
            if (hoverContent == null)
            {
                hoverContent = BuildHoverContent(stickyTarget, hoverResponse);
                _contextService.ApplyHoverContent(state, surfaceState.VisibleContextKey, surfaceState.VisibleHoverKey, hoverContent);
            }

            return hoverContent;
        }

        private bool TryResolveVisibleHover(
            CortexShellState state,
            SurfaceHoverState surfaceState,
            EditorHoverTarget hoverTarget,
            RenderPoint pointerPosition,
            RenderSize viewportSize,
            bool hasMouse,
            out EditorHoverTarget visibleTarget,
            out EditorResolvedHoverContent hoverContent,
            out LanguageServiceHoverResponse hoverResponse,
            out bool usedStickyHover,
            out string stickyReason,
            out string resolutionDetail)
        {
            visibleTarget = null;
            hoverContent = null;
            hoverResponse = null;
            usedStickyHover = false;
            stickyReason = string.Empty;
            resolutionDetail = string.Empty;

            var resolvedHoverTarget = hoverTarget ?? GetPendingHoverTarget(surfaceState);
            if (resolvedHoverTarget != null)
            {
                hoverResponse = ResolveHoverResponse(state, resolvedHoverTarget);
                if (hoverResponse == null)
                {
                    resolutionDetail = hoverTarget != null ? "target-no-response" : "pending-target-no-response";
                }
                else if (!hoverResponse.Success)
                {
                    resolutionDetail = hoverTarget != null ? "target-response-failed" : "pending-target-response-failed";
                }

                hoverContent = ResolveHoverContent(state, resolvedHoverTarget, hoverResponse);
                if (hoverResponse != null && hoverResponse.Success && hoverContent != null)
                {
                    visibleTarget = resolvedHoverTarget;
                    return true;
                }

                if (hoverResponse != null && hoverResponse.Success && hoverContent == null)
                {
                    resolutionDetail = hoverTarget != null ? "target-no-content" : "pending-target-no-content";
                }
            }

            stickyReason = ResolveStickyVisibilityReason(surfaceState, hoverTarget, pointerPosition, hasMouse, viewportSize);
            if (string.IsNullOrEmpty(stickyReason))
            {
                if (string.IsNullOrEmpty(resolutionDetail))
                {
                    resolutionDetail = "sticky-not-eligible";
                }
                return false;
            }

            hoverContent = ResolveStickyHoverContent(state, surfaceState, out visibleTarget, out hoverResponse);
            usedStickyHover = visibleTarget != null && hoverContent != null && hoverResponse != null && hoverResponse.Success;
            if (!usedStickyHover)
            {
                resolutionDetail = "sticky-unresolved|" + stickyReason;
            }
            return usedStickyHover;
        }

        private EditorResolvedHoverContent BuildHoverContent(EditorHoverTarget hoverTarget, LanguageServiceHoverResponse response)
        {
            var target = hoverTarget != null ? hoverTarget.Target : null;
            var tokenClassification = hoverTarget != null ? hoverTarget.TokenClassification ?? string.Empty : string.Empty;
            var overloadSummary = BuildOverloadSummary(response != null ? response.SupplementalSections : null);
            return new EditorResolvedHoverContent
            {
                Key = hoverTarget != null ? hoverTarget.HoverKey ?? string.Empty : string.Empty,
                ContextKey = target != null ? target.ContextKey ?? string.Empty : string.Empty,
                DocumentPath = target != null ? target.DocumentPath ?? string.Empty : string.Empty,
                DocumentVersion = response != null ? response.DocumentVersion : 0,
                QualifiedPath = response != null ? response.QualifiedSymbolDisplay ?? string.Empty : string.Empty,
                SymbolDisplay = response != null && !string.IsNullOrEmpty(response.SymbolDisplay)
                    ? response.SymbolDisplay ?? string.Empty
                    : (target != null ? target.SymbolText ?? string.Empty : string.Empty),
                SummaryText = BuildMetaText(response, null, tokenClassification),
                DocumentationText = response != null ? response.DocumentationText ?? string.Empty : string.Empty,
                SignatureParts = BuildSignatureParts(response, tokenClassification, overloadSummary),
                SupplementalSections = BuildSections(response != null ? response.SupplementalSections : null, tokenClassification),
                OverloadSummary = overloadSummary,
                OverloadCount = 0,
                OverloadIndex = -1,
                PrimaryNavigationTarget = BuildNavigationTarget(response)
            };
        }

        private EditorHoverContentPart[] BuildSignatureParts(LanguageServiceHoverResponse response, string tokenClassification, string overloadSummary)
        {
            if (response == null || response.DisplayParts == null || response.DisplayParts.Length == 0)
            {
                var fallbackParts = new List<EditorHoverContentPart>
                {
                    new EditorHoverContentPart
                    {
                        Text = response != null ? response.SymbolDisplay ?? string.Empty : string.Empty,
                        Classification = response != null ? response.SymbolKind ?? string.Empty : string.Empty,
                        IsInteractive = false,
                        SummaryText = BuildMetaText(response, null, tokenClassification),
                        DocumentationText = response != null ? response.DocumentationText ?? string.Empty : string.Empty,
                        SupplementalSections = BuildSections(response != null ? response.SupplementalSections : null, tokenClassification)
                    }
                };

                AppendOverloadSummaryPart(fallbackParts, overloadSummary);
                return fallbackParts.ToArray();
            }

            var parts = new List<EditorHoverContentPart>(response.DisplayParts.Length + 1);
            for (var i = 0; i < response.DisplayParts.Length; i++)
            {
                var displayPart = response.DisplayParts[i];
                parts.Add(new EditorHoverContentPart
                {
                    Text = displayPart != null ? displayPart.Text ?? string.Empty : string.Empty,
                    Classification = displayPart != null ? displayPart.Classification ?? string.Empty : string.Empty,
                    IsInteractive = displayPart != null && displayPart.IsInteractive && BuildNavigationTarget(displayPart) != null,
                    SummaryText = BuildMetaText(response, displayPart, tokenClassification),
                    DocumentationText = displayPart != null && !string.IsNullOrEmpty(displayPart.DocumentationText)
                        ? displayPart.DocumentationText ?? string.Empty
                        : (response != null ? response.DocumentationText ?? string.Empty : string.Empty),
                    SupplementalSections = BuildSections(displayPart != null ? displayPart.SupplementalSections : null, tokenClassification),
                    NavigationTarget = BuildNavigationTarget(displayPart)
                });
            }

            AppendOverloadSummaryPart(parts, overloadSummary);
            return parts.ToArray();
        }

        private static void AppendOverloadSummaryPart(List<EditorHoverContentPart> parts, string overloadSummary)
        {
            if (parts == null || string.IsNullOrEmpty(overloadSummary))
            {
                return;
            }

            parts.Add(new EditorHoverContentPart
            {
                Text = " " + overloadSummary,
                Classification = "overload",
                IsInteractive = false
            });
        }

        private EditorHoverSection[] BuildSections(LanguageServiceHoverSection[] sections, string tokenClassification)
        {
            if (sections == null || sections.Length == 0)
            {
                return new EditorHoverSection[0];
            }

            var results = new List<EditorHoverSection>();
            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var displayParts = section.DisplayParts;
                var mappedDisplayParts = new EditorHoverContentPart[displayParts != null ? displayParts.Length : 0];
                for (var partIndex = 0; displayParts != null && partIndex < displayParts.Length; partIndex++)
                {
                    var displayPart = displayParts[partIndex];
                    mappedDisplayParts[partIndex] = new EditorHoverContentPart
                    {
                        Text = displayPart != null ? displayPart.Text ?? string.Empty : string.Empty,
                        Classification = displayPart != null ? displayPart.Classification ?? string.Empty : string.Empty,
                        IsInteractive = displayPart != null && displayPart.IsInteractive && BuildNavigationTarget(displayPart) != null,
                        SummaryText = BuildMetaText(null, displayPart, tokenClassification),
                        DocumentationText = displayPart != null ? displayPart.DocumentationText ?? string.Empty : string.Empty,
                        SupplementalSections = BuildSections(displayPart != null ? displayPart.SupplementalSections : null, tokenClassification),
                        NavigationTarget = BuildNavigationTarget(displayPart)
                    };
                }

                var text = !string.IsNullOrEmpty(section.Text)
                    ? section.Text ?? string.Empty
                    : FlattenDisplayParts(section.DisplayParts);
                if (string.IsNullOrEmpty(text) && mappedDisplayParts.Length == 0)
                {
                    continue;
                }

                results.Add(new EditorHoverSection
                {
                    Title = section.Title ?? section.Kind ?? string.Empty,
                    Text = text,
                    DisplayParts = mappedDisplayParts
                });
            }

            return results.ToArray();
        }

        private void PublishVisibleHover(CortexShellState state, EditorHoverTarget hoverTarget, EditorHoverContentPart hoveredPart)
        {
            if (state == null || hoverTarget == null || hoverTarget.Target == null || _contextService == null)
            {
                return;
            }

            var navigationTarget = hoveredPart != null && hoveredPart.IsInteractive && hoveredPart.NavigationTarget != null
                ? hoveredPart.NavigationTarget
                : null;
            _contextService.PublishHoveredContext(
                state,
                hoverTarget.Target.ContextKey ?? string.Empty,
                navigationTarget != null ? navigationTarget.DefinitionDocumentPath ?? string.Empty : hoverTarget.Target.DefinitionDocumentPath ?? string.Empty);
        }

        private static void PreloadHoverTarget(
            CortexNavigationService navigationService,
            CortexShellState state,
            EditorHoverContentPart hoveredPart,
            EditorResolvedHoverContent hoverContent)
        {
            if (navigationService == null || state == null)
            {
                return;
            }

            var navigationTarget = hoveredPart != null && hoveredPart.IsInteractive
                ? hoveredPart.NavigationTarget
                : (hoverContent != null ? hoverContent.PrimaryNavigationTarget : null);
            navigationService.PreloadHoverNavigationTarget(state, navigationTarget);
        }

        private static void OpenActivatedHoverTarget(
            CortexNavigationService navigationService,
            CortexShellState state,
            EditorHoverTarget hoverTarget,
            string hoverKey,
            EditorHoverContentPart activatedPart)
        {
            if (navigationService == null || state == null || activatedPart == null || !activatedPart.IsInteractive)
            {
                return;
            }

            var navigationTarget = activatedPart.NavigationTarget;
            if (navigationTarget == null)
            {
                return;
            }

            LogActivatedPart(hoverTarget, hoverKey, activatedPart, navigationTarget);
            navigationService.OpenHoverNavigationTarget(
                state,
                navigationTarget,
                "Opened definition: " + (navigationTarget.Label ?? string.Empty),
                "Unable to open definition for " + (navigationTarget.Label ?? string.Empty) + ".");
        }

        private static void LogVisibleHover(
            SurfaceHoverState surfaceState,
            string surfaceId,
            string telemetrySurfaceKind,
            EditorHoverTarget hoverTarget,
            LanguageServiceHoverResponse hoverResponse,
            EditorResolvedHoverContent hoverContent,
            bool usedStickyHover,
            string stickyReason)
        {
            var hoverKey = hoverTarget != null ? hoverTarget.HoverKey ?? string.Empty : string.Empty;
            if (surfaceState == null ||
                string.IsNullOrEmpty(hoverKey) ||
                string.Equals(surfaceState.LastVisibleHoverLogKey, hoverKey, StringComparison.Ordinal))
            {
                return;
            }

            surfaceState.LastVisibleHoverLogKey = hoverKey;
            CortexDeveloperLog.WriteSymbolTooltipVisible(
                telemetrySurfaceKind ?? string.Empty,
                hoverKey,
                hoverTarget != null && hoverTarget.Target != null ? hoverTarget.Target.SymbolText ?? string.Empty : string.Empty,
                hoverResponse);
            MMLog.WriteInfo(
                "[Cortex.Hover.Debug] Visible hover. SurfaceId='" + (surfaceId ?? string.Empty) +
                "', SurfaceKind='" + (telemetrySurfaceKind ?? string.Empty) +
                "', HoverKey='" + hoverKey +
                "', ContextKey='" + (hoverTarget != null && hoverTarget.Target != null ? hoverTarget.Target.ContextKey ?? string.Empty : string.Empty) +
                "', Symbol='" + (hoverTarget != null && hoverTarget.Target != null ? hoverTarget.Target.SymbolText ?? string.Empty : string.Empty) +
                "', QualifiedPath='" + (hoverContent != null ? hoverContent.QualifiedPath ?? string.Empty : string.Empty) +
                "', SignatureParts=" + CountParts(hoverContent != null ? hoverContent.SignatureParts : null) +
                ", InteractiveParts=" + CountInteractiveParts(hoverContent) +
                ", Sections=" + CountSections(hoverContent) +
                ", Sticky=" + usedStickyHover +
                ", StickyReason='" + (stickyReason ?? string.Empty) + "'.");
        }

        private static void LogHoverRequestQueued(bool success, string surfaceId, EditorHoverTarget hoverTarget, CortexShellState state, string detail)
        {
            var contextKey = hoverTarget != null && hoverTarget.Target != null
                ? hoverTarget.Target.ContextKey ?? string.Empty
                : state != null && state.Editor != null ? state.Editor.Hover.RequestedContextKey ?? string.Empty : string.Empty;
            var hoverKey = hoverTarget != null
                ? hoverTarget.HoverKey ?? string.Empty
                : state != null && state.Editor != null ? state.Editor.Hover.RequestedKey ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(hoverKey))
            {
                return;
            }

            var documentVersion = 0;
            if (state != null &&
                state.EditorContext != null &&
                !string.IsNullOrEmpty(contextKey))
            {
                EditorContextSnapshot snapshot;
                if (state.EditorContext.ContextsByKey.TryGetValue(contextKey, out snapshot) && snapshot != null)
                {
                    documentVersion = snapshot.DocumentVersion;
                }
            }

            CortexDeveloperLog.WriteHoverPipelineStage(
                "RequestQueued",
                success,
                surfaceId,
                hoverTarget != null ? hoverTarget.SurfaceKind.ToString() : string.Empty,
                hoverKey,
                contextKey,
                hoverTarget != null && hoverTarget.Target != null ? hoverTarget.Target.DocumentPath ?? string.Empty : state != null && state.Editor != null ? state.Editor.Hover.RequestedDocumentPath ?? string.Empty : string.Empty,
                documentVersion,
                hoverTarget != null && hoverTarget.Target != null ? hoverTarget.Target.AbsolutePosition : state != null && state.Editor != null ? state.Editor.Hover.RequestedAbsolutePosition : -1,
                hoverTarget != null && hoverTarget.Target != null ? hoverTarget.Target.SymbolText ?? string.Empty : state != null && state.Editor != null ? state.Editor.Hover.RequestedTokenText ?? string.Empty : string.Empty,
                detail);
        }

        private static void LogHoverTargetCreated(
            bool success,
            string surfaceId,
            EditorSurfaceKind surfaceKind,
            string documentPath,
            int documentVersion,
            int absolutePosition,
            string hoverKey,
            string tokenText,
            string detail)
        {
            CortexDeveloperLog.WriteHoverPipelineStage(
                "TargetCreated",
                success,
                surfaceId,
                surfaceKind.ToString(),
                hoverKey ?? string.Empty,
                string.Empty,
                documentPath ?? string.Empty,
                documentVersion,
                absolutePosition,
                tokenText ?? string.Empty,
                detail ?? string.Empty);
        }

        private static void LogHoverDrawResolved(
            bool success,
            string surfaceId,
            string surfaceKind,
            EditorHoverTarget hoverTarget,
            SurfaceHoverState surfaceState,
            CortexShellState state,
            LanguageServiceHoverResponse hoverResponse,
            string detail)
        {
            var hoverKey = hoverTarget != null
                ? hoverTarget.HoverKey ?? string.Empty
                : surfaceState != null ? surfaceState.VisibleHoverKey ?? string.Empty : string.Empty;
            var contextKey = hoverTarget != null && hoverTarget.Target != null
                ? hoverTarget.Target.ContextKey ?? string.Empty
                : surfaceState != null ? surfaceState.VisibleContextKey ?? string.Empty : string.Empty;
            var documentPath = hoverTarget != null && hoverTarget.Target != null
                ? hoverTarget.Target.DocumentPath ?? string.Empty
                : surfaceState != null ? surfaceState.VisibleDocumentPath ?? string.Empty : string.Empty;
            var tokenText = hoverTarget != null && hoverTarget.Target != null
                ? hoverTarget.Target.SymbolText ?? string.Empty
                : state != null && state.Editor != null ? state.Editor.Hover.RequestedTokenText ?? string.Empty : string.Empty;
            if (!success && string.Equals(detail, "no-visible-hover", StringComparison.Ordinal))
            {
                return;
            }
            if (!success &&
                string.IsNullOrEmpty(hoverKey) &&
                state != null &&
                state.Editor != null)
            {
                hoverKey = state.Editor.Hover.RequestedKey ?? string.Empty;
                contextKey = state.Editor.Hover.RequestedContextKey ?? contextKey;
                documentPath = state.Editor.Hover.RequestedDocumentPath ?? documentPath;
                tokenText = state.Editor.Hover.RequestedTokenText ?? tokenText;
            }

            CortexDeveloperLog.WriteHoverPipelineStage(
                "DrawResolved",
                success,
                surfaceId,
                surfaceKind ?? string.Empty,
                hoverKey,
                contextKey,
                documentPath,
                hoverResponse != null ? hoverResponse.DocumentVersion : 0,
                hoverTarget != null && hoverTarget.Target != null ? hoverTarget.Target.AbsolutePosition : state != null && state.Editor != null ? state.Editor.Hover.RequestedAbsolutePosition : -1,
                tokenText,
                detail ?? string.Empty);
        }

        private static void LogRetargetSuppressed(SurfaceHoverState surfaceState, string surfaceId, EditorHoverTarget hoverTarget)
        {
            var hoverKey = hoverTarget != null ? hoverTarget.HoverKey ?? string.Empty : string.Empty;
            if (surfaceState == null ||
                string.IsNullOrEmpty(surfaceState.VisibleHoverKey) ||
                string.Equals(surfaceState.LastRetargetSuppressionLogKey, hoverKey, StringComparison.Ordinal))
            {
                return;
            }

            surfaceState.LastRetargetSuppressionLogKey = hoverKey;
            CortexDeveloperLog.WriteHoverPipelineStage(
                "RetargetSuppressed",
                true,
                surfaceId,
                hoverTarget != null ? hoverTarget.SurfaceKind.ToString() : string.Empty,
                hoverKey,
                string.Empty,
                string.Empty,
                0,
                hoverTarget != null && hoverTarget.Target != null ? hoverTarget.Target.AbsolutePosition : -1,
                hoverTarget != null && hoverTarget.Target != null ? hoverTarget.Target.SymbolText ?? string.Empty : string.Empty,
                "sticky-hover");
        }

        private static void LogActivatedPart(EditorHoverTarget hoverTarget, string hoverKey, EditorHoverContentPart activatedPart, EditorHoverNavigationTarget navigationTarget)
        {
            if (activatedPart == null || navigationTarget == null)
            {
                return;
            }

            MMLog.WriteInfo(
                "[Cortex.Hover.Debug] Activated hover navigation. SurfaceId='" + (hoverTarget != null ? hoverTarget.SurfaceId ?? string.Empty : string.Empty) +
                "', HoverKey='" + (hoverKey ?? string.Empty) +
                "', PartText='" + (activatedPart.Text ?? string.Empty) +
                "', Target='" + (navigationTarget.Label ?? string.Empty) +
                "', DefinitionPath='" + (navigationTarget.DefinitionDocumentPath ?? string.Empty) + "'.");
        }

        private static void LogHoverCleared(SurfaceHoverState surfaceState, string surfaceId, string reason)
        {
            if (surfaceState == null ||
                string.IsNullOrEmpty(surfaceState.VisibleHoverKey))
            {
                return;
            }

            var clearKey = (surfaceState.VisibleHoverKey ?? string.Empty) + "|" + (reason ?? string.Empty);
            if (string.Equals(surfaceState.LastClearLogKey, clearKey, StringComparison.Ordinal))
            {
                return;
            }

            surfaceState.LastClearLogKey = clearKey;
            MMLog.WriteInfo(
                "[Cortex.Hover.Debug] Cleared hover. SurfaceId='" + (surfaceId ?? string.Empty) +
                "', HoverKey='" + (surfaceState.VisibleHoverKey ?? string.Empty) +
                "', ContextKey='" + (surfaceState.VisibleContextKey ?? string.Empty) +
                "', Reason='" + (reason ?? string.Empty) + "'.");
        }

        private static string FlattenDisplayParts(LanguageServiceHoverDisplayPart[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            var text = string.Empty;
            for (var i = 0; i < parts.Length; i++)
            {
                text += parts[i] != null ? parts[i].Text ?? string.Empty : string.Empty;
            }

            return text.Trim();
        }

        private static string BuildMetaText(LanguageServiceHoverResponse response, LanguageServiceHoverDisplayPart hoveredPart, string tokenClassification)
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

            return lines.Count > 0 ? string.Join(Environment.NewLine, lines.ToArray()) : string.Empty;
        }

        private static string BuildOverloadSummary(LanguageServiceHoverSection[] sections)
        {
            if (sections == null || sections.Length == 0)
            {
                return string.Empty;
            }

            for (var i = 0; i < sections.Length; i++)
            {
                var section = sections[i];
                if (section == null)
                {
                    continue;
                }

                var title = section.Title ?? section.Kind ?? string.Empty;
                if (title.IndexOf("overload", StringComparison.OrdinalIgnoreCase) >= 0 && !string.IsNullOrEmpty(section.Text))
                {
                    return section.Text ?? string.Empty;
                }
            }

            return string.Empty;
        }

        private static EditorHoverNavigationTarget BuildNavigationTarget(LanguageServiceHoverResponse response)
        {
            if (response == null)
            {
                return null;
            }

            return CreateNavigationTarget(
                response.SymbolDisplay,
                response.SymbolKind,
                response.MetadataName,
                response.ContainingTypeName,
                response.ContainingAssemblyName,
                response.DocumentationCommentId,
                response.DefinitionDocumentPath,
                response.DefinitionRange);
        }

        private static EditorHoverNavigationTarget BuildNavigationTarget(LanguageServiceHoverDisplayPart displayPart)
        {
            if (displayPart == null || !displayPart.IsInteractive)
            {
                return null;
            }

            return CreateNavigationTarget(
                displayPart.SymbolDisplay,
                displayPart.SymbolKind,
                displayPart.MetadataName,
                displayPart.ContainingTypeName,
                displayPart.ContainingAssemblyName,
                displayPart.DocumentationCommentId,
                displayPart.DefinitionDocumentPath,
                displayPart.DefinitionRange);
        }

        private static EditorHoverNavigationTarget CreateNavigationTarget(
            string symbolDisplay,
            string symbolKind,
            string metadataName,
            string containingTypeName,
            string containingAssemblyName,
            string documentationCommentId,
            string definitionDocumentPath,
            LanguageServiceRange definitionRange)
        {
            if (string.IsNullOrEmpty(definitionDocumentPath) &&
                string.IsNullOrEmpty(containingAssemblyName) &&
                string.IsNullOrEmpty(documentationCommentId) &&
                string.IsNullOrEmpty(metadataName))
            {
                return null;
            }

            return new EditorHoverNavigationTarget
            {
                Kind = EditorHoverNavigationKind.Symbol,
                Label = !string.IsNullOrEmpty(symbolDisplay) ? symbolDisplay ?? string.Empty : metadataName ?? string.Empty,
                SymbolDisplay = symbolDisplay ?? string.Empty,
                SymbolKind = symbolKind ?? string.Empty,
                MetadataName = metadataName ?? string.Empty,
                ContainingTypeName = containingTypeName ?? string.Empty,
                ContainingAssemblyName = containingAssemblyName ?? string.Empty,
                DocumentationCommentId = documentationCommentId ?? string.Empty,
                DefinitionDocumentPath = definitionDocumentPath ?? string.Empty,
                DefinitionRange = definitionRange
            };
        }

        private static HoverTooltipRenderModel BuildRenderModel(EditorHoverTarget hoverTarget, EditorResolvedHoverContent hoverContent)
        {
            return new HoverTooltipRenderModel
            {
                Key = hoverContent != null ? hoverContent.Key ?? string.Empty : string.Empty,
                ContextKey = hoverContent != null ? hoverContent.ContextKey ?? string.Empty : string.Empty,
                DocumentPath = hoverContent != null ? hoverContent.DocumentPath ?? string.Empty : string.Empty,
                DocumentVersion = hoverContent != null ? hoverContent.DocumentVersion : 0,
                AnchorRect = hoverTarget != null ? hoverTarget.AnchorRect : new RenderRect(0f, 0f, 0f, 0f),
                QualifiedPath = hoverContent != null ? hoverContent.QualifiedPath ?? string.Empty : string.Empty,
                SymbolDisplay = hoverContent != null ? hoverContent.SymbolDisplay ?? string.Empty : string.Empty,
                SummaryText = hoverContent != null ? hoverContent.SummaryText ?? string.Empty : string.Empty,
                DocumentationText = hoverContent != null ? hoverContent.DocumentationText ?? string.Empty : string.Empty,
                SignatureParts = hoverContent != null ? hoverContent.SignatureParts : new EditorHoverContentPart[0],
                SupplementalSections = hoverContent != null ? hoverContent.SupplementalSections : new EditorHoverSection[0],
                OverloadIndex = hoverContent != null ? hoverContent.OverloadIndex : -1,
                OverloadCount = hoverContent != null ? hoverContent.OverloadCount : 0,
                OverloadSummary = hoverContent != null ? hoverContent.OverloadSummary ?? string.Empty : string.Empty,
                PrimaryNavigationTarget = hoverContent != null ? hoverContent.PrimaryNavigationTarget : null
            };
        }

        private static string ResolveStickyVisibilityReason(
            SurfaceHoverState surfaceState,
            EditorHoverTarget hoverTarget,
            RenderPoint pointerPosition,
            bool hasMouse,
            RenderSize viewportSize)
        {
            if (surfaceState == null || string.IsNullOrEmpty(surfaceState.VisibleHoverKey))
            {
                return string.Empty;
            }

            if (!IsUsableTooltipViewport(viewportSize))
            {
                return "viewport-preserved";
            }

            if (hoverTarget != null &&
                string.Equals(surfaceState.VisibleHoverKey, hoverTarget.HoverKey ?? string.Empty, StringComparison.Ordinal))
            {
                return "same-hover";
            }

            if (hasMouse && IsPointerWithinHoverSurface(surfaceState, pointerPosition))
            {
                return "pointer-on-hover";
            }

            return DateTime.UtcNow <= surfaceState.KeepAliveUtc
                ? "grace-window"
                : string.Empty;
        }

        private static bool IsUsableTooltipViewport(RenderSize viewportSize)
        {
            return viewportSize.Width >= 64f && viewportSize.Height >= 64f;
        }

        private static bool ShouldSuppressRetarget(
            SurfaceHoverState surfaceState,
            string hoverKey,
            bool hasMouse,
            RenderPoint pointerPosition)
        {
            return surfaceState != null &&
                !string.IsNullOrEmpty(surfaceState.VisibleHoverKey) &&
                !string.Equals(surfaceState.VisibleHoverKey, hoverKey ?? string.Empty, StringComparison.Ordinal) &&
                hasMouse &&
                IsPointerWithinHoverSurface(surfaceState, pointerPosition);
        }

        private static bool IsPointerWithinHoverSurface(SurfaceHoverState surfaceState, RenderPoint pointerPosition)
        {
            return IsPointWithinRect(surfaceState != null ? surfaceState.VisibleTooltipRect : new RenderRect(0f, 0f, 0f, 0f), pointerPosition) ||
                IsPointWithinRect(surfaceState != null ? surfaceState.VisibleAnchorRect : new RenderRect(0f, 0f, 0f, 0f), pointerPosition) ||
                IsPointWithinRect(BuildHoverBridgeRect(surfaceState), pointerPosition);
        }

        private static RenderRect BuildHoverBridgeRect(SurfaceHoverState surfaceState)
        {
            if (surfaceState == null ||
                !HasArea(surfaceState.VisibleAnchorRect) ||
                !HasArea(surfaceState.VisibleTooltipRect))
            {
                return new RenderRect(0f, 0f, 0f, 0f);
            }

            var anchorRect = surfaceState.VisibleAnchorRect;
            var tooltipRect = surfaceState.VisibleTooltipRect;
            var overlapLeft = Math.Max(anchorRect.X, tooltipRect.X);
            var overlapRight = Math.Min(anchorRect.X + anchorRect.Width, tooltipRect.X + tooltipRect.Width);
            if (overlapRight > overlapLeft)
            {
                return new RenderRect(
                    overlapLeft - 12f,
                    Math.Min(anchorRect.Y, tooltipRect.Y) - 8f,
                    (overlapRight - overlapLeft) + 24f,
                    Math.Max(anchorRect.Y + anchorRect.Height, tooltipRect.Y + tooltipRect.Height) - Math.Min(anchorRect.Y, tooltipRect.Y) + 16f);
            }

            var overlapTop = Math.Max(anchorRect.Y, tooltipRect.Y);
            var overlapBottom = Math.Min(anchorRect.Y + anchorRect.Height, tooltipRect.Y + tooltipRect.Height);
            if (overlapBottom > overlapTop)
            {
                return new RenderRect(
                    Math.Min(anchorRect.X, tooltipRect.X) - 8f,
                    overlapTop - 12f,
                    Math.Max(anchorRect.X + anchorRect.Width, tooltipRect.X + tooltipRect.Width) - Math.Min(anchorRect.X, tooltipRect.X) + 16f,
                    (overlapBottom - overlapTop) + 24f);
            }

            var anchorCenterX = anchorRect.X + anchorRect.Width * 0.5f;
            var anchorCenterY = anchorRect.Y + anchorRect.Height * 0.5f;
            var tooltipCenterX = tooltipRect.X + tooltipRect.Width * 0.5f;
            var tooltipCenterY = tooltipRect.Y + tooltipRect.Height * 0.5f;
            var left = Math.Min(anchorCenterX, tooltipCenterX) - 16f;
            var top = Math.Min(anchorCenterY, tooltipCenterY) - 16f;
            var right = Math.Max(anchorCenterX, tooltipCenterX) + 16f;
            var bottom = Math.Max(anchorCenterY, tooltipCenterY) + 16f;
            return new RenderRect(left, top, right - left, bottom - top);
        }

        private static bool IsPointWithinRect(RenderRect rect, RenderPoint point)
        {
            return HasArea(rect) &&
                point.X >= rect.X &&
                point.X <= rect.X + rect.Width &&
                point.Y >= rect.Y &&
                point.Y <= rect.Y + rect.Height;
        }

        private static bool HasArea(RenderRect rect)
        {
            return rect.Width > 0f && rect.Height > 0f;
        }

        private static void RefreshStickyHoverKeepAlive(SurfaceHoverState surfaceState)
        {
            if (surfaceState == null)
            {
                return;
            }

            surfaceState.KeepAliveUtc = DateTime.UtcNow.AddMilliseconds(StickyHoverGraceMs);
        }

        private static int CountParts(EditorHoverContentPart[] parts)
        {
            return parts != null ? parts.Length : 0;
        }

        private static int CountInteractiveParts(EditorResolvedHoverContent hoverContent)
        {
            if (hoverContent == null || hoverContent.SignatureParts == null)
            {
                return 0;
            }

            var count = 0;
            for (var i = 0; i < hoverContent.SignatureParts.Length; i++)
            {
                if (hoverContent.SignatureParts[i] != null && hoverContent.SignatureParts[i].IsInteractive)
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountSections(EditorResolvedHoverContent hoverContent)
        {
            return hoverContent != null && hoverContent.SupplementalSections != null
                ? hoverContent.SupplementalSections.Length
                : 0;
        }

        private static void ResetCandidate(SurfaceHoverState surfaceState)
        {
            if (surfaceState == null)
            {
                return;
            }

            surfaceState.HoverCandidateKey = string.Empty;
            surfaceState.HoverCandidateUtc = DateTime.MinValue;
            surfaceState.PendingHoverTarget = null;
            surfaceState.PendingHoverUtc = DateTime.MinValue;
        }

        private static void ClearVisualRefreshRequest(CortexShellState state, string hoverKey)
        {
            if (state == null ||
                state.Editor == null ||
                state.Editor.Hover == null ||
                string.IsNullOrEmpty(hoverKey) ||
                !string.Equals(state.Editor.Hover.VisualRefreshHoverKey ?? string.Empty, hoverKey, StringComparison.Ordinal))
            {
                return;
            }

            state.Editor.Hover.VisualRefreshHoverKey = string.Empty;
            state.Editor.Hover.VisualRefreshRequestedUtc = DateTime.MinValue;
        }

        private static void CapturePendingHoverTarget(SurfaceHoverState surfaceState, EditorHoverTarget hoverTarget)
        {
            if (surfaceState == null || hoverTarget == null || hoverTarget.Target == null)
            {
                return;
            }

            surfaceState.PendingHoverTarget = CloneHoverTarget(hoverTarget);
            surfaceState.PendingHoverUtc = DateTime.UtcNow;
        }

        private static EditorHoverTarget GetPendingHoverTarget(SurfaceHoverState surfaceState)
        {
            if (surfaceState == null ||
                surfaceState.PendingHoverTarget == null ||
                surfaceState.PendingHoverTarget.Target == null)
            {
                return null;
            }

            return (DateTime.UtcNow - surfaceState.PendingHoverUtc).TotalMilliseconds <= PendingHoverTargetLifetimeMs
                ? CloneHoverTarget(surfaceState.PendingHoverTarget)
                : null;
        }

        private static EditorHoverTarget CloneHoverTarget(EditorHoverTarget hoverTarget)
        {
            if (hoverTarget == null || hoverTarget.Target == null)
            {
                return null;
            }

            return new EditorHoverTarget
            {
                SurfaceId = hoverTarget.SurfaceId ?? string.Empty,
                PaneId = hoverTarget.PaneId ?? string.Empty,
                SurfaceKind = hoverTarget.SurfaceKind,
                HoverKey = hoverTarget.HoverKey ?? string.Empty,
                TokenClassification = hoverTarget.TokenClassification ?? string.Empty,
                AnchorRect = hoverTarget.AnchorRect,
                Target = hoverTarget.Target.Clone()
            };
        }

        private SurfaceHoverState GetSurfaceState(string surfaceId)
        {
            PruneSurfaceStates();
            var key = surfaceId ?? string.Empty;
            SurfaceHoverState surfaceState;
            if (!_surfaceStates.TryGetValue(key, out surfaceState) || surfaceState == null)
            {
                surfaceState = new SurfaceHoverState();
                _surfaceStates[key] = surfaceState;
            }

            surfaceState.LastTouchedUtc = DateTime.UtcNow;
            return surfaceState;
        }

        private void PruneSurfaceStates()
        {
            if (_surfaceStates.Count < MaxTrackedSurfaceStates)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var staleKeys = new List<string>();
            foreach (var pair in _surfaceStates)
            {
                var state = pair.Value;
                if (state == null)
                {
                    staleKeys.Add(pair.Key);
                    continue;
                }

                var hasLiveHover =
                    !string.IsNullOrEmpty(state.VisibleHoverKey) ||
                    !string.IsNullOrEmpty(state.HoverCandidateKey) ||
                    (state.PendingHoverTarget != null && state.PendingHoverTarget.Target != null);
                if (hasLiveHover)
                {
                    continue;
                }

                if ((now - state.LastTouchedUtc).TotalMilliseconds >= SurfaceStateStaleMs)
                {
                    staleKeys.Add(pair.Key);
                }
            }

            for (var i = 0; i < staleKeys.Count; i++)
            {
                _surfaceStates.Remove(staleKeys[i]);
            }
        }

        private static string BuildHoverKey(DocumentSession session, EditorCommandTarget target)
        {
            return BuildHoverKey(session != null ? session.FilePath : (target != null ? target.DocumentPath : string.Empty), target != null ? target.AbsolutePosition : -1);
        }

        private static string BuildHoverKey(string documentPath, int absolutePosition)
        {
            return (documentPath ?? string.Empty) + "|" + absolutePosition;
        }

        private void ApplyInteractionCapabilities(EditorCommandTarget target, LanguageServiceHoverResponse hoverResponse)
        {
            if (target == null)
            {
                return;
            }

            _symbolInteractionService.ApplyHoverMetadata(target, hoverResponse);
        }

        private void EnsureHoverResponseOnContext(
            CortexShellState state,
            string contextKey,
            string hoverKey,
            LanguageServiceHoverResponse hoverResponse)
        {
            if (_contextService == null ||
                string.IsNullOrEmpty(contextKey) ||
                string.IsNullOrEmpty(hoverKey) ||
                hoverResponse == null ||
                !hoverResponse.Success)
            {
                return;
            }

            var existingResponse = _contextService.ResolveHoverResponse(state, contextKey, hoverKey);
            if (existingResponse != null && existingResponse.Success)
            {
                return;
            }

            _contextService.ApplyHoverResponse(state, contextKey, hoverKey, hoverResponse);
        }

        private static void LogHoverDiagnostic(
            string category,
            EditorHoverTarget hoverTarget,
            SurfaceHoverState surfaceState,
            string detail)
        {
            var hoverKey = hoverTarget != null
                ? hoverTarget.HoverKey ?? string.Empty
                : surfaceState != null ? surfaceState.VisibleHoverKey ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(hoverKey))
            {
                return;
            }

            CortexDeveloperLog.WriteHoverDiagnostic(category, hoverKey, detail ?? string.Empty);
        }

        private sealed class SurfaceHoverState
        {
            public string HoverCandidateKey = string.Empty;
            public DateTime HoverCandidateUtc = DateTime.MinValue;
            public EditorHoverTarget PendingHoverTarget;
            public DateTime PendingHoverUtc = DateTime.MinValue;
            public string VisibleHoverKey = string.Empty;
            public string VisibleContextKey = string.Empty;
            public string VisibleDocumentPath = string.Empty;
            public RenderRect VisibleAnchorRect = new RenderRect(0f, 0f, 0f, 0f);
            public RenderRect VisibleTooltipRect = new RenderRect(0f, 0f, 0f, 0f);
            public DateTime KeepAliveUtc = DateTime.MinValue;
            public string LastVisibleHoverLogKey = string.Empty;
            public string LastClearLogKey = string.Empty;
            public string LastRetargetSuppressionLogKey = string.Empty;
            public DateTime LastTouchedUtc = DateTime.UtcNow;
        }
    }
}
