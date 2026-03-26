using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal interface IEditorContextService
    {
        string BuildSurfaceId(string documentPath, EditorSurfaceKind surfaceKind, string paneId);
        EditorContextSnapshot PublishDocumentContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, bool editingEnabled, int absolutePosition);
        EditorContextSnapshot PublishInvocationContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandInvocation invocation, bool setActive);
        EditorContextSnapshot PublishTargetContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandTarget target, bool setActive);
        EditorContextSnapshot GetActiveContext(CortexShellState state);
        EditorContextSnapshot GetContext(CortexShellState state, string contextKey);
        EditorContextSnapshot GetSurfaceContext(CortexShellState state, string surfaceId);
        EditorCommandTarget ResolveTarget(CortexShellState state, string contextKey);
        EditorCommandInvocation ResolveInvocation(CortexShellState state, string contextKey);
        void ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, LanguageServiceHoverResponse response);
        void ApplySymbolContext(CortexShellState state, string contextKey, LanguageServiceSymbolContextResponse response);
        string BuildContextKey(string surfaceId, string documentPath, int documentVersion, int caretIndex, int selectionStart, int selectionEnd, int targetStart, int targetLength, string symbolText);
    }

    internal sealed class EditorContextService : IEditorContextService
    {
        private const int MaxTrackedContexts = 32;
        private readonly IEditorService _editorService;
        private readonly EditorCommandContextFactory _contextFactory;
        private readonly EditorSymbolInteractionService _symbolInteractionService;

        public EditorContextService(
            IEditorService editorService,
            EditorCommandContextFactory contextFactory,
            EditorSymbolInteractionService symbolInteractionService)
        {
            _editorService = editorService ?? new EditorService();
            _contextFactory = contextFactory ?? new EditorCommandContextFactory();
            _symbolInteractionService = symbolInteractionService ?? new EditorSymbolInteractionService();
        }

        public string BuildSurfaceId(string documentPath, EditorSurfaceKind surfaceKind, string paneId)
        {
            return (surfaceKind.ToString() + "|" + (paneId ?? string.Empty) + "|" + (documentPath ?? string.Empty)).ToLowerInvariant();
        }

        public EditorContextSnapshot PublishDocumentContext(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            string paneId,
            EditorSurfaceKind surfaceKind,
            bool editingEnabled,
            int absolutePosition)
        {
            if (state == null || session == null)
            {
                return null;
            }

            var invocation = _contextFactory.CreateDocumentInvocation(session, state, editingEnabled, absolutePosition);
            return PublishInvocationContext(state, session, surfaceId, paneId, surfaceKind, invocation, true);
        }

        public EditorContextSnapshot PublishInvocationContext(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            string paneId,
            EditorSurfaceKind surfaceKind,
            EditorCommandInvocation invocation,
            bool setActive)
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

        public EditorContextSnapshot PublishTargetContext(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            string paneId,
            EditorSurfaceKind surfaceKind,
            EditorCommandTarget target,
            bool setActive)
        {
            if (state == null || target == null)
            {
                return null;
            }

            if (session != null)
            {
                _editorService.EnsureDocumentState(session);
            }

            var snapshot = BuildSnapshot(state, session, surfaceId, paneId, surfaceKind, target);
            target.ContextKey = snapshot.ContextKey ?? string.Empty;
            target.SurfaceId = snapshot.SurfaceId ?? string.Empty;
            var contexts = state.EditorContext != null ? state.EditorContext.ContextsByKey : null;
            if (contexts == null || string.IsNullOrEmpty(snapshot.ContextKey))
            {
                return snapshot;
            }

            contexts[snapshot.ContextKey] = snapshot;
            if (!string.IsNullOrEmpty(snapshot.SurfaceId))
            {
                state.EditorContext.SurfaceContextKeys[snapshot.SurfaceId] = snapshot.ContextKey;
            }

            if (setActive)
            {
                state.EditorContext.ActiveSurfaceId = snapshot.SurfaceId ?? string.Empty;
                state.EditorContext.ActiveContextKey = snapshot.ContextKey ?? string.Empty;
            }

            TrimOldContexts(state);
            return snapshot;
        }

        public EditorContextSnapshot GetActiveContext(CortexShellState state)
        {
            return GetContext(state, state != null && state.EditorContext != null ? state.EditorContext.ActiveContextKey : string.Empty);
        }

        public EditorContextSnapshot GetContext(CortexShellState state, string contextKey)
        {
            if (state == null || state.EditorContext == null || string.IsNullOrEmpty(contextKey))
            {
                return null;
            }

            EditorContextSnapshot snapshot;
            return state.EditorContext.ContextsByKey.TryGetValue(contextKey, out snapshot) ? snapshot : null;
        }

        public EditorContextSnapshot GetSurfaceContext(CortexShellState state, string surfaceId)
        {
            if (state == null || state.EditorContext == null || string.IsNullOrEmpty(surfaceId))
            {
                return null;
            }

            string contextKey;
            return state.EditorContext.SurfaceContextKeys.TryGetValue(surfaceId, out contextKey)
                ? GetContext(state, contextKey)
                : null;
        }

        public EditorCommandTarget ResolveTarget(CortexShellState state, string contextKey)
        {
            var snapshot = GetContext(state, contextKey);
            return snapshot != null && snapshot.Target != null ? snapshot.Target.Clone() : null;
        }

        public EditorCommandInvocation ResolveInvocation(CortexShellState state, string contextKey)
        {
            var target = ResolveTarget(state, contextKey);
            return target != null ? _contextFactory.CreateForTarget(state, target) : null;
        }

        public void ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, LanguageServiceHoverResponse response)
        {
            var snapshot = GetContext(state, contextKey);
            if (snapshot == null || snapshot.Target == null)
            {
                return;
            }

            snapshot.HoverKey = hoverKey ?? snapshot.HoverKey ?? string.Empty;
            _symbolInteractionService.ApplyHoverMetadata(snapshot.Target, response);
            snapshot.FocusTokenText = !string.IsNullOrEmpty(snapshot.Target.SymbolText)
                ? snapshot.Target.SymbolText
                : snapshot.FocusTokenText ?? string.Empty;
            snapshot.TargetStart = snapshot.Target.AbsolutePosition;
            snapshot.TargetLength = !string.IsNullOrEmpty(snapshot.Target.SymbolText)
                ? snapshot.Target.SymbolText.Length
                : snapshot.TargetLength;
            snapshot.ContainingMemberName = ResolveContainingMemberName(snapshot.Target);
        }

        public void ApplySymbolContext(CortexShellState state, string contextKey, LanguageServiceSymbolContextResponse response)
        {
            var snapshot = GetContext(state, contextKey);
            if (snapshot == null || snapshot.Target == null || response == null)
            {
                return;
            }

            if (!string.IsNullOrEmpty(response.QualifiedSymbolDisplay))
            {
                snapshot.Target.QualifiedSymbolDisplay = response.QualifiedSymbolDisplay;
            }

            if (!string.IsNullOrEmpty(response.SymbolKind))
            {
                snapshot.Target.SymbolKind = response.SymbolKind;
            }

            if (!string.IsNullOrEmpty(response.MetadataName))
            {
                snapshot.Target.MetadataName = response.MetadataName;
            }

            if (!string.IsNullOrEmpty(response.ContainingTypeName))
            {
                snapshot.Target.ContainingTypeName = response.ContainingTypeName;
            }

            if (!string.IsNullOrEmpty(response.ContainingAssemblyName))
            {
                snapshot.Target.ContainingAssemblyName = response.ContainingAssemblyName;
            }

            if (!string.IsNullOrEmpty(response.DocumentationCommentId))
            {
                snapshot.Target.DocumentationCommentId = response.DocumentationCommentId;
            }

            if (!string.IsNullOrEmpty(response.DefinitionDocumentPath))
            {
                snapshot.Target.DefinitionDocumentPath = response.DefinitionDocumentPath;
            }

            if (response.DefinitionRange != null)
            {
                snapshot.Target.DefinitionStart = response.DefinitionRange.Start;
                snapshot.Target.DefinitionLength = response.DefinitionRange.Length;
                snapshot.Target.DefinitionLine = response.DefinitionRange.StartLine;
                snapshot.Target.DefinitionColumn = response.DefinitionRange.StartColumn;
            }

            snapshot.ContainingMemberName = ResolveContainingMemberName(snapshot.Target);
        }

        public string BuildContextKey(
            string surfaceId,
            string documentPath,
            int documentVersion,
            int caretIndex,
            int selectionStart,
            int selectionEnd,
            int targetStart,
            int targetLength,
            string symbolText)
        {
            return (surfaceId ?? string.Empty) + "|" +
                (documentPath ?? string.Empty) + "|" +
                documentVersion + "|" +
                caretIndex + "|" +
                selectionStart + "|" +
                selectionEnd + "|" +
                targetStart + "|" +
                targetLength + "|" +
                (symbolText ?? string.Empty);
        }

        private EditorContextSnapshot BuildSnapshot(
            CortexShellState state,
            DocumentSession session,
            string surfaceId,
            string paneId,
            EditorSurfaceKind surfaceKind,
            EditorCommandTarget target)
        {
            var clonedTarget = target.Clone();
            var caretIndex = clonedTarget.CaretIndex;
            var selectionAnchorIndex = session != null && session.EditorState != null
                ? session.EditorState.SelectionAnchorIndex
                : (clonedTarget.HasSelection ? clonedTarget.SelectionStart : caretIndex);
            var selectionStart = clonedTarget.SelectionStart;
            var selectionEnd = clonedTarget.SelectionEnd;
            var documentPath = session != null ? session.FilePath ?? clonedTarget.DocumentPath ?? string.Empty : clonedTarget.DocumentPath ?? string.Empty;
            var documentVersion = session != null ? session.TextVersion : 0;
            var targetStart = clonedTarget.AbsolutePosition;
            var targetLength = !string.IsNullOrEmpty(clonedTarget.SymbolText) ? clonedTarget.SymbolText.Length : 0;
            var contextKey = BuildContextKey(
                surfaceId,
                documentPath,
                documentVersion,
                caretIndex,
                selectionStart,
                selectionEnd,
                targetStart,
                targetLength,
                clonedTarget.SymbolText);

            clonedTarget.ContextKey = contextKey;
            clonedTarget.SurfaceId = surfaceId ?? string.Empty;

            return new EditorContextSnapshot
            {
                ContextKey = contextKey,
                SurfaceId = surfaceId ?? string.Empty,
                PaneId = paneId ?? string.Empty,
                SurfaceKind = surfaceKind,
                DocumentPath = documentPath,
                DocumentVersion = documentVersion,
                DocumentKind = session != null ? session.Kind : clonedTarget.DocumentKind,
                CaretIndex = caretIndex,
                SelectionAnchorIndex = selectionAnchorIndex,
                SelectionStart = selectionStart,
                SelectionEnd = selectionEnd,
                HasSelection = clonedTarget.HasSelection,
                SelectionText = clonedTarget.SelectionText ?? string.Empty,
                TargetStart = targetStart,
                TargetLength = targetLength,
                FocusTokenText = !string.IsNullOrEmpty(clonedTarget.SymbolText)
                    ? clonedTarget.SymbolText
                    : clonedTarget.SelectionText ?? string.Empty,
                HoverKey = BuildHoverKey(documentPath, clonedTarget.AbsolutePosition),
                ContainingMemberName = ResolveContainingMemberName(clonedTarget),
                CapturedUtc = DateTime.UtcNow,
                Target = clonedTarget
            };
        }

        private void TrimOldContexts(CortexShellState state)
        {
            if (state == null || state.EditorContext == null || state.EditorContext.ContextsByKey.Count <= MaxTrackedContexts)
            {
                return;
            }

            string oldestKey = string.Empty;
            DateTime oldestUtc = DateTime.MaxValue;
            foreach (var pair in state.EditorContext.ContextsByKey)
            {
                if (pair.Value == null ||
                    string.Equals(pair.Key, state.EditorContext.ActiveContextKey, StringComparison.Ordinal))
                {
                    continue;
                }

                if (pair.Value.CapturedUtc < oldestUtc)
                {
                    oldestUtc = pair.Value.CapturedUtc;
                    oldestKey = pair.Key;
                }
            }

            if (!string.IsNullOrEmpty(oldestKey))
            {
                state.EditorContext.ContextsByKey.Remove(oldestKey);
            }
        }

        private static string BuildHoverKey(string documentPath, int absolutePosition)
        {
            return (documentPath ?? string.Empty) + "|" + absolutePosition;
        }

        private static string ResolveContainingMemberName(EditorCommandTarget target)
        {
            if (target == null)
            {
                return string.Empty;
            }

            return !string.IsNullOrEmpty(target.SymbolText) &&
                !string.IsNullOrEmpty(target.SymbolKind) &&
                target.SymbolKind.IndexOf("method", StringComparison.OrdinalIgnoreCase) >= 0
                ? target.SymbolText
                : string.Empty;
        }
    }
}
