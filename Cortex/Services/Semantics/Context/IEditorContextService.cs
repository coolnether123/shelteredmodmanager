using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Context
{
    internal interface IEditorContextService
    {
        string BuildSurfaceId(string documentPath, EditorSurfaceKind surfaceKind, string paneId);
        EditorContextSnapshot PublishDocumentContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, bool editingEnabled, int absolutePosition);
        EditorContextSnapshot PublishInvocationContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandInvocation invocation, bool setActive);
        EditorContextSnapshot PublishTargetContext(CortexShellState state, DocumentSession session, string surfaceId, string paneId, EditorSurfaceKind surfaceKind, EditorCommandTarget target, bool setActive);
        EditorContextSnapshot GetActiveContext(CortexShellState state);
        EditorContextSnapshot GetHoveredContext(CortexShellState state);
        EditorContextSnapshot GetContext(CortexShellState state, string contextKey);
        EditorContextSnapshot GetSurfaceContext(CortexShellState state, string surfaceId);
        EditorCommandTarget ResolveTarget(CortexShellState state, string contextKey);
        EditorCommandInvocation ResolveInvocation(CortexShellState state, string contextKey);
        LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string contextKey, string hoverKey);
        LanguageServiceHoverResponse ResolveHoverResponse(CortexShellState state, string hoverKey);
        EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string contextKey, string hoverKey);
        EditorResolvedHoverContent ResolveHoverContent(CortexShellState state, string hoverKey);
        string ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, LanguageServiceHoverResponse response);
        void ApplyHoverContent(CortexShellState state, string contextKey, string hoverKey, EditorResolvedHoverContent content);
        void ApplySymbolContext(CortexShellState state, string contextKey, LanguageServiceSymbolContextResponse response);
        void ClearHoverResponse(CortexShellState state, string contextKey);
        void PublishHoveredContext(CortexShellState state, string contextKey, string definitionDocumentPath);
        void ClearHoveredContext(CortexShellState state);
        string BuildContextKey(string surfaceId, string documentPath, int documentVersion, int caretIndex, int selectionStart, int selectionEnd, int targetStart, int targetLength, string symbolText);
    }
}
