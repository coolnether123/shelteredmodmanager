using System.Text;
using Microsoft.CodeAnalysis.Completion;

namespace Cortex.Roslyn.Worker
{
    internal static class CompletionInsertTextBuilder
    {
        public static string BuildFastInsertText(CompletionItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            if (!string.IsNullOrEmpty(item.DisplayTextPrefix))
            {
                builder.Append(item.DisplayTextPrefix);
            }

            if (!string.IsNullOrEmpty(item.DisplayText))
            {
                builder.Append(item.DisplayText);
            }

            if (!string.IsNullOrEmpty(item.DisplayTextSuffix))
            {
                builder.Append(item.DisplayTextSuffix);
            }

            if (builder.Length > 0)
            {
                return builder.ToString();
            }

            if (!string.IsNullOrEmpty(item.FilterText))
            {
                return item.FilterText;
            }

            return item.DisplayText ?? string.Empty;
        }
    }
}
