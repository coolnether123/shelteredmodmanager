using System;
using Cortex.Core.Models;
using Cortex.Services.Semantics.Completion;

namespace Cortex.Services.Semantics.Completion.Augmentation
{
    internal static class CompletionAugmentationDispatchPolicy
    {
        public static string GetPreQueueReason(CompletionAugmentationRequest request)
        {
            if (request == null || request.ExplicitInvocation)
            {
                return string.Empty;
            }

            if (HasLowSignalTrailingToken(request.CurrentLinePrefixText))
            {
                return "LowSignalContext";
            }

            return string.Empty;
        }

        public static bool ShouldDebounce(CompletionAugmentationRequest request)
        {
            return request != null &&
                !request.ExplicitInvocation &&
                !string.IsNullOrEmpty(request.ProviderId);
        }

        public static DateTime GetDeferredDispatchUtc(CompletionAugmentationRequest request, int debounceMs)
        {
            return ShouldDebounce(request)
                ? DateTime.UtcNow.AddMilliseconds(Math.Max(0, debounceMs))
                : DateTime.UtcNow;
        }

        public static string GetSkipReason(
            CortexCompletionInteractionState editorState,
            DocumentSession session,
            CompletionAugmentationRequest request,
            IEditorCompletionService editorCompletionService)
        {
            if (editorState == null || session == null || request == null || editorCompletionService == null)
            {
                return string.Empty;
            }

            var providerId = request.ProviderId ?? string.Empty;
            if (string.IsNullOrEmpty(providerId))
            {
                return string.Empty;
            }

            if (!string.Equals(editorState.InlineProviderId ?? string.Empty, providerId, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            if (!string.Equals(session.FilePath ?? string.Empty, request.DocumentPath ?? string.Empty, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            string inlineSuffix;
            if (editorCompletionService.TryGetInlineSuggestionSuffix(editorState, session, out inlineSuffix) &&
                !string.IsNullOrEmpty(inlineSuffix))
            {
                return "InlineSuggestionStillMatches";
            }

            return string.Empty;
        }

        private static bool HasLowSignalTrailingToken(string currentLinePrefixText)
        {
            var value = currentLinePrefixText ?? string.Empty;
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }

            var end = value.Length - 1;
            while (end >= 0 && char.IsWhiteSpace(value[end]))
            {
                end--;
            }

            if (end < 0)
            {
                return false;
            }

            var start = end;
            while (start >= 0 && (char.IsLetterOrDigit(value[start]) || value[start] == '_'))
            {
                start--;
            }

            var tokenLength = end - start;
            if (tokenLength <= 0 || tokenLength >= 3)
            {
                return false;
            }

            var precedingIndex = start;
            while (precedingIndex >= 0 && char.IsWhiteSpace(value[precedingIndex]))
            {
                precedingIndex--;
            }

            if (precedingIndex < 0)
            {
                return true;
            }

            switch (value[precedingIndex])
            {
                case '.':
                case ':':
                case '(':
                case '[':
                case ',':
                case '=':
                    return false;
                default:
                    return true;
            }
        }
    }
}
