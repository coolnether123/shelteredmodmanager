using System;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Completion.Augmentation
{
    internal static class CompletionMatchUtility
    {
        public static string GetCandidateText(LanguageServiceCompletionItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(item.FilterText))
            {
                return item.FilterText;
            }

            if (!string.IsNullOrEmpty(item.DisplayText))
            {
                return item.DisplayText;
            }

            return item.InsertText ?? string.Empty;
        }

        public static bool StartsWithWordPart(string candidate, string query)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(query))
            {
                return false;
            }

            for (var i = 1; i < candidate.Length; i++)
            {
                var current = candidate[i];
                var previous = candidate[i - 1];
                if (current == '_')
                {
                    continue;
                }

                if (previous == '_' ||
                    previous == '.' ||
                    previous == ':' ||
                    (char.IsUpper(current) && !char.IsUpper(previous)))
                {
                    var remaining = candidate.Length - i;
                    if (remaining >= query.Length &&
                        string.Compare(candidate, i, query, 0, query.Length, StringComparison.OrdinalIgnoreCase) == 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }
    }
}