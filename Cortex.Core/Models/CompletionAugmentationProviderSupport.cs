using System;
using System.Net;
using System.Text;
using Cortex.LanguageService.Protocol;

namespace Cortex.Core.Models
{
    /// <summary>
    /// Shared helpers for provider implementations so prompt shaping and
    /// completion response normalization stay consistent as more providers
    /// are added over time.
    /// </summary>
    public static class CompletionAugmentationProviderSupport
    {
        public static string ResolveInstructionText(string defaultPrompt, CompletionAugmentationRequest request)
        {
            var fallback = defaultPrompt ?? string.Empty;
            var extra = request != null ? request.AdditionalInstructions ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(extra))
            {
                return fallback;
            }

            return request != null && request.ReplaceProviderPrompt
                ? extra
                : string.IsNullOrEmpty(fallback)
                    ? extra
                    : fallback + "\n" + extra;
        }

        public static string BuildCuratedContextBlock(CompletionAugmentationRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.AppendLine("Cortex selected the following completion context. Use only this supplied context and do not assume access to any other workspace files.");
            builder.AppendLine("Prefer existing symbols and APIs that appear below. If the needed symbol is not present in the supplied context, return no completion instead of inventing one.");
            AppendMetadata(builder, "Language", request.LanguageId);
            AppendMetadata(builder, "Document", !string.IsNullOrEmpty(request.RelativeDocumentPath) ? request.RelativeDocumentPath : request.DocumentPath);
            AppendMetadata(builder, "Workspace", request.WorkspaceRootPath);
            AppendMetadata(builder, "Trigger", request.ExplicitInvocation ? "explicit" : "automatic");
            AppendMetadata(builder, "Cursor", request.AbsolutePosition.ToString());
            AppendMetadata(builder, "CurrentLineIndentation", request.CurrentLineIndentation);

            var declarations = request.Declarations;
            if (declarations != null && declarations.Length > 0)
            {
                builder.AppendLine();
                builder.AppendLine("[Declarations]");
                for (var i = 0; i < declarations.Length; i++)
                {
                    var declaration = declarations[i];
                    if (!string.IsNullOrEmpty(declaration))
                    {
                        builder.AppendLine(declaration);
                    }
                }
            }

            if (!string.IsNullOrEmpty(request.SelectedText))
            {
                builder.AppendLine();
                builder.AppendLine("[Selected Text]");
                builder.AppendLine(request.SelectedText);
            }

            AppendSection(builder, "Cursor Prefix Tail", TrimTail(request.PrefixText, 600));
            AppendSection(builder, "Cursor Suffix Head", TrimHead(request.SuffixText, 600));
            AppendSection(builder, "Current Line Prefix", request.CurrentLinePrefixText);
            AppendSection(builder, "Current Line Suffix", request.CurrentLineSuffixText);

            var snippets = request.RelatedSnippets;
            if (snippets != null && snippets.Length > 0)
            {
                for (var i = 0; i < snippets.Length; i++)
                {
                    var snippet = snippets[i];
                    if (snippet == null || string.IsNullOrEmpty(snippet.Content))
                    {
                        continue;
                    }

                    builder.AppendLine();
                    builder.Append("[Related Snippet ");
                    builder.Append(i + 1);
                    builder.Append("]");
                    if (!string.IsNullOrEmpty(snippet.RelativePath))
                    {
                        builder.Append(" ");
                        builder.Append(snippet.RelativePath);
                    }
                    else if (!string.IsNullOrEmpty(snippet.DisplayName))
                    {
                        builder.Append(" ");
                        builder.Append(snippet.DisplayName);
                    }

                    builder.AppendLine();
                    builder.AppendLine(snippet.Content);
                }
            }

            return builder.ToString().Trim();
        }

        public static LanguageServiceCompletionResponse BuildSuccessResponse(
            CompletionAugmentationRequest request,
            string completion,
            string providerDisplayName)
        {
            return BuildSuccessResponse(
                request,
                string.IsNullOrEmpty(completion) ? new string[0] : new[] { completion },
                providerDisplayName);
        }

        public static LanguageServiceCompletionResponse BuildSuccessResponse(
            CompletionAugmentationRequest request,
            string[] completions,
            string providerDisplayName)
        {
            var values = completions ?? new string[0];
            var items = new System.Collections.Generic.List<LanguageServiceCompletionItem>();
            var query = ExtractIdentifierPrefix(request != null ? request.PrefixText : string.Empty);
            for (var i = 0; i < values.Length; i++)
            {
                var text = NormalizeCompletionText(request, values[i]);
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                items.Add(new LanguageServiceCompletionItem
                {
                    DisplayText = BuildDisplayText(request, text),
                    InsertText = text,
                    FilterText = query + text,
                    SortText = i.ToString("D4"),
                    InlineDescription = providerDisplayName ?? "AI",
                    Kind = "AI",
                    IsPreselected = i == 0
                });
            }

            return new LanguageServiceCompletionResponse
            {
                Success = true,
                StatusMessage = items.Count == 0 ? "no suggestions" : "ok",
                DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                DocumentVersion = request != null ? request.DocumentVersion : 0,
                ReplacementRange = new LanguageServiceRange
                {
                    Start = request != null ? Math.Max(0, request.AbsolutePosition) : 0,
                    Length = 0
                },
                Items = items.ToArray()
            };
        }

        public static string NormalizeCompletionText(CompletionAugmentationRequest request, string completion)
        {
            var normalized = completion ?? string.Empty;
            if (string.IsNullOrEmpty(normalized))
            {
                return string.Empty;
            }

            normalized = StripPrefixOverlap(request != null ? request.PrefixText : string.Empty, normalized);
            normalized = StripPrefixOverlap(request != null ? request.CurrentLinePrefixText : string.Empty, normalized);
            normalized = StripRepeatedRecentLines(request != null ? request.PrefixText : string.Empty, normalized);
            normalized = StripPrefixOverlap(request != null ? request.PrefixText : string.Empty, normalized);
            normalized = StripPrefixOverlap(request != null ? request.CurrentLinePrefixText : string.Empty, normalized);
            return normalized;
        }

        private static string StripPrefixOverlap(string prefixText, string completion)
        {
            if (string.IsNullOrEmpty(prefixText) || string.IsNullOrEmpty(completion))
            {
                return completion ?? string.Empty;
            }

            var maxOverlap = Math.Min(prefixText.Length, completion.Length);
            for (var overlap = maxOverlap; overlap > 0; overlap--)
            {
                if (string.CompareOrdinal(prefixText, prefixText.Length - overlap, completion, 0, overlap) == 0)
                {
                    return completion.Substring(overlap);
                }
            }

            return completion;
        }

        private static string StripRepeatedRecentLines(string prefixText, string completion)
        {
            if (string.IsNullOrEmpty(prefixText) || string.IsNullOrEmpty(completion))
            {
                return completion ?? string.Empty;
            }

            var recentLines = GetRecentNonEmptyLines(prefixText, 6);
            var value = completion;
            for (var i = 0; i < 2; i++)
            {
                var lineBreakIndex = FindFirstLineBreak(value);
                if (lineBreakIndex < 0)
                {
                    break;
                }

                var firstLine = value.Substring(0, lineBreakIndex).TrimEnd();
                if (string.IsNullOrEmpty(firstLine) || !ContainsLine(recentLines, firstLine))
                {
                    break;
                }

                var nextIndex = lineBreakIndex;
                while (nextIndex < value.Length && (value[nextIndex] == '\r' || value[nextIndex] == '\n'))
                {
                    nextIndex++;
                }

                value = nextIndex < value.Length ? value.Substring(nextIndex) : string.Empty;
            }

            return value;
        }

        private static string[] GetRecentNonEmptyLines(string text, int limit)
        {
            if (string.IsNullOrEmpty(text) || limit <= 0)
            {
                return new string[0];
            }

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var results = new System.Collections.Generic.List<string>();
            for (var i = lines.Length - 1; i >= 0 && results.Count < limit; i--)
            {
                var trimmed = (lines[i] ?? string.Empty).TrimEnd();
                if (!string.IsNullOrEmpty(trimmed))
                {
                    results.Add(trimmed);
                }
            }

            return results.ToArray();
        }

        private static bool ContainsLine(string[] lines, string candidate)
        {
            if (lines == null || string.IsNullOrEmpty(candidate))
            {
                return false;
            }

            for (var i = 0; i < lines.Length; i++)
            {
                if (string.Equals(lines[i] ?? string.Empty, candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static int FindFirstLineBreak(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return -1;
            }

            for (var i = 0; i < value.Length; i++)
            {
                if (value[i] == '\r' || value[i] == '\n')
                {
                    return i;
                }
            }

            return -1;
        }

        public static LanguageServiceCompletionResponse BuildErrorResponse(CompletionAugmentationRequest request, string message)
        {
            return new LanguageServiceCompletionResponse
            {
                Success = false,
                StatusMessage = message ?? string.Empty,
                DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                DocumentVersion = request != null ? request.DocumentVersion : 0,
                ReplacementRange = new LanguageServiceRange
                {
                    Start = request != null ? Math.Max(0, request.AbsolutePosition) : 0,
                    Length = 0
                },
                Items = new LanguageServiceCompletionItem[0]
            };
        }

        public static string ExtractIdentifierPrefix(string prefixText)
        {
            if (string.IsNullOrEmpty(prefixText))
            {
                return string.Empty;
            }

            var start = prefixText.Length;
            while (start > 0)
            {
                var value = prefixText[start - 1];
                if (!(char.IsLetterOrDigit(value) || value == '_'))
                {
                    break;
                }

                start--;
            }

            return start < prefixText.Length ? prefixText.Substring(start, prefixText.Length - start) : string.Empty;
        }

        public static string ReadWebException(string fallbackMessage, WebException ex)
        {
            if (ex == null)
            {
                return fallbackMessage ?? "Request failed.";
            }

            try
            {
                var response = ex.Response as HttpWebResponse;
                if (response == null)
                {
                    return ex.Message ?? fallbackMessage ?? "Request failed.";
                }

                using (var stream = response.GetResponseStream())
                using (var reader = new System.IO.StreamReader(stream ?? System.IO.Stream.Null))
                {
                    var body = reader.ReadToEnd();
                    return !string.IsNullOrEmpty(body)
                        ? (int)response.StatusCode + " " + response.StatusDescription + ": " + body
                        : (int)response.StatusCode + " " + response.StatusDescription;
                }
            }
            catch
            {
                return ex.Message ?? fallbackMessage ?? "Request failed.";
            }
        }

        public static string BuildDisplayText(CompletionAugmentationRequest request, string completion)
        {
            var prefix = ExtractIdentifierPrefix(request != null ? request.PrefixText : string.Empty);
            var combined = prefix + (completion ?? string.Empty);
            return combined.Length <= 80 ? combined : combined.Substring(0, 77) + "...";
        }

        private static void AppendMetadata(StringBuilder builder, string label, string value)
        {
            if (builder == null || string.IsNullOrEmpty(label) || string.IsNullOrEmpty(value))
            {
                return;
            }

            builder.Append(label);
            builder.Append(": ");
            builder.AppendLine(value);
        }

        private static void AppendSection(StringBuilder builder, string label, string value)
        {
            if (builder == null || string.IsNullOrEmpty(label) || string.IsNullOrEmpty(value))
            {
                return;
            }

            builder.AppendLine();
            builder.Append('[');
            builder.Append(label);
            builder.AppendLine("]");
            builder.AppendLine(value);
        }

        private static string TrimTail(string value, int maxCharacters)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
            {
                return value ?? string.Empty;
            }

            return value.Substring(value.Length - maxCharacters, maxCharacters);
        }

        private static string TrimHead(string value, int maxCharacters)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= maxCharacters)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, maxCharacters);
        }
    }
}
