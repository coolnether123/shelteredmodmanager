using System.Collections.Generic;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Inspector.Relationships
{
    internal interface IEditorMethodCallHierarchyMapper
    {
        void AppendRelationships(List<EditorMethodRelationshipItem> items, LanguageServiceCallHierarchyItem[] sourceItems);
    }

    internal sealed class EditorMethodCallHierarchyMapper : IEditorMethodCallHierarchyMapper
    {
        public void AppendRelationships(List<EditorMethodRelationshipItem> items, LanguageServiceCallHierarchyItem[] sourceItems)
        {
            if (items == null || sourceItems == null)
            {
                return;
            }

            for (var i = 0; i < sourceItems.Length; i++)
            {
                var item = sourceItems[i];
                if (item == null)
                {
                    continue;
                }

                EditorMethodRelationshipSet.AddDistinct(items, new EditorMethodRelationshipItem
                {
                    Title = item.SymbolDisplay ?? string.Empty,
                    Detail = item.ContainingTypeName ?? string.Empty,
                    SymbolKind = item.SymbolKind ?? string.Empty,
                    MetadataName = item.MetadataName ?? string.Empty,
                    ContainingTypeName = item.ContainingTypeName ?? string.Empty,
                    ContainingAssemblyName = item.ContainingAssemblyName ?? string.Empty,
                    DocumentationCommentId = item.DocumentationCommentId ?? string.Empty,
                    Relationship = item.Relationship ?? "Call",
                    CallCount = item.CallCount > 0 ? item.CallCount : 1
                });
            }
        }
    }
}
