using System;

namespace Cortex.Core.Services
{
    public sealed class CompletionAugmentationCursorContext
    {
        public string CurrentLinePrefixText;
        public string CurrentLineSuffixText;
        public string CurrentLineIndentation;
    }

    public static class CompletionAugmentationCursorContextBuilder
    {
        public static CompletionAugmentationCursorContext Build(string text, int absolutePosition)
        {
            var value = text ?? string.Empty;
            var caret = Math.Max(0, Math.Min(value.Length, absolutePosition));
            var lineStart = FindLineStart(value, caret);
            var lineEnd = FindLineEnd(value, caret);
            var linePrefix = caret > lineStart ? value.Substring(lineStart, caret - lineStart) : string.Empty;
            var lineSuffix = lineEnd > caret ? value.Substring(caret, lineEnd - caret) : string.Empty;
            return new CompletionAugmentationCursorContext
            {
                CurrentLinePrefixText = linePrefix,
                CurrentLineSuffixText = lineSuffix,
                CurrentLineIndentation = ExtractIndentation(linePrefix)
            };
        }

        private static int FindLineStart(string text, int caret)
        {
            var index = Math.Max(0, Math.Min(text.Length, caret));
            while (index > 0)
            {
                var previous = text[index - 1];
                if (previous == '\r' || previous == '\n')
                {
                    break;
                }

                index--;
            }

            return index;
        }

        private static int FindLineEnd(string text, int caret)
        {
            var index = Math.Max(0, Math.Min(text.Length, caret));
            while (index < text.Length)
            {
                var current = text[index];
                if (current == '\r' || current == '\n')
                {
                    break;
                }

                index++;
            }

            return index;
        }

        private static string ExtractIndentation(string linePrefix)
        {
            var value = linePrefix ?? string.Empty;
            var length = 0;
            while (length < value.Length)
            {
                var current = value[length];
                if (current != ' ' && current != '\t')
                {
                    break;
                }

                length++;
            }

            return length > 0 ? value.Substring(0, length) : string.Empty;
        }
    }
}
