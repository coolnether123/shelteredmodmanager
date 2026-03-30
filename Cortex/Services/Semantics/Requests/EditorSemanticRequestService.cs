using System;
using Cortex.Core.Models;

namespace Cortex.Services.Semantics.Requests
{
    internal sealed class EditorSemanticRequestService : IEditorSemanticRequestService
    {
        public void QueueRequest(CortexShellState state, EditorCommandTarget target, SemanticRequestKind requestKind)
        {
            QueueRequest(state, target, requestKind, string.Empty);
        }

        public void QueueRequest(CortexShellState state, EditorCommandTarget target, SemanticRequestKind requestKind, string newName)
        {
            if (state == null || state.Semantic == null || target == null)
            {
                return;
            }

            state.Semantic.Request.RequestedKey = (target.DocumentPath ?? string.Empty) + "|" + target.AbsolutePosition + "|" + requestKind + "|" + DateTime.UtcNow.Ticks;
            state.Semantic.Request.RequestedContextKey = target.ContextKey ?? string.Empty;
            state.Semantic.Request.RequestedKind = requestKind;
            state.Semantic.Request.RequestedDocumentPath = target.DocumentPath ?? string.Empty;
            state.Semantic.Request.RequestedLine = target.Line;
            state.Semantic.Request.RequestedColumn = target.Column;
            state.Semantic.Request.RequestedAbsolutePosition = target.AbsolutePosition;
            state.Semantic.Request.RequestedSymbolText = target.SymbolText ?? string.Empty;
            state.Semantic.Request.RequestedNewName = newName ?? string.Empty;
            state.Semantic.Request.RequestedCommandId = string.Empty;
            state.Semantic.Request.RequestedTitle = string.Empty;
            state.Semantic.Request.RequestedApplyLabel = string.Empty;
            state.Semantic.Request.RequestedOrganizeImports = false;
            state.Semantic.Request.RequestedSimplifyNames = false;
            state.Semantic.Request.RequestedFormatDocument = false;
        }

        public void QueueDocumentTransformRequest(CortexShellState state, EditorCommandTarget target, string commandId, string title, string applyLabel, bool organizeImports, bool simplifyNames, bool formatDocument)
        {
            if (state == null || state.Semantic == null || target == null)
            {
                return;
            }

            state.Semantic.Request.RequestedKey = (target.DocumentPath ?? string.Empty) + "|" + target.AbsolutePosition + "|" + SemanticRequestKind.DocumentTransformPreview + "|" + DateTime.UtcNow.Ticks;
            state.Semantic.Request.RequestedContextKey = target.ContextKey ?? string.Empty;
            state.Semantic.Request.RequestedKind = SemanticRequestKind.DocumentTransformPreview;
            state.Semantic.Request.RequestedDocumentPath = target.DocumentPath ?? string.Empty;
            state.Semantic.Request.RequestedLine = target.Line;
            state.Semantic.Request.RequestedColumn = target.Column;
            state.Semantic.Request.RequestedAbsolutePosition = target.AbsolutePosition;
            state.Semantic.Request.RequestedSymbolText = target.SymbolText ?? string.Empty;
            state.Semantic.Request.RequestedNewName = string.Empty;
            state.Semantic.Request.RequestedCommandId = commandId ?? string.Empty;
            state.Semantic.Request.RequestedTitle = title ?? string.Empty;
            state.Semantic.Request.RequestedApplyLabel = applyLabel ?? string.Empty;
            state.Semantic.Request.RequestedOrganizeImports = organizeImports;
            state.Semantic.Request.RequestedSimplifyNames = simplifyNames;
            state.Semantic.Request.RequestedFormatDocument = formatDocument;
        }
    }
}
