using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class TextSearchService : ITextSearchService
    {
        public TextSearchResultSet Search(TextSearchQuery query, IList<TextSearchDocumentInput> documents)
        {
            var resultSet = new TextSearchResultSet();
            resultSet.Query = CloneQuery(query);
            resultSet.GeneratedUtc = DateTime.UtcNow;

            if (query == null || string.IsNullOrEmpty(query.SearchText))
            {
                resultSet.StatusMessage = "Enter text to search.";
                return resultSet;
            }

            if (documents == null || documents.Count == 0)
            {
                resultSet.StatusMessage = "No documents were available for this scope.";
                return resultSet;
            }

            for (var i = 0; i < documents.Count; i++)
            {
                var input = documents[i];
                if (input == null || string.IsNullOrEmpty(input.DocumentPath))
                {
                    continue;
                }

                resultSet.SearchedDocumentCount++;
                var documentResult = SearchDocument(query, input);
                if (documentResult == null || documentResult.Matches.Count == 0)
                {
                    continue;
                }

                resultSet.Documents.Add(documentResult);
                resultSet.TotalMatchCount += documentResult.Matches.Count;
            }

            resultSet.StatusMessage = resultSet.TotalMatchCount > 0
                ? "Found " + resultSet.TotalMatchCount + " match(es) in " + resultSet.Documents.Count + " file(s)."
                : "No matches found.";
            return resultSet;
        }

        private static TextSearchDocumentResult SearchDocument(TextSearchQuery query, TextSearchDocumentInput input)
        {
            var text = input.Text ?? string.Empty;
            if (text.Length == 0)
            {
                return null;
            }

            var comparison = query.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            var searchText = query.SearchText ?? string.Empty;
            var documentResult = new TextSearchDocumentResult
            {
                DocumentPath = input.DocumentPath ?? string.Empty,
                DisplayPath = !string.IsNullOrEmpty(input.DisplayPath) ? input.DisplayPath : input.DocumentPath ?? string.Empty
            };

            var index = 0;
            while (index <= text.Length - searchText.Length)
            {
                var matchIndex = text.IndexOf(searchText, index, comparison);
                if (matchIndex < 0)
                {
                    break;
                }

                if (!query.WholeWord || IsWholeWordMatch(text, matchIndex, searchText.Length))
                {
                    documentResult.Matches.Add(BuildMatch(input, text, matchIndex, searchText.Length));
                }

                index = matchIndex + Math.Max(1, searchText.Length);
            }

            return documentResult.Matches.Count > 0 ? documentResult : null;
        }

        private static TextSearchMatch BuildMatch(TextSearchDocumentInput input, string text, int absoluteIndex, int length)
        {
            var lineStart = FindLineStart(text, absoluteIndex);
            var lineEnd = FindLineEnd(text, absoluteIndex);
            var lineText = lineEnd > lineStart ? text.Substring(lineStart, lineEnd - lineStart) : string.Empty;
            var lineNumber = CountLinesBefore(text, lineStart) + 1;
            var columnNumber = absoluteIndex - lineStart + 1;

            return new TextSearchMatch
            {
                DocumentPath = input.DocumentPath ?? string.Empty,
                DisplayPath = !string.IsNullOrEmpty(input.DisplayPath) ? input.DisplayPath : input.DocumentPath ?? string.Empty,
                AbsoluteIndex = absoluteIndex,
                Length = length,
                LineNumber = lineNumber,
                ColumnNumber = columnNumber,
                LineText = lineText,
                PreviewText = BuildPreview(lineText, columnNumber - 1, length)
            };
        }

        private static TextSearchQuery CloneQuery(TextSearchQuery query)
        {
            return query != null
                ? new TextSearchQuery
                {
                    SearchText = query.SearchText ?? string.Empty,
                    Scope = query.Scope,
                    MatchCase = query.MatchCase,
                    WholeWord = query.WholeWord
                }
                : new TextSearchQuery();
        }

        private static bool IsWholeWordMatch(string text, int index, int length)
        {
            var leftOk = index <= 0 || !IsWordCharacter(text[index - 1]);
            var rightIndex = index + length;
            var rightOk = rightIndex >= text.Length || !IsWordCharacter(text[rightIndex]);
            return leftOk && rightOk;
        }

        private static bool IsWordCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static int FindLineStart(string text, int index)
        {
            var safeIndex = Math.Max(0, Math.Min(index, text.Length));
            while (safeIndex > 0)
            {
                var candidate = text[safeIndex - 1];
                if (candidate == '\r' || candidate == '\n')
                {
                    break;
                }

                safeIndex--;
            }

            return safeIndex;
        }

        private static int FindLineEnd(string text, int index)
        {
            var safeIndex = Math.Max(0, Math.Min(index, text.Length));
            while (safeIndex < text.Length)
            {
                var candidate = text[safeIndex];
                if (candidate == '\r' || candidate == '\n')
                {
                    break;
                }

                safeIndex++;
            }

            return safeIndex;
        }

        private static int CountLinesBefore(string text, int lineStart)
        {
            var count = 0;
            for (var i = 0; i < lineStart && i < text.Length; i++)
            {
                if (text[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }

        private static string BuildPreview(string lineText, int columnIndex, int length)
        {
            if (string.IsNullOrEmpty(lineText))
            {
                return string.Empty;
            }

            var safeColumn = Math.Max(0, Math.Min(columnIndex, Math.Max(0, lineText.Length - 1)));
            var prefixStart = Math.Max(0, safeColumn - 28);
            var suffixEnd = Math.Min(lineText.Length, safeColumn + length + 28);
            var snippet = lineText.Substring(prefixStart, suffixEnd - prefixStart).Trim();
            if (prefixStart > 0)
            {
                snippet = "..." + snippet;
            }

            if (suffixEnd < lineText.Length)
            {
                snippet += "...";
            }

            return snippet;
        }
    }
}
