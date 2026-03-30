using System;
using Cortex.Core.Models;

namespace Cortex.Services.Semantics.Context
{
    internal sealed class EditorContextProjectionService
    {
        public EditorCommandTarget BuildBaseTarget(EditorCommandTarget source)
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

        public EditorSemanticContext BuildSemanticContext(EditorCommandTarget source)
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

        public EditorCommandTarget ProjectTarget(EditorContextSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Target == null)
            {
                return null;
            }

            var projected = snapshot.Target.Clone();
            CopySemanticFields(projected, snapshot.Semantic);
            return projected;
        }

        public void CopySemanticFields(EditorCommandTarget target, EditorSemanticContext semantic)
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

        public string ResolveContainingMemberName(EditorCommandTarget target, EditorSemanticContext semantic)
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
    }
}
