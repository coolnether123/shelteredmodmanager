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

                var definitionLocation = FindPreferredDefinitionLocation(item.Locations);

                EditorMethodRelationshipSet.AddDistinct(items, new EditorMethodRelationshipItem
                {
                    Title = item.SymbolDisplay ?? string.Empty,
                    Detail = item.ContainingTypeName ?? string.Empty,
                    SymbolKind = item.SymbolKind ?? string.Empty,
                    MetadataName = item.MetadataName ?? string.Empty,
                    ContainingTypeName = item.ContainingTypeName ?? string.Empty,
                    ContainingAssemblyName = item.ContainingAssemblyName ?? string.Empty,
                    DocumentationCommentId = item.DocumentationCommentId ?? string.Empty,
                    DefinitionDocumentPath = definitionLocation != null ? definitionLocation.DocumentPath ?? string.Empty : string.Empty,
                    DefinitionRange = definitionLocation != null ? definitionLocation.Range : null,
                    Relationship = item.Relationship ?? "Call",
                    CallCount = item.CallCount > 0 ? item.CallCount : 1
                });
            }
        }

        private static LanguageServiceSymbolLocation FindPreferredDefinitionLocation(LanguageServiceSymbolLocation[] locations)
        {
            if (locations == null || locations.Length == 0)
            {
                return null;
            }

            for (var i = 0; i < locations.Length; i++)
            {
                var location = locations[i];
                if (location != null && location.IsDefinition)
                {
                    return location;
                }
            }

            for (var i = 0; i < locations.Length; i++)
            {
                var location = locations[i];
                if (location != null && location.IsPrimary)
                {
                    return location;
                }
            }

            return locations[0];
        }
    }
}
