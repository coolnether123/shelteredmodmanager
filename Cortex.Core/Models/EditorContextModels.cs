using System;
using Cortex.LanguageService.Protocol;

namespace Cortex.Core.Models
{
    public enum EditorSurfaceKind
    {
        Unknown = 0,
        Source = 1,
        ReadOnlyCode = 2,
        Decompiled = 3,
        Peek = 4,
        Inspector = 5
    }

    /// <summary>
    /// Immutable-like snapshot of the current editor context for one surface.
    /// Features consume this snapshot instead of holding private symbol/target state.
    /// </summary>
    public sealed class EditorContextSnapshot
    {
        public string ContextKey = string.Empty;
        public string SurfaceId = string.Empty;
        public string PaneId = string.Empty;
        public EditorSurfaceKind SurfaceKind;
        public string DocumentPath = string.Empty;
        public int DocumentVersion;
        public DocumentKind DocumentKind;
        public int CaretIndex = -1;
        public int SelectionAnchorIndex = -1;
        public int SelectionStart = -1;
        public int SelectionEnd = -1;
        public bool HasSelection;
        public string SelectionText = string.Empty;
        public int TargetStart = -1;
        public int TargetLength = -1;
        public string FocusTokenText = string.Empty;
        public string HoverKey = string.Empty;
        public string ContainingMemberName = string.Empty;
        public DateTime CapturedUtc = DateTime.UtcNow;
        public EditorCommandTarget Target;
        public EditorSemanticContext Semantic = new EditorSemanticContext();

        public EditorContextSnapshot Clone()
        {
            return new EditorContextSnapshot
            {
                ContextKey = ContextKey ?? string.Empty,
                SurfaceId = SurfaceId ?? string.Empty,
                PaneId = PaneId ?? string.Empty,
                SurfaceKind = SurfaceKind,
                DocumentPath = DocumentPath ?? string.Empty,
                DocumentVersion = DocumentVersion,
                DocumentKind = DocumentKind,
                CaretIndex = CaretIndex,
                SelectionAnchorIndex = SelectionAnchorIndex,
                SelectionStart = SelectionStart,
                SelectionEnd = SelectionEnd,
                HasSelection = HasSelection,
                SelectionText = SelectionText ?? string.Empty,
                TargetStart = TargetStart,
                TargetLength = TargetLength,
                FocusTokenText = FocusTokenText ?? string.Empty,
                HoverKey = HoverKey ?? string.Empty,
                ContainingMemberName = ContainingMemberName ?? string.Empty,
                CapturedUtc = CapturedUtc,
                Target = Target != null ? Target.Clone() : null,
                Semantic = Semantic != null ? Semantic.Clone() : new EditorSemanticContext()
            };
        }
    }

    public sealed class EditorSemanticContext
    {
        public string QualifiedSymbolDisplay = string.Empty;
        public string SymbolKind = string.Empty;
        public string MetadataName = string.Empty;
        public string ContainingTypeName = string.Empty;
        public string ContainingAssemblyName = string.Empty;
        public string DocumentationCommentId = string.Empty;
        public string HoverText = string.Empty;
        public string DefinitionDocumentPath = string.Empty;
        public int DefinitionLine;
        public int DefinitionColumn;
        public int DefinitionStart = -1;
        public int DefinitionLength = -1;
        public LanguageServiceHoverResponse HoverResponse;

        public EditorSemanticContext Clone()
        {
            return new EditorSemanticContext
            {
                QualifiedSymbolDisplay = QualifiedSymbolDisplay ?? string.Empty,
                SymbolKind = SymbolKind ?? string.Empty,
                MetadataName = MetadataName ?? string.Empty,
                ContainingTypeName = ContainingTypeName ?? string.Empty,
                ContainingAssemblyName = ContainingAssemblyName ?? string.Empty,
                DocumentationCommentId = DocumentationCommentId ?? string.Empty,
                HoverText = HoverText ?? string.Empty,
                DefinitionDocumentPath = DefinitionDocumentPath ?? string.Empty,
                DefinitionLine = DefinitionLine,
                DefinitionColumn = DefinitionColumn,
                DefinitionStart = DefinitionStart,
                DefinitionLength = DefinitionLength,
                HoverResponse = HoverResponse
            };
        }
    }
}
