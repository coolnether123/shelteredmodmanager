using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Rendering.Models;
using Cortex.Services.Semantics.Context;
using Cortex.Services.Editor.Context;

namespace Cortex.Services.Semantics.Hover
{
    internal sealed class EditorHoverTargetFactory
    {
        private readonly EditorCommandContextFactory _contextFactory = new EditorCommandContextFactory();
        private readonly EditorSymbolInteractionService _symbolInteractionService = new EditorSymbolInteractionService();
        private readonly IEditorContextService _contextService;

        public EditorHoverTargetFactory(IEditorContextService contextService)
        {
            _contextService = contextService;
        }

        public bool TryCreateInteractionTarget(DocumentSession session, CortexShellState state, bool editingEnabled, int absolutePosition, out EditorCommandTarget target)
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

        public bool TryCreateSourceHoverTarget(DocumentSession session, CortexShellState state, bool editingEnabled, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, int absolutePosition, RenderRect anchorRect, string tokenClassification, out EditorHoverTarget hoverTarget)
        {
            hoverTarget = null;
            EditorCommandTarget target;
            if (!TryCreateInteractionTarget(session, state, editingEnabled, absolutePosition, out target))
            {
                return false;
            }

            return TryPublishHoverTarget(state, session, surfaceId, paneId, surfaceKind, target, anchorRect, tokenClassification, out hoverTarget);
        }

        public bool TryCreateReadOnlyHoverTarget(DocumentSession session, CortexShellState state, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, int absolutePosition, int line, int column, string tokenText, bool canNavigateToDefinition, string tokenClassification, RenderRect anchorRect, out EditorHoverTarget hoverTarget)
        {
            hoverTarget = null;
            EditorCommandInvocation invocation;
            var hoverKey = BuildHoverKey(session != null ? session.FilePath : string.Empty, absolutePosition);
            var hoverResponse = ResolveHoverResponse(state, hoverKey);
            if (!_contextFactory.TryCreateTokenInvocation(session, state, absolutePosition, line, column, tokenText, hoverResponse, canNavigateToDefinition, out invocation) ||
                invocation == null ||
                invocation.Target == null)
            {
                return false;
            }

            return TryPublishHoverTarget(state, session, surfaceId, paneId, surfaceKind, invocation.Target, anchorRect, tokenClassification, out hoverTarget);
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

        private bool TryPublishHoverTarget(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandTarget target, RenderRect anchorRect, string tokenClassification, out EditorHoverTarget hoverTarget)
        {
            hoverTarget = null;
            if (_contextService == null || state == null || target == null)
            {
                return false;
            }

            var snapshot = _contextService.PublishTargetContext(state, session, surfaceId, paneId, surfaceKind, target, false);
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
                Target = snapshot.Target.Clone()
            };
            return true;
        }

        private void ApplyInteractionCapabilities(EditorCommandTarget target, LanguageServiceHoverResponse hoverResponse)
        {
            if (target == null)
            {
                return;
            }

            _symbolInteractionService.ApplyHoverMetadata(target, hoverResponse);
        }

        private static string BuildHoverKey(DocumentSession session, EditorCommandTarget target)
        {
            return BuildHoverKey(session != null ? session.FilePath : (target != null ? target.DocumentPath : string.Empty), target != null ? target.AbsolutePosition : -1);
        }

        private static string BuildHoverKey(string documentPath, int absolutePosition)
        {
            return (documentPath ?? string.Empty) + "|" + absolutePosition;
        }
    }
}
