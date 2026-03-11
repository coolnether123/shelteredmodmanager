using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class CompletionRankingService
    {
        public string GetQuery(DocumentSession session, LanguageServiceCompletionResponse response)
        {
            return BuildContext(session, response).Query;
        }

        public LanguageServiceCompletionResponse Rank(DocumentSession session, LanguageServiceCompletionResponse response)
        {
            if (session == null || response == null || response.Items == null || response.Items.Length <= 1)
            {
                return response;
            }

            var rankingContext = BuildContext(session, response);
            var ranked = new RankedCompletionItem[response.Items.Length];
            for (var i = 0; i < response.Items.Length; i++)
            {
                var match = EvaluateQueryMatch(response.Items[i], rankingContext);
                ranked[i] = new RankedCompletionItem
                {
                    Item = response.Items[i],
                    Score = ScoreItem(response.Items[i], rankingContext, match),
                    MatchTier = match.MatchTier,
                    OriginalIndex = i
                };
            }

            Array.Sort(ranked, CompareRankedItems);
            ranked = FilterWeakMatches(ranked, rankingContext);
            var reordered = new LanguageServiceCompletionItem[ranked.Length];
            for (var i = 0; i < ranked.Length; i++)
            {
                reordered[i] = ranked[i].Item;
            }

            response.Items = reordered;
            return response;
        }

        private static CompletionRankingContext BuildContext(DocumentSession session, LanguageServiceCompletionResponse response)
        {
            var text = session.Text ?? string.Empty;
            var caretIndex = session.EditorState != null ? session.EditorState.CaretIndex : text.Length;
            caretIndex = Math.Max(0, Math.Min(text.Length, caretIndex));

            var replacementRange = response.ReplacementRange;
            var replacementStart = replacementRange != null ? Math.Max(0, Math.Min(text.Length, replacementRange.Start)) : caretIndex;
            if (replacementStart > caretIndex)
            {
                replacementStart = caretIndex;
            }

            var identifierStart = FindIdentifierStart(text, caretIndex);
            var queryStart = Math.Min(identifierStart, replacementStart);
            if (queryStart > caretIndex)
            {
                queryStart = caretIndex;
            }

            var query = caretIndex > queryStart
                ? text.Substring(queryStart, caretIndex - queryStart)
                : string.Empty;
            var queryTrimmed = query.Trim();
            var previousToken = FindPreviousToken(text, queryStart);
            var previousSymbol = FindPreviousSymbol(text, queryStart);

            return new CompletionRankingContext
            {
                Query = queryTrimmed,
                LowerQuery = queryTrimmed.ToLowerInvariant(),
                PreviousToken = previousToken,
                LowerPreviousToken = previousToken.ToLowerInvariant(),
                PreviousSymbol = previousSymbol,
                IsMemberAccess = previousSymbol == '.',
                IsAfterUsing = string.Equals(previousToken, "using", StringComparison.OrdinalIgnoreCase),
                IsAfterNew = string.Equals(previousToken, "new", StringComparison.OrdinalIgnoreCase),
                IsAfterReturn = string.Equals(previousToken, "return", StringComparison.OrdinalIgnoreCase),
                IsAfterOverride = string.Equals(previousToken, "override", StringComparison.OrdinalIgnoreCase),
                IsDeclarationContext = IsDeclarationContext(text, queryStart, previousToken)
            };
        }

        private static long ScoreItem(LanguageServiceCompletionItem item, CompletionRankingContext context, QueryMatchResult match)
        {
            if (item == null)
            {
                return long.MinValue;
            }

            var candidate = !string.IsNullOrEmpty(item.FilterText)
                ? item.FilterText
                : (!string.IsNullOrEmpty(item.DisplayText) ? item.DisplayText : item.InsertText ?? string.Empty);
            var lowerCandidate = candidate.ToLowerInvariant();
            var score = 0L;

            if (item.IsPreselected)
            {
                score += 1500;
            }

            score += GetKindBoost(item.Kind, context);
            score += GetKeywordBoost(lowerCandidate, context);

            if (!string.IsNullOrEmpty(context.Query))
            {
                score += match.Score;
            }
            else
            {
                score += ScoreNoQueryContext(item, lowerCandidate, context);
            }

            if (!string.IsNullOrEmpty(item.SortText))
            {
                score -= Math.Min(200, item.SortText.Length);
            }

            score -= Math.Min(120, candidate.Length);
            return score;
        }

        private static QueryMatchResult EvaluateQueryMatch(LanguageServiceCompletionItem item, CompletionRankingContext context)
        {
            if (item == null || string.IsNullOrEmpty(context.Query))
            {
                return new QueryMatchResult();
            }

            var candidate = !string.IsNullOrEmpty(item.FilterText)
                ? item.FilterText
                : (!string.IsNullOrEmpty(item.DisplayText) ? item.DisplayText : item.InsertText ?? string.Empty);
            var lowerCandidate = candidate.ToLowerInvariant();
            var query = context.Query;
            var lowerQuery = context.LowerQuery;

            if (string.Equals(candidate, query, StringComparison.Ordinal))
            {
                return new QueryMatchResult(8, 220000);
            }

            if (string.Equals(lowerCandidate, lowerQuery, StringComparison.Ordinal))
            {
                return new QueryMatchResult(7, 210000);
            }

            if (candidate.StartsWith(query, StringComparison.Ordinal))
            {
                return new QueryMatchResult(6, 200000 - Math.Max(0, candidate.Length - query.Length));
            }

            if (candidate.StartsWith(query, StringComparison.OrdinalIgnoreCase))
            {
                return new QueryMatchResult(5, 190000 - Math.Max(0, candidate.Length - query.Length));
            }

            if (StartsWithWordPart(candidate, query))
            {
                return new QueryMatchResult(4, 170000 - Math.Max(0, candidate.Length - query.Length));
            }

            if (CamelCaseStartsWith(candidate, query))
            {
                return new QueryMatchResult(3, 160000 - Math.Max(0, candidate.Length - query.Length));
            }

            var containsIndex = lowerCandidate.IndexOf(lowerQuery, StringComparison.Ordinal);
            if (containsIndex >= 0)
            {
                return new QueryMatchResult(2, 120000 - (containsIndex * 100) - Math.Max(0, candidate.Length - query.Length));
            }

            if (IsSubsequence(lowerQuery, lowerCandidate))
            {
                var penalty = SubsequenceGapPenalty(lowerQuery, lowerCandidate);
                var baseScore = 70000 - (penalty * 90) - Math.Max(0, candidate.Length - query.Length);
                if (query.Length >= 4 && penalty > query.Length)
                {
                    baseScore -= 50000;
                }

                return new QueryMatchResult(1, baseScore);
            }

            var distance = ComputePrefixDistance(lowerCandidate, lowerQuery);
            if (query.Length <= 1)
            {
                return new QueryMatchResult(0, 2000 - (distance * 180));
            }

            return new QueryMatchResult(0, -60000 - (distance * 400));
        }

        private static long ScoreNoQueryContext(LanguageServiceCompletionItem item, string lowerCandidate, CompletionRankingContext context)
        {
            var score = 0L;
            if (context.IsMemberAccess)
            {
                score += 1000;
            }

            if (item != null &&
                string.Equals(item.Kind ?? string.Empty, "Keyword", StringComparison.OrdinalIgnoreCase) &&
                lowerCandidate.Length <= 8)
            {
                score += 200;
            }

            return score;
        }

        private static int GetKindBoost(string kind, CompletionRankingContext context)
        {
            if (string.IsNullOrEmpty(kind))
            {
                return 0;
            }

            if (context.IsMemberAccess)
            {
                if (kind.Equals("Method", StringComparison.OrdinalIgnoreCase))
                {
                    return 2400;
                }

                if (kind.Equals("Property", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Field", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Event", StringComparison.OrdinalIgnoreCase))
                {
                    return 2200;
                }

                if (kind.Equals("Class", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Struct", StringComparison.OrdinalIgnoreCase))
                {
                    return 1200;
                }
            }

            if (context.IsAfterNew)
            {
                if (kind.Equals("Class", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Struct", StringComparison.OrdinalIgnoreCase))
                {
                    return 2600;
                }
            }

            if (context.IsAfterOverride)
            {
                if (kind.Equals("Method", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Property", StringComparison.OrdinalIgnoreCase))
                {
                    return 2600;
                }
            }

            if (context.IsAfterUsing)
            {
                if (kind.Equals("Namespace", StringComparison.OrdinalIgnoreCase))
                {
                    return 2800;
                }
            }

            if (context.IsDeclarationContext)
            {
                if (kind.Equals("Keyword", StringComparison.OrdinalIgnoreCase))
                {
                    return 1800;
                }

                if (kind.Equals("Class", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Interface", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Struct", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Enum", StringComparison.OrdinalIgnoreCase))
                {
                    return 1600;
                }
            }

            if (context.IsAfterReturn)
            {
                if (kind.Equals("Local", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Parameter", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Property", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Field", StringComparison.OrdinalIgnoreCase) ||
                    kind.Equals("Method", StringComparison.OrdinalIgnoreCase))
                {
                    return 1200;
                }
            }

            return 0;
        }

        private static int GetKeywordBoost(string lowerCandidate, CompletionRankingContext context)
        {
            if (string.IsNullOrEmpty(lowerCandidate))
            {
                return 0;
            }

            if (context.IsDeclarationContext)
            {
                switch (lowerCandidate)
                {
                    case "class":
                    case "struct":
                    case "interface":
                    case "enum":
                    case "void":
                    case "string":
                    case "int":
                    case "bool":
                    case "float":
                    case "double":
                    case "public":
                    case "private":
                    case "protected":
                    case "internal":
                    case "static":
                    case "partial":
                        return 3200;
                }
            }

            if (context.IsAfterUsing && lowerCandidate == "system")
            {
                return 2400;
            }

            if (context.IsAfterNew)
            {
                switch (lowerCandidate)
                {
                    case "list":
                    case "dictionary":
                    case "hashset":
                        return 1000;
                }
            }

            if (context.IsAfterReturn)
            {
                switch (lowerCandidate)
                {
                    case "true":
                    case "false":
                    case "null":
                    case "default":
                        return 1200;
                }
            }

            return 0;
        }

        private static RankedCompletionItem[] FilterWeakMatches(RankedCompletionItem[] ranked, CompletionRankingContext context)
        {
            if (ranked == null || ranked.Length == 0 || string.IsNullOrEmpty(context.Query))
            {
                return ranked ?? new RankedCompletionItem[0];
            }

            var minimumTier = context.Query.Length >= 3 ? 2 : (context.Query.Length >= 2 ? 1 : 0);
            var strongCount = 0;
            for (var i = 0; i < ranked.Length; i++)
            {
                if (ranked[i].MatchTier >= minimumTier)
                {
                    strongCount++;
                }
            }

            if (strongCount < 3)
            {
                return ranked;
            }

            var filtered = new RankedCompletionItem[strongCount];
            var next = 0;
            for (var i = 0; i < ranked.Length; i++)
            {
                if (ranked[i].MatchTier >= minimumTier)
                {
                    filtered[next++] = ranked[i];
                }
            }

            return filtered;
        }

        private static bool IsDeclarationContext(string text, int replacementStart, string previousToken)
        {
            if (string.IsNullOrEmpty(previousToken))
            {
                return false;
            }

            switch (previousToken)
            {
                case "public":
                case "private":
                case "protected":
                case "internal":
                case "static":
                case "partial":
                case "abstract":
                case "sealed":
                case "unsafe":
                case "virtual":
                case "async":
                    return true;
            }

            var lineStart = replacementStart;
            while (lineStart > 0)
            {
                var previous = text[lineStart - 1];
                if (previous == '\n' || previous == '\r')
                {
                    break;
                }

                lineStart--;
            }

            var linePrefix = replacementStart > lineStart
                ? text.Substring(lineStart, replacementStart - lineStart).TrimStart()
                : string.Empty;
            return linePrefix.StartsWith("public ", StringComparison.Ordinal) ||
                linePrefix.StartsWith("private ", StringComparison.Ordinal) ||
                linePrefix.StartsWith("protected ", StringComparison.Ordinal) ||
                linePrefix.StartsWith("internal ", StringComparison.Ordinal) ||
                linePrefix.StartsWith("static ", StringComparison.Ordinal) ||
                linePrefix.StartsWith("partial ", StringComparison.Ordinal) ||
                linePrefix.StartsWith("abstract ", StringComparison.Ordinal) ||
                linePrefix.StartsWith("sealed ", StringComparison.Ordinal);
        }

        private static string FindPreviousToken(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index <= 0)
            {
                return string.Empty;
            }

            var current = index - 1;
            while (current >= 0 && char.IsWhiteSpace(text[current]))
            {
                current--;
            }

            if (current < 0)
            {
                return string.Empty;
            }

            if (!char.IsLetterOrDigit(text[current]) && text[current] != '_')
            {
                current--;
                while (current >= 0 && char.IsWhiteSpace(text[current]))
                {
                    current--;
                }
            }

            if (current < 0)
            {
                return string.Empty;
            }

            var end = current;
            while (current >= 0 && (char.IsLetterOrDigit(text[current]) || text[current] == '_'))
            {
                current--;
            }

            return end >= current + 1
                ? text.Substring(current + 1, end - current)
                : string.Empty;
        }

        private static int FindIdentifierStart(string text, int caretIndex)
        {
            if (string.IsNullOrEmpty(text) || caretIndex <= 0)
            {
                return Math.Max(0, caretIndex);
            }

            var index = Math.Min(text.Length, caretIndex);
            while (index > 0)
            {
                var value = text[index - 1];
                if (!char.IsLetterOrDigit(value) && value != '_')
                {
                    break;
                }

                index--;
            }

            return index;
        }

        private static char FindPreviousSymbol(string text, int index)
        {
            if (string.IsNullOrEmpty(text) || index <= 0)
            {
                return '\0';
            }

            var current = index - 1;
            while (current >= 0 && char.IsWhiteSpace(text[current]))
            {
                current--;
            }

            return current >= 0 ? text[current] : '\0';
        }

        private static bool CamelCaseStartsWith(string candidate, string query)
        {
            if (string.IsNullOrEmpty(candidate) || string.IsNullOrEmpty(query))
            {
                return false;
            }

            var capitals = string.Empty;
            for (var i = 0; i < candidate.Length; i++)
            {
                var value = candidate[i];
                if (char.IsUpper(value))
                {
                    capitals += value;
                }
            }

            return capitals.StartsWith(query, StringComparison.OrdinalIgnoreCase);
        }

        private static bool StartsWithWordPart(string candidate, string query)
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
                    char.IsUpper(current) && !char.IsUpper(previous))
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

        private static bool IsSubsequence(string query, string candidate)
        {
            if (string.IsNullOrEmpty(query))
            {
                return true;
            }

            var queryIndex = 0;
            for (var i = 0; i < candidate.Length && queryIndex < query.Length; i++)
            {
                if (candidate[i] == query[queryIndex])
                {
                    queryIndex++;
                }
            }

            return queryIndex == query.Length;
        }

        private static int SubsequenceGapPenalty(string query, string candidate)
        {
            if (string.IsNullOrEmpty(query) || string.IsNullOrEmpty(candidate))
            {
                return 0;
            }

            var penalty = 0;
            var lastMatch = -1;
            var queryIndex = 0;
            for (var i = 0; i < candidate.Length && queryIndex < query.Length; i++)
            {
                if (candidate[i] != query[queryIndex])
                {
                    continue;
                }

                if (lastMatch >= 0)
                {
                    penalty += Math.Max(0, i - lastMatch - 1);
                }

                lastMatch = i;
                queryIndex++;
            }

            return penalty;
        }

        private static int ComputePrefixDistance(string candidate, string query)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return string.IsNullOrEmpty(query) ? 0 : query.Length;
            }

            var length = Math.Min(candidate.Length, query.Length);
            var distance = Math.Abs(candidate.Length - query.Length);
            for (var i = 0; i < length; i++)
            {
                if (candidate[i] != query[i])
                {
                    distance++;
                }
            }

            return distance;
        }

        private static int CompareRankedItems(RankedCompletionItem left, RankedCompletionItem right)
        {
            var scoreCompare = right.Score.CompareTo(left.Score);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            var leftText = left.Item != null ? left.Item.SortText ?? left.Item.FilterText ?? left.Item.DisplayText ?? string.Empty : string.Empty;
            var rightText = right.Item != null ? right.Item.SortText ?? right.Item.FilterText ?? right.Item.DisplayText ?? string.Empty : string.Empty;
            var textCompare = string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
            if (textCompare != 0)
            {
                return textCompare;
            }

            return left.OriginalIndex.CompareTo(right.OriginalIndex);
        }

        private sealed class RankedCompletionItem
        {
            public LanguageServiceCompletionItem Item;
            public long Score;
            public int MatchTier;
            public int OriginalIndex;
        }

        private struct QueryMatchResult
        {
            public QueryMatchResult(int matchTier, long score)
            {
                MatchTier = matchTier;
                Score = score;
            }

            public int MatchTier;
            public long Score;
        }

        private sealed class CompletionRankingContext
        {
            public string Query;
            public string LowerQuery;
            public string PreviousToken;
            public string LowerPreviousToken;
            public char PreviousSymbol;
            public bool IsMemberAccess;
            public bool IsAfterUsing;
            public bool IsAfterNew;
            public bool IsAfterReturn;
            public bool IsAfterOverride;
            public bool IsDeclarationContext;
        }
    }
}
