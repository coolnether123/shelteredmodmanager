using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services.Semantics.Context
{
    internal sealed class EditorContextSnapshotBuilder
    {
        private readonly IEditorService _editorService;
        private readonly EditorContextProjectionService _projectionService;

        public EditorContextSnapshotBuilder(IEditorService editorService, EditorContextProjectionService projectionService)
        {
            _editorService = editorService;
            _projectionService = projectionService;
        }

        public string BuildSurfaceId(string documentPath, EditorSurfaceKind surfaceKind, string paneId)
        {
            return (surfaceKind.ToString() + "|" + (paneId ?? string.Empty) + "|" + (documentPath ?? string.Empty)).ToLowerInvariant();
        }

        public string BuildContextKey(string surfaceId, string documentPath, int documentVersion, int caretIndex, int selectionStart, int selectionEnd, int targetStart, int targetLength, string symbolText)
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

        public EditorContextSnapshot BuildSnapshot(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandTarget target)
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
            var contextKey = BuildContextKey(surfaceId, documentPath, documentVersion, caretIndex, selectionStart, selectionEnd, targetStart, targetLength, clonedTarget.SymbolText);

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
                FocusTokenText = !string.IsNullOrEmpty(clonedTarget.SymbolText) ? clonedTarget.SymbolText : clonedTarget.SelectionText ?? string.Empty,
                HoverKey = (documentPath ?? string.Empty) + "|" + clonedTarget.AbsolutePosition,
                ContainingMemberName = _projectionService.ResolveContainingMemberName(clonedTarget, null),
                CapturedUtc = DateTime.UtcNow,
                Target = _projectionService.BuildBaseTarget(clonedTarget),
                Semantic = _projectionService.BuildSemanticContext(clonedTarget)
            };
        }

        public void EnsureDocumentState(DocumentSession session)
        {
            if (session != null)
            {
                _editorService.EnsureDocumentState(session);
            }
        }
    }
}
