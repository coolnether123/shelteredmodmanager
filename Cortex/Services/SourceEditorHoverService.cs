using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class SourceEditorHoverTarget
    {
        public string HoverKey = string.Empty;
        public EditorCommandTarget Target;
    }

    internal sealed class SourceEditorHoverService
    {
        private const float HoverDelaySeconds = 0.18f;
        private readonly EditorCommandContextFactory _contextFactory = new EditorCommandContextFactory();
        private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();
        private readonly IEditorContextService _contextService;

        public SourceEditorHoverService(IEditorContextService contextService)
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
            ApplyInteractionCapabilities(target, session, state, editingEnabled, ResolveHoverResponse(state, BuildHoverKey(session, target)));
            return true;
        }

        public bool TryCreateHoverTarget(
            DocumentSession session,
            CortexShellState state,
            bool editingEnabled,
            int absolutePosition,
            out SourceEditorHoverTarget hoverTarget)
        {
            hoverTarget = null;
            EditorCommandTarget target;
            if (!TryCreateInteractionTarget(session, state, editingEnabled, absolutePosition, out target))
            {
                return false;
            }

            hoverTarget = new SourceEditorHoverTarget
            {
                HoverKey = BuildHoverKey(session, target),
                Target = target
            };
            return true;
        }

        public void UpdateHoverRequest(
            DocumentSession session,
            CortexShellState state,
            SourceEditorHoverTarget hoverTarget,
            bool hasMouse,
            ref string hoverCandidateKey,
            ref DateTime hoverCandidateUtc)
        {
            if (!hasMouse || state == null || state.Editor == null || hoverTarget == null || hoverTarget.Target == null || string.IsNullOrEmpty(hoverTarget.HoverKey))
            {
                hoverCandidateKey = string.Empty;
                hoverCandidateUtc = DateTime.MinValue;
                ClearVisibleHover(state);
                return;
            }

            if (!string.Equals(hoverCandidateKey, hoverTarget.HoverKey, StringComparison.Ordinal))
            {
                hoverCandidateKey = hoverTarget.HoverKey;
                hoverCandidateUtc = DateTime.UtcNow;
                ClearVisibleHover(state);
                return;
            }

            if ((DateTime.UtcNow - hoverCandidateUtc).TotalSeconds < HoverDelaySeconds)
            {
                return;
            }

            var response = ResolveHoverResponse(state, hoverTarget.HoverKey);
            if (response != null && response.Success)
            {
                return;
            }

            if (string.Equals(state.Editor.Hover.RequestedKey, hoverTarget.HoverKey, StringComparison.Ordinal))
            {
                return;
            }

            state.Editor.Hover.RequestedKey = hoverTarget.HoverKey;
            state.Editor.Hover.RequestedContextKey = hoverTarget.Target.ContextKey ?? string.Empty;
            state.Editor.Hover.RequestedDocumentPath = session != null ? session.FilePath ?? string.Empty : string.Empty;
            state.Editor.Hover.RequestedLine = hoverTarget.Target.Line;
            state.Editor.Hover.RequestedColumn = hoverTarget.Target.Column;
            state.Editor.Hover.RequestedAbsolutePosition = hoverTarget.Target.AbsolutePosition;
            state.Editor.Hover.RequestedTokenText = hoverTarget.Target.SymbolText ?? string.Empty;
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

        public void SetVisibleHover(CortexShellState state, string hoverKey, LanguageServiceHoverResponse response)
        {
            if (state == null || state.Editor == null)
            {
                return;
            }

            state.Editor.Hover.VisibleContextKey = state.Editor.Hover.ActiveContextKey ?? string.Empty;
            state.Editor.Hover.VisibleDefinitionDocumentPath = response != null ? response.DefinitionDocumentPath ?? string.Empty : string.Empty;
        }

        public void ClearVisibleHover(CortexShellState state)
        {
            if (state == null || state.Editor == null)
            {
                return;
            }

            state.Editor.Hover.VisibleContextKey = string.Empty;
            state.Editor.Hover.VisibleDefinitionDocumentPath = string.Empty;
        }

        private string BuildHoverKey(DocumentSession session, EditorCommandTarget target)
        {
            return (session != null ? session.FilePath ?? string.Empty : string.Empty) +
                "|" + (target != null ? target.AbsolutePosition : -1);
        }

        private void ApplyInteractionCapabilities(EditorCommandTarget target, DocumentSession session, CortexShellState state, bool editingEnabled, LanguageServiceHoverResponse hoverResponse)
        {
            if (target == null)
            {
                return;
            }

            _symbolInteractionService.ApplyHoverMetadata(target, hoverResponse);
        }
    }
}
