using System;
using System.Collections.Generic;

namespace Cortex.Services.Inspector.Relationships
{
    internal static class EditorMethodRelationshipSet
    {
        public static void AddDistinct(List<EditorMethodRelationshipItem> items, EditorMethodRelationshipItem candidate)
        {
            if (items == null || candidate == null)
            {
                return;
            }

            var candidateKey = BuildKey(candidate);
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(BuildKey(items[i]), candidateKey, StringComparison.Ordinal))
                {
                    return;
                }
            }

            items.Add(candidate);
        }

        private static string BuildKey(EditorMethodRelationshipItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return (item.Relationship ?? string.Empty) + "|" +
                (item.DocumentationCommentId ?? string.Empty) + "|" +
                (item.ContainingAssemblyName ?? string.Empty) + "|" +
                (item.MetadataName ?? string.Empty) + "|" +
                (item.ContainingTypeName ?? string.Empty);
        }
    }
}
