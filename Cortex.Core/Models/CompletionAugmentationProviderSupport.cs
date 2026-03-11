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
            AppendMetadata(builder, "Language", request.LanguageId);
            AppendMetadata(builder, "Document", !string.IsNullOrEmpty(request.RelativeDocumentPath) ? request.RelativeDocumentPath : request.DocumentPath);
            AppendMetadata(builder, "Workspace", request.WorkspaceRootPath);
            AppendMetadata(builder, "Trigger", request.ExplicitInvocation ? "explicit" : "automatic");

            if (!string.IsNullOrEmpty(request.SelectedText))
            {
                builder.AppendLine();
                builder.AppendLine("[Selected Text]");
                builder.AppendLine(request.SelectedText);
            }

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
            var text = completion ?? string.Empty;
            var items = string.IsNullOrEmpty(text)
                ? new LanguageServiceCompletionItem[0]
                : new[]
                {
                    new LanguageServiceCompletionItem
                    {
                        DisplayText = BuildDisplayText(request, text),
                        InsertText = text,
                        FilterText = ExtractIdentifierPrefix(request != null ? request.PrefixText : string.Empty) + text,
                        SortText = "0000",
                        InlineDescription = providerDisplayName ?? "AI",
                        Kind = "AI",
                        IsPreselected = true
                    }
                };

            return new LanguageServiceCompletionResponse
            {
                Success = true,
                StatusMessage = items.Length == 0 ? "no suggestions" : "ok",
                DocumentPath = request != null ? request.DocumentPath ?? string.Empty : string.Empty,
                ProjectFilePath = request != null ? request.ProjectFilePath ?? string.Empty : string.Empty,
                DocumentVersion = request != null ? request.DocumentVersion : 0,
                ReplacementRange = new LanguageServiceRange
                {
                    Start = request != null ? Math.Max(0, request.AbsolutePosition) : 0,
                    Length = 0
                },
                Items = items
            };
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
    }
}
