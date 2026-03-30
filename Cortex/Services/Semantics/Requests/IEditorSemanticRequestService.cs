using Cortex.Core.Models;

namespace Cortex.Services.Semantics.Requests
{
    internal interface IEditorSemanticRequestService
    {
        void QueueRequest(CortexShellState state, EditorCommandTarget target, SemanticRequestKind requestKind);
        void QueueRequest(CortexShellState state, EditorCommandTarget target, SemanticRequestKind requestKind, string newName);
        void QueueDocumentTransformRequest(CortexShellState state, EditorCommandTarget target, string commandId, string title, string applyLabel, bool organizeImports, bool simplifyNames, bool formatDocument);
    }
}
