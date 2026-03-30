using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Editor.Context;

namespace Cortex.Services.Semantics.Context
{
    internal sealed class EditorContextService : IEditorContextService
    {
        private readonly EditorCommandContextFactory _contextFactory;
        private readonly EditorContextSnapshotBuilder _snapshotBuilder;
        private readonly EditorContextStoreService _storeService;
        private readonly EditorContextSemanticMutationService _semanticMutationService;

        public EditorContextService(IEditorService editorService, EditorCommandContextFactory contextFactory, EditorSymbolInteractionService symbolInteractionService)
        {
            var projectionService = new EditorContextProjectionService();
            _contextFactory = contextFactory ?? new EditorCommandContextFactory();
            _snapshotBuilder = new EditorContextSnapshotBuilder(editorService ?? new EditorService(), projectionService);
            _storeService = new EditorContextStoreService(_contextFactory, projectionService);
            _semanticMutationService = new EditorContextSemanticMutationService(symbolInteractionService ?? new EditorSymbolInteractionService(), projectionService, _storeService);
        }

        public string BuildSurfaceId(string documentPath, EditorSurfaceKind surfaceKind, string paneId) { return _snapshotBuilder.BuildSurfaceId(documentPath, surfaceKind, paneId); }
        public string BuildContextKey(string surfaceId, string documentPath, int documentVersion, int caretIndex, int selectionStart, int selectionEnd, int targetStart, int targetLength, string symbolText) { return _snapshotBuilder.BuildContextKey(surfaceId, documentPath, documentVersion, caretIndex, selectionStart, selectionEnd, targetStart, targetLength, symbolText); }
        public EditorContextSnapshot GetActiveContext(CortexShellState state) { return GetContext(state, state != null && state.EditorContext != null ? state.EditorContext.ActiveContextKey : string.Empty); }
        public EditorContextSnapshot GetHoveredContext(CortexShellState state) { return GetContext(state, state != null && state.EditorContext != null ? state.EditorContext.HoveredContextKey : string.Empty); }
        public EditorContextSnapshot GetContext(CortexShellState state, string contextKey) { return _storeService.GetContext(state, contextKey); }
        public EditorContextSnapshot GetSurfaceContext(CortexShellState state, string surfaceId) { return _storeService.GetSurfaceContext(state, surfaceId); }
        public EditorCommandTarget ResolveTarget(CortexShellState state, string contextKey) { return _storeService.ResolveTarget(state, contextKey); }
        public EditorCommandInvocation ResolveInvocation(CortexShellState state, string contextKey) { return _storeService.ResolveInvocation(state, contextKey); }
        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string contextKey, string hoverKey) { return _storeService.ResolveHoverResponse(state, contextKey, hoverKey); }
        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey) { return _storeService.ResolveHoverResponse(state, hoverKey); }
        public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string contextKey, string hoverKey) { return _storeService.ResolveHoverContent(state, contextKey, hoverKey); }
        public EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string hoverKey) { return _storeService.ResolveHoverContent(state, hoverKey); }
        public string ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, LanguageServiceHoverResponse response) { return _semanticMutationService.ApplyHoverResponse(state, contextKey, hoverKey, response); }
        public void ApplyHoverContent(CortexShellState state, string contextKey, string hoverKey, EditorResolvedHoverContent content) { _semanticMutationService.ApplyHoverContent(state, contextKey, hoverKey, content); }
        public void ApplySymbolContext(CortexShellState state, string contextKey, LanguageServiceSymbolContextResponse response) { _semanticMutationService.ApplySymbolContext(state, contextKey, response); }
        public void ClearHoverResponse(CortexShellState state, string contextKey) { _semanticMutationService.ClearHoverResponse(state, contextKey); }
        public void PublishHoveredContext(CortexShellState state, string contextKey, string definitionDocumentPath) { _semanticMutationService.PublishHoveredContext(state, contextKey, definitionDocumentPath); }
        public void ClearHoveredContext(CortexShellState state) { _semanticMutationService.ClearHoveredContext(state); }

        public EditorContextSnapshot PublishDocumentContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, bool editingEnabled, int absolutePosition)
        {
            if (state == null || session == null)
            {
                return null;
            }

            var invocation = _contextFactory.CreateDocumentInvocation(session, state, editingEnabled, absolutePosition);
            return PublishInvocationContext(state, session, surfaceId, paneId, surfaceKind, invocation, true);
        }

        public EditorContextSnapshot PublishInvocationContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandInvocation invocation, bool setActive)
        {
            var target = invocation != null ? invocation.Target : null;
            var snapshot = PublishTargetContext(state, session, surfaceId, paneId, surfaceKind, target, setActive);
            if (invocation != null && invocation.Target != null && snapshot != null && snapshot.Target != null)
            {
                invocation.Target.ContextKey = snapshot.Target.ContextKey;
                invocation.Target.SurfaceId = snapshot.Target.SurfaceId;
            }

            return snapshot;
        }

        public EditorContextSnapshot PublishTargetContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandTarget target, bool setActive)
        {
            if (state == null || target == null)
            {
                return null;
            }

            _snapshotBuilder.EnsureDocumentState(session);
            var snapshot = _snapshotBuilder.BuildSnapshot(state, session, surfaceId, paneId, surfaceKind, target);
            target.ContextKey = snapshot.ContextKey ?? string.Empty;
            target.SurfaceId = snapshot.SurfaceId ?? string.Empty;
            _storeService.StoreSnapshot(state, snapshot, setActive);
            return snapshot;
        }
    }
}
