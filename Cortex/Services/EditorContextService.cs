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
        LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string contextKey, string hoverKey);
        LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey);
        void ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, LanguageServiceHoverResponse response);
        void ApplySymbolContext(CortexShellState state, string contextKey, LanguageServiceSymbolContextResponse response);
        void ClearHoverResponse(CortexShellState state, string contextKey);
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
            return ProjectTarget(snapshot);
        }

        public EditorCommandInvocation ResolveInvocation(CortexShellState state, string contextKey)
        {
            var target = ResolveTarget(state, contextKey);
            return target != null ? _contextFactory.CreateForTarget(state, target) : null;
        }

        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string contextKey, string hoverKey)
        {
            var snapshot = GetContext(state, contextKey);
            if (snapshot == null || snapshot.Semantic == null)
            {
                return null;
            }

            return string.IsNullOrEmpty(hoverKey) || string.Equals(snapshot.HoverKey ?? string.Empty, hoverKey, StringComparison.Ordinal)
                ? snapshot.Semantic.HoverResponse
                : null;
        }

        public LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey)
        {
            if (state == null || state.EditorContext == null || string.IsNullOrEmpty(hoverKey))
            {
                return null;
            }

            foreach (var pair in state.EditorContext.ContextsByKey)
            {
                var snapshot = pair.Value;
                if (snapshot == null ||
                    !string.Equals(snapshot.HoverKey ?? string.Empty, hoverKey, StringComparison.Ordinal))
                {
                    continue;
                }

                return snapshot.Semantic != null ? snapshot.Semantic.HoverResponse : null;
            }

            return null;
        }

        public void ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, LanguageServiceHoverResponse response)
        {
            var snapshot = GetContext(state, contextKey);
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.Semantic == null)
            {
                snapshot.Semantic = new EditorSemanticContext();
            }

            snapshot.HoverKey = hoverKey ?? snapshot.HoverKey ?? string.Empty;
            snapshot.Semantic.HoverResponse = response;
            var projected = ProjectTarget(snapshot);
            _symbolInteractionService.ApplyHoverMetadata(projected, response);
            CopySemanticFields(projected, snapshot.Semantic);
            snapshot.FocusTokenText = !string.IsNullOrEmpty(projected.SymbolText)
                ? projected.SymbolText
                : snapshot.FocusTokenText ?? string.Empty;
            snapshot.TargetStart = projected.AbsolutePosition;
            snapshot.TargetLength = !string.IsNullOrEmpty(projected.SymbolText)
                ? projected.SymbolText.Length
                : snapshot.TargetLength;
            snapshot.ContainingMemberName = ResolveContainingMemberName(projected, snapshot.Semantic);
        }

        public void ApplySymbolContext(CortexShellState state, string contextKey, LanguageServiceSymbolContextResponse response)
        {
            var snapshot = GetContext(state, contextKey);
            if (snapshot == null || response == null)
            {
                return;
            }

            if (snapshot.Semantic == null)
            {
                snapshot.Semantic = new EditorSemanticContext();
            }

            if (!string.IsNullOrEmpty(response.QualifiedSymbolDisplay))
            {
                snapshot.Semantic.QualifiedSymbolDisplay = response.QualifiedSymbolDisplay;
            }

            if (!string.IsNullOrEmpty(response.SymbolKind))
            {
                snapshot.Semantic.SymbolKind = response.SymbolKind;
            }

            if (!string.IsNullOrEmpty(response.MetadataName))
            {
                snapshot.Semantic.MetadataName = response.MetadataName;
            }

            if (!string.IsNullOrEmpty(response.ContainingTypeName))
            {
                snapshot.Semantic.ContainingTypeName = response.ContainingTypeName;
            }

            if (!string.IsNullOrEmpty(response.ContainingAssemblyName))
            {
                snapshot.Semantic.ContainingAssemblyName = response.ContainingAssemblyName;
            }

            if (!string.IsNullOrEmpty(response.DocumentationCommentId))
            {
                snapshot.Semantic.DocumentationCommentId = response.DocumentationCommentId;
            }

            if (!string.IsNullOrEmpty(response.DefinitionDocumentPath))
            {
                snapshot.Semantic.DefinitionDocumentPath = response.DefinitionDocumentPath;
            }

            if (response.DefinitionRange != null)
            {
                snapshot.Semantic.DefinitionStart = response.DefinitionRange.Start;
                snapshot.Semantic.DefinitionLength = response.DefinitionRange.Length;
                snapshot.Semantic.DefinitionLine = response.DefinitionRange.StartLine;
                snapshot.Semantic.DefinitionColumn = response.DefinitionRange.StartColumn;
            }

            snapshot.ContainingMemberName = ResolveContainingMemberName(ProjectTarget(snapshot), snapshot.Semantic);
        }

        public void ClearHoverResponse(CortexShellState state, string contextKey)
        {
            var snapshot = GetContext(state, contextKey);
            if (snapshot == null || snapshot.Semantic == null)
            {
                return;
            }

            snapshot.Semantic.HoverResponse = null;
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
                ContainingMemberName = ResolveContainingMemberName(clonedTarget, null),
                CapturedUtc = DateTime.UtcNow,
                Target = BuildBaseTarget(clonedTarget),
                Semantic = BuildSemanticContext(clonedTarget)
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

        private static string ResolveContainingMemberName(EditorCommandTarget target, EditorSemanticContext semantic)
        {
            if (target == null)
            {
                return string.Empty;
            }

            var symbolKind = semantic != null && !string.IsNullOrEmpty(semantic.SymbolKind)
                ? semantic.SymbolKind
                : target.SymbolKind;
            return !string.IsNullOrEmpty(target.SymbolText) &&
                !string.IsNullOrEmpty(symbolKind) &&
                symbolKind.IndexOf("method", StringComparison.OrdinalIgnoreCase) >= 0
                ? target.SymbolText
                : string.Empty;
        }

        private static EditorCommandTarget BuildBaseTarget(EditorCommandTarget source)
        {
            if (source == null)
            {
                return null;
            }

            var clone = source.Clone();
            clone.QualifiedSymbolDisplay = string.Empty;
            clone.SymbolKind = string.Empty;
            clone.MetadataName = string.Empty;
            clone.ContainingTypeName = string.Empty;
            clone.ContainingAssemblyName = string.Empty;
            clone.DocumentationCommentId = string.Empty;
            clone.HoverText = string.Empty;
            clone.DefinitionDocumentPath = string.Empty;
            clone.DefinitionLine = 0;
            clone.DefinitionColumn = 0;
            clone.DefinitionStart = -1;
            clone.DefinitionLength = -1;
            return clone;
        }

        private static EditorSemanticContext BuildSemanticContext(EditorCommandTarget source)
        {
            if (source == null)
            {
                return new EditorSemanticContext();
            }

            return new EditorSemanticContext
            {
                QualifiedSymbolDisplay = source.QualifiedSymbolDisplay ?? string.Empty,
                SymbolKind = source.SymbolKind ?? string.Empty,
                MetadataName = source.MetadataName ?? string.Empty,
                ContainingTypeName = source.ContainingTypeName ?? string.Empty,
                ContainingAssemblyName = source.ContainingAssemblyName ?? string.Empty,
                DocumentationCommentId = source.DocumentationCommentId ?? string.Empty,
                HoverText = source.HoverText ?? string.Empty,
                DefinitionDocumentPath = source.DefinitionDocumentPath ?? string.Empty,
                DefinitionLine = source.DefinitionLine,
                DefinitionColumn = source.DefinitionColumn,
                DefinitionStart = source.DefinitionStart,
                DefinitionLength = source.DefinitionLength
            };
        }

        private static EditorCommandTarget ProjectTarget(EditorContextSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Target == null)
            {
                return null;
            }

            var projected = snapshot.Target.Clone();
            CopySemanticFields(projected, snapshot.Semantic);
            return projected;
        }

        private static void CopySemanticFields(EditorCommandTarget target, EditorSemanticContext semantic)
        {
            if (target == null || semantic == null)
            {
                return;
            }

            target.QualifiedSymbolDisplay = semantic.QualifiedSymbolDisplay ?? string.Empty;
            target.SymbolKind = semantic.SymbolKind ?? string.Empty;
            target.MetadataName = semantic.MetadataName ?? string.Empty;
            target.ContainingTypeName = semantic.ContainingTypeName ?? string.Empty;
            target.ContainingAssemblyName = semantic.ContainingAssemblyName ?? string.Empty;
            target.DocumentationCommentId = semantic.DocumentationCommentId ?? string.Empty;
            target.HoverText = semantic.HoverText ?? string.Empty;
            target.DefinitionDocumentPath = semantic.DefinitionDocumentPath ?? string.Empty;
            target.DefinitionLine = semantic.DefinitionLine;
            target.DefinitionColumn = semantic.DefinitionColumn;
            target.DefinitionStart = semantic.DefinitionStart;
            target.DefinitionLength = semantic.DefinitionLength;
        }
    }
}
