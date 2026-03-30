using Cortex.Core.Models;

namespace Cortex.Services.Semantics.Requests
{
    internal sealed class EditorQuickActionsService : IEditorQuickActionsService
    {
        public void OpenQuickActions(CortexShellState state, EditorCommandTarget target, EditorResolvedContextAction[] actions)
        {
            if (state == null || state.Semantic == null)
            {
                return;
            }

            state.Semantic.QuickActions.Visible = true;
            state.Semantic.QuickActions.Title = target != null && !string.IsNullOrEmpty(target.SymbolText)
                ? target.SymbolText
                : "Quick Actions";
            state.Semantic.QuickActions.FilterText = string.Empty;
            state.Semantic.QuickActions.SelectedIndex = actions != null && actions.Length > 0 ? 0 : -1;
            state.Semantic.QuickActions.ContextKey = target != null ? target.ContextKey ?? string.Empty : string.Empty;
            state.Semantic.QuickActions.Actions = actions ?? new EditorResolvedContextAction[0];
        }

        public void CloseQuickActions(CortexShellState state)
        {
            if (state == null || state.Semantic == null)
            {
                return;
            }

            state.Semantic.QuickActions.Visible = false;
            state.Semantic.QuickActions.Title = string.Empty;
            state.Semantic.QuickActions.FilterText = string.Empty;
            state.Semantic.QuickActions.SelectedIndex = -1;
            state.Semantic.QuickActions.ContextKey = string.Empty;
            state.Semantic.QuickActions.Actions = new EditorResolvedContextAction[0];
        }
    }
}
