using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Cortex.Services.Editor.Context;

namespace Cortex.Services.Semantics.Context
{
    internal sealed class EditorContextSemanticMutationService
    {
        private readonly EditorSymbolInteractionService _symbolInteractionService;
        private readonly EditorContextProjectionService _projectionService;
        private readonly EditorContextStoreService _storeService;

        public EditorContextSemanticMutationService(EditorSymbolInteractionService symbolInteractionService, EditorContextProjectionService projectionService, EditorContextStoreService storeService)
        {
            _symbolInteractionService = symbolInteractionService;
            _projectionService = projectionService;
            _storeService = storeService;
        }

        public string ApplyHoverResponse(CortexShellState state, string contextKey, string hoverKey, LanguageServiceHoverResponse response)
        {
            var snapshot = _storeService.ResolveHoverMutationSnapshot(state, contextKey, hoverKey);
            if (snapshot == null)
            {
                return string.Empty;
            }

            if (snapshot.Semantic == null)
            {
                snapshot.Semantic = new EditorSemanticContext();
            }

            snapshot.HoverKey = hoverKey ?? snapshot.HoverKey ?? string.Empty;
            snapshot.Semantic.HoverResponse = response;
            snapshot.Semantic.HoverContent = null;
            var projected = _projectionService.ProjectTarget(snapshot);
            _symbolInteractionService.ApplyHoverMetadata(projected, response);
            _projectionService.CopySemanticFields(projected, snapshot.Semantic);
            snapshot.FocusTokenText = !string.IsNullOrEmpty(projected.SymbolText) ? projected.SymbolText : snapshot.FocusTokenText ?? string.Empty;
            snapshot.TargetStart = projected.AbsolutePosition;
            snapshot.TargetLength = !string.IsNullOrEmpty(projected.SymbolText) ? projected.SymbolText.Length : snapshot.TargetLength;
            snapshot.ContainingMemberName = _projectionService.ResolveContainingMemberName(projected, snapshot.Semantic);
            return snapshot.ContextKey ?? string.Empty;
        }

        public void ApplyHoverContent(CortexShellState state, string contextKey, string hoverKey, EditorResolvedHoverContent content)
        {
            var snapshot = _storeService.GetContext(state, contextKey);
            if (snapshot == null)
            {
                return;
            }

            if (snapshot.Semantic == null)
            {
                snapshot.Semantic = new EditorSemanticContext();
            }

            snapshot.HoverKey = hoverKey ?? snapshot.HoverKey ?? string.Empty;
            snapshot.Semantic.HoverContent = content != null ? content.Clone() : null;
        }

        public void ApplySymbolContext(CortexShellState state, string contextKey, LanguageServiceSymbolContextResponse response)
        {
            var snapshot = _storeService.GetContext(state, contextKey);
            if (snapshot == null || response == null)
            {
                return;
            }

            if (snapshot.Semantic == null)
            {
                snapshot.Semantic = new EditorSemanticContext();
            }

            if (!string.IsNullOrEmpty(response.QualifiedSymbolDisplay)) { snapshot.Semantic.QualifiedSymbolDisplay = response.QualifiedSymbolDisplay; }
            if (!string.IsNullOrEmpty(response.SymbolKind)) { snapshot.Semantic.SymbolKind = response.SymbolKind; }
            if (!string.IsNullOrEmpty(response.MetadataName)) { snapshot.Semantic.MetadataName = response.MetadataName; }
            if (!string.IsNullOrEmpty(response.ContainingTypeName)) { snapshot.Semantic.ContainingTypeName = response.ContainingTypeName; }
            if (!string.IsNullOrEmpty(response.ContainingAssemblyName)) { snapshot.Semantic.ContainingAssemblyName = response.ContainingAssemblyName; }
            if (!string.IsNullOrEmpty(response.DocumentationCommentId)) { snapshot.Semantic.DocumentationCommentId = response.DocumentationCommentId; }
            if (!string.IsNullOrEmpty(response.DefinitionDocumentPath)) { snapshot.Semantic.DefinitionDocumentPath = response.DefinitionDocumentPath; }
            if (response.DefinitionRange != null)
            {
                snapshot.Semantic.DefinitionStart = response.DefinitionRange.Start;
                snapshot.Semantic.DefinitionLength = response.DefinitionRange.Length;
                snapshot.Semantic.DefinitionLine = response.DefinitionRange.StartLine;
                snapshot.Semantic.DefinitionColumn = response.DefinitionRange.StartColumn;
            }

            snapshot.ContainingMemberName = _projectionService.ResolveContainingMemberName(_projectionService.ProjectTarget(snapshot), snapshot.Semantic);
        }

        public void ClearHoverResponse(CortexShellState state, string contextKey)
        {
            var snapshot = _storeService.GetContext(state, contextKey);
            if (snapshot == null || snapshot.Semantic == null)
            {
                return;
            }

            snapshot.Semantic.HoverResponse = null;
            snapshot.Semantic.HoverContent = null;
        }

        public void PublishHoveredContext(CortexShellState state, string contextKey, string definitionDocumentPath)
        {
            if (state == null || state.EditorContext == null)
            {
                return;
            }

            state.EditorContext.HoveredContextKey = contextKey ?? string.Empty;
            state.EditorContext.HoveredDefinitionDocumentPath = definitionDocumentPath ?? string.Empty;
        }

        public void ClearHoveredContext(CortexShellState state)
        {
            if (state == null || state.EditorContext == null)
            {
                return;
            }

            state.EditorContext.HoveredContextKey = string.Empty;
            state.EditorContext.HoveredDefinitionDocumentPath = string.Empty;
        }
    }
}
