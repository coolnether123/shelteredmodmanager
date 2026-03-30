using Cortex.Core.Models;

namespace Cortex.Services.Semantics.Requests
{
    internal interface IEditorQuickActionsService
    {
        void OpenQuickActions(CortexShellState state, EditorCommandTarget target, EditorResolvedContextAction[] actions);
        void CloseQuickActions(CortexShellState state);
    }
}
