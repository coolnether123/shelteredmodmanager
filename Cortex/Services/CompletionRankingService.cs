using System;
using System.Collections.Generic;
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

        public LanguageServiceCompletionResponse Rank(
            DocumentSession session,
            CortexEditorInteractionState editorState,
            LanguageServiceCompletionResponse response)
        {
            if (session == null || response == null || response.Items == null || response.Items.Length <= 1)
            {
                return response;
            }

            var rankingContext = BuildContext(session, editorState, response);
            var candidates = BuildAugmentedCandidates(response.Items, rankingContext);
            var ranked = new RankedCompletionItem[candidates.Length];
            for (var i = 0; i < candidates.Length; i++)
            {
                var match = EvaluateQueryMatch(candidates[i], rankingContext);
                ranked[i] = new RankedCompletionItem
                {
                    Item = candidates[i],
                    Score = ScoreItem(candidates[i], rankingContext, match),
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

        private static CompletionRankingContext BuildContext(
            DocumentSession session,
            CortexEditorInteractionState editorState,
            LanguageServiceCompletionResponse response)
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
            var acceptedCompletions = editorState != null
                ? editorState.RecentAcceptedCompletions
                : null;

            var context = new CompletionRankingContext
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
                IsDeclarationContext = IsDeclarationContext(text, queryStart, previousToken),
                CaretIndex = caretIndex,
                RecentAcceptedCompletions = acceptedCompletions,
                DocumentPath = session.FilePath ?? string.Empty
            };

            PopulateObservedContext(text, caretIndex, context);
            return context;
        }

        private static LanguageServiceCompletionItem[] BuildAugmentedCandidates(
            LanguageServiceCompletionItem[] items,
            CompletionRankingContext context)
        {
            if (items == null || items.Length == 0)
            {
                return items ?? new LanguageServiceCompletionItem[0];
            }

            var combined = new List<LanguageServiceCompletionItem>(items.Length + 8);
            var knownKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    continue;
                }

                combined.Add(item);
                knownKeys.Add(BuildCandidateKey(item));
            }

            var synthetic = BuildSyntheticPatternCandidates(context);
            for (var i = 0; i < synthetic.Count; i++)
            {
                var item = synthetic[i];
                var key = BuildCandidateKey(item);
                if (knownKeys.Contains(key))
                {
                    continue;
                }

                knownKeys.Add(key);
                combined.Add(item);
            }

            return combined.ToArray();
        }

        private static List<LanguageServiceCompletionItem> BuildSyntheticPatternCandidates(CompletionRankingContext context)
        {
            var results = new List<LanguageServiceCompletionItem>();
            if (context == null ||
                context.IsMemberAccess ||
                string.IsNullOrEmpty(context.Query) ||
                context.MemberChainStats.Count == 0)
            {
                return results;
            }

            var rankedPatterns = new List<MemberChainPattern>();
            foreach (var pair in context.MemberChainStats)
            {
                var chain = pair.Value;
                if (chain == null ||
                    string.IsNullOrEmpty(chain.DisplayText) ||
                    string.IsNullOrEmpty(chain.RootIdentifier))
                {
                    continue;
                }

                if (!chain.LowerRootIdentifier.StartsWith(context.LowerQuery, StringComparison.OrdinalIgnoreCase) &&
                    !chain.LowerDisplayText.StartsWith(context.LowerQuery, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (chain.SegmentCount < 2)
                {
                    continue;
                }

                rankedPatterns.Add(chain);
            }

            rankedPatterns.Sort(CompareMemberChainPatterns);
            var limit = Math.Min(4, rankedPatterns.Count);
            for (var i = 0; i < limit; i++)
            {
                var pattern = rankedPatterns[i];
                results.Add(new LanguageServiceCompletionItem
                {
                    DisplayText = pattern.DisplayText,
                    InsertText = pattern.DisplayText,
                    FilterText = pattern.DisplayText,
                    SortText = pattern.DisplayText,
                    InlineDescription = "context pattern",
                    Kind = "Pattern",
                    IsPreselected = false
                });
            }

            return results;
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
                score += GetShortQueryKindBoost(item, context, match);
                score += GetObservedContextBoost(item, candidate, lowerCandidate, context, match);
                score += GetAcceptedCompletionBoost(candidate, lowerCandidate, context, match);
            }
            else
            {
                score += ScoreNoQueryContext(item, lowerCandidate, context);
            }

            if (!string.IsNullOrEmpty(item.SortText))
            {
                score -= Math.Min(200, item.SortText.Length);
            }

            if (!context.IsMemberAccess && candidate.IndexOf('.') >= 0)
            {
                score -= 1800;
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

        private static int GetShortQueryKindBoost(LanguageServiceCompletionItem item, CompletionRankingContext context, QueryMatchResult match)
        {
            if (item == null || string.IsNullOrEmpty(context.Query))
            {
                return 0;
            }

            var queryLength = context.Query.Length;
            if (queryLength > 2 || context.IsMemberAccess)
            {
                return 0;
            }

            var score = 0;
            if (IsTypeLikeKind(item.Kind))
            {
                if (match.MatchTier >= 5)
                {
                    score += 3200;
                }
                else if (match.MatchTier >= 4)
                {
                    score += 1800;
                }
            }
            else if (string.Equals(item.Kind ?? string.Empty, "Namespace", StringComparison.OrdinalIgnoreCase))
            {
                if (match.MatchTier >= 5)
                {
                    score += 2400;
                }
                else if (match.MatchTier >= 4)
                {
                    score += 1200;
                }
            }

            if (queryLength == 1)
            {
                if (match.MatchTier == 2)
                {
                    score -= 90000;
                }
                else if (match.MatchTier <= 1)
                {
                    score -= 120000;
                }
            }
            else if (queryLength == 2)
            {
                if (match.MatchTier == 2)
                {
                    score -= 45000;
                }
                else if (match.MatchTier <= 1)
                {
                    score -= 70000;
                }
            }

            return score;
        }

        private static RankedCompletionItem[] FilterWeakMatches(RankedCompletionItem[] ranked, CompletionRankingContext context)
        {
            if (ranked == null || ranked.Length == 0 || string.IsNullOrEmpty(context.Query))
            {
                return ranked ?? new RankedCompletionItem[0];
            }

            var minimumTier = GetMinimumMatchTier(context.Query);
            var strongCount = 0;
            for (var i = 0; i < ranked.Length; i++)
            {
                if (ranked[i].MatchTier >= minimumTier)
                {
                    strongCount++;
                }
            }

            if (strongCount == 0)
            {
                return LimitRankedItems(ranked, GetWeakMatchFallbackLimit(context.Query));
            }

            if (strongCount < GetStrongMatchThreshold(context.Query))
            {
                return LimitRankedItems(BuildFilteredMatches(ranked, minimumTier, strongCount), GetStrongMatchFallbackLimit(context.Query));
            }

            return BuildFilteredMatches(ranked, minimumTier, strongCount);
        }

        private static int GetMinimumMatchTier(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return 0;
            }

            if (query.Length <= 2)
            {
                return 4;
            }

            return query.Length >= 3 ? 2 : 0;
        }

        private static int GetStrongMatchThreshold(string query)
        {
            return !string.IsNullOrEmpty(query) && query.Length <= 2 ? 5 : 3;
        }

        private static int GetStrongMatchFallbackLimit(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return 32;
            }

            if (query.Length <= 2)
            {
                return 12;
            }

            return 8;
        }

        private static int GetWeakMatchFallbackLimit(string query)
        {
            if (string.IsNullOrEmpty(query))
            {
                return 32;
            }

            return query.Length <= 2 ? 24 : 12;
        }

        private static RankedCompletionItem[] BuildFilteredMatches(RankedCompletionItem[] ranked, int minimumTier, int strongCount)
        {
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

        private static int GetObservedContextBoost(
            LanguageServiceCompletionItem item,
            string candidate,
            string lowerCandidate,
            CompletionRankingContext context,
            QueryMatchResult match)
        {
            if (string.IsNullOrEmpty(candidate))
            {
                return 0;
            }

            var score = 0;
            IdentifierObservation identifierObservation;
            if (context.IdentifierObservations.TryGetValue(lowerCandidate, out identifierObservation))
            {
                score += Math.Min(24000, (identifierObservation.Count * 1400) + identifierObservation.RecencyScore);
                if (identifierObservation.NearCaretCount > 0)
                {
                    score += Math.Min(9000, identifierObservation.NearCaretCount * 2200);
                }
            }

            MemberChainPattern memberPattern;
            if (context.MemberChainStats.TryGetValue(lowerCandidate, out memberPattern))
            {
                score += Math.Min(18000, (memberPattern.Count * 2200) + memberPattern.RecencyScore);
                if (memberPattern.NearCaretCount > 0)
                {
                    score += Math.Min(9000, memberPattern.NearCaretCount * 2400);
                }
            }

            MemberChainRootObservation rootObservation;
            if (context.MemberChainRootStats.TryGetValue(lowerCandidate, out rootObservation))
            {
                score += Math.Min(12000, (rootObservation.Count * 1200) + rootObservation.RecencyScore);
                if (match.MatchTier >= 5)
                {
                    score += Math.Min(5000, rootObservation.Count * 450);
                }
            }

            if (!context.IsMemberAccess &&
                candidate.IndexOf('.') < 0 &&
                IsTypeLikeKind(item != null ? item.Kind : string.Empty) &&
                identifierObservation != null &&
                identifierObservation.Count > 0)
            {
                score += 1800;
            }

            return score;
        }

        private static int GetAcceptedCompletionBoost(
            string candidate,
            string lowerCandidate,
            CompletionRankingContext context,
            QueryMatchResult match)
        {
            if (string.IsNullOrEmpty(candidate) ||
                context == null ||
                context.RecentAcceptedCompletions == null ||
                context.RecentAcceptedCompletions.Count == 0)
            {
                return 0;
            }

            var score = 0;
            for (var i = context.RecentAcceptedCompletions.Count - 1; i >= 0; i--)
            {
                var entry = context.RecentAcceptedCompletions[i];
                if (entry == null)
                {
                    continue;
                }

                if (!string.IsNullOrEmpty(entry.DocumentPath) &&
                    !string.IsNullOrEmpty(context.DocumentPath) &&
                    !string.Equals(entry.DocumentPath, context.DocumentPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var text = entry.CompletionText ?? string.Empty;
                if (string.IsNullOrEmpty(text))
                {
                    continue;
                }

                var lowerText = text.ToLowerInvariant();
                var recencyWeight = Math.Max(1, context.AcceptanceSequence - entry.Sequence + 1);
                var recencyScore = 14000 / recencyWeight;

                if (string.Equals(lowerText, lowerCandidate, StringComparison.Ordinal))
                {
                    score += recencyScore;
                    continue;
                }

                if (lowerText.StartsWith(lowerCandidate, StringComparison.Ordinal) && match.MatchTier >= 5)
                {
                    score += Math.Max(1500, recencyScore / 2);
                    continue;
                }

                if (lowerText.StartsWith(lowerCandidate + ".", StringComparison.Ordinal) && match.MatchTier >= 5)
                {
                    score += Math.Max(1200, recencyScore / 3);
                }
            }

            return score;
        }

        private static RankedCompletionItem[] LimitRankedItems(RankedCompletionItem[] ranked, int limit)
        {
            if (ranked == null || ranked.Length == 0)
            {
                return ranked ?? new RankedCompletionItem[0];
            }

            if (limit <= 0 || ranked.Length <= limit)
            {
                return ranked;
            }

            var limited = new RankedCompletionItem[limit];
            Array.Copy(ranked, limited, limit);
            return limited;
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

        private static int CompareMemberChainPatterns(MemberChainPattern left, MemberChainPattern right)
        {
            var rightScore = right != null ? ((right.Count * 1000) + right.RecencyScore + (right.NearCaretCount * 2500)) : 0;
            var leftScore = left != null ? ((left.Count * 1000) + left.RecencyScore + (left.NearCaretCount * 2500)) : 0;
            var scoreCompare = rightScore.CompareTo(leftScore);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            var leftText = left != null ? left.DisplayText ?? string.Empty : string.Empty;
            var rightText = right != null ? right.DisplayText ?? string.Empty : string.Empty;
            return string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCandidateKey(LanguageServiceCompletionItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return (item.FilterText ?? string.Empty) + "|" +
                (item.InsertText ?? string.Empty) + "|" +
                (item.DisplayText ?? string.Empty);
        }

        private static void PopulateObservedContext(string text, int caretIndex, CompletionRankingContext context)
        {
            if (context == null || string.IsNullOrEmpty(text) || caretIndex <= 0)
            {
                return;
            }

            var limit = Math.Max(0, Math.Min(caretIndex, text.Length));
            var index = 0;
            while (index < limit)
            {
                if (!IsIdentifierStart(text[index]))
                {
                    index++;
                    continue;
                }

                var rootStart = index;
                var root = ReadIdentifier(text, ref index, limit);
                if (string.IsNullOrEmpty(root))
                {
                    index++;
                    continue;
                }

                RecordIdentifierObservation(context, root, rootStart, caretIndex);

                var chainBuilder = root;
                var segmentCount = 1;
                var lookahead = index;
                while (TryReadMemberAccess(text, ref lookahead, limit, out var segment, out var segmentStart))
                {
                    if (string.IsNullOrEmpty(segment))
                    {
                        break;
                    }

                    chainBuilder += "." + segment;
                    segmentCount++;
                    RecordIdentifierObservation(context, segment, segmentStart, caretIndex);
                    RecordMemberChainObservation(context, root, chainBuilder, segmentCount, rootStart, caretIndex);
                    index = lookahead;
                }
            }
        }

        private static void RecordIdentifierObservation(
            CompletionRankingContext context,
            string identifier,
            int startIndex,
            int caretIndex)
        {
            if (context == null || string.IsNullOrEmpty(identifier))
            {
                return;
            }

            var key = identifier.ToLowerInvariant();
            IdentifierObservation observation;
            if (!context.IdentifierObservations.TryGetValue(key, out observation))
            {
                observation = new IdentifierObservation
                {
                    DisplayText = identifier
                };
                context.IdentifierObservations[key] = observation;
            }

            observation.Count++;
            var distance = Math.Max(0, caretIndex - startIndex);
            observation.RecencyScore += ComputeOccurrenceWeight(distance);
            if (distance <= 240)
            {
                observation.NearCaretCount++;
            }
        }

        private static void RecordMemberChainObservation(
            CompletionRankingContext context,
            string rootIdentifier,
            string chain,
            int segmentCount,
            int startIndex,
            int caretIndex)
        {
            if (context == null || string.IsNullOrEmpty(rootIdentifier) || string.IsNullOrEmpty(chain))
            {
                return;
            }

            var lowerChain = chain.ToLowerInvariant();
            MemberChainPattern pattern;
            if (!context.MemberChainStats.TryGetValue(lowerChain, out pattern))
            {
                pattern = new MemberChainPattern
                {
                    DisplayText = chain,
                    LowerDisplayText = lowerChain,
                    RootIdentifier = rootIdentifier,
                    LowerRootIdentifier = rootIdentifier.ToLowerInvariant(),
                    SegmentCount = segmentCount
                };
                context.MemberChainStats[lowerChain] = pattern;
            }

            pattern.Count++;
            var distance = Math.Max(0, caretIndex - startIndex);
            pattern.RecencyScore += ComputeOccurrenceWeight(distance);
            if (distance <= 320)
            {
                pattern.NearCaretCount++;
            }

            var lowerRoot = pattern.LowerRootIdentifier;
            MemberChainRootObservation rootObservation;
            if (!context.MemberChainRootStats.TryGetValue(lowerRoot, out rootObservation))
            {
                rootObservation = new MemberChainRootObservation();
                context.MemberChainRootStats[lowerRoot] = rootObservation;
            }

            rootObservation.Count++;
            rootObservation.RecencyScore += Math.Max(400, ComputeOccurrenceWeight(distance) / 2);
        }

        private static int ComputeOccurrenceWeight(int distance)
        {
            if (distance <= 48)
            {
                return 5200;
            }

            if (distance <= 160)
            {
                return 3600;
            }

            if (distance <= 480)
            {
                return 2200;
            }

            if (distance <= 1200)
            {
                return 1200;
            }

            if (distance <= 3200)
            {
                return 650;
            }

            return 250;
        }

        private static bool TryReadMemberAccess(
            string text,
            ref int index,
            int limit,
            out string identifier,
            out int identifierStart)
        {
            identifier = string.Empty;
            identifierStart = -1;
            var lookahead = index;
            while (lookahead < limit && char.IsWhiteSpace(text[lookahead]))
            {
                lookahead++;
            }

            if (lookahead >= limit || text[lookahead] != '.')
            {
                return false;
            }

            lookahead++;
            while (lookahead < limit && char.IsWhiteSpace(text[lookahead]))
            {
                lookahead++;
            }

            if (lookahead >= limit || !IsIdentifierStart(text[lookahead]))
            {
                return false;
            }

            identifierStart = lookahead;
            identifier = ReadIdentifier(text, ref lookahead, limit);
            index = lookahead;
            return !string.IsNullOrEmpty(identifier);
        }

        private static string ReadIdentifier(string text, ref int index, int limit)
        {
            if (string.IsNullOrEmpty(text) || index < 0 || index >= limit)
            {
                return string.Empty;
            }

            var start = index;
            if (text[index] == '@')
            {
                index++;
            }

            if (index >= limit || (!char.IsLetter(text[index]) && text[index] != '_'))
            {
                index = start;
                return string.Empty;
            }

            index++;
            while (index < limit)
            {
                var value = text[index];
                if (!char.IsLetterOrDigit(value) && value != '_')
                {
                    break;
                }

                index++;
            }

            var raw = text.Substring(start, index - start);
            return raw.Length > 0 && raw[0] == '@'
                ? raw.Substring(1)
                : raw;
        }

        private static bool IsIdentifierStart(char value)
        {
            return char.IsLetter(value) || value == '_' || value == '@';
        }

        private static bool IsTypeLikeKind(string kind)
        {
            if (string.IsNullOrEmpty(kind))
            {
                return false;
            }

            return kind.Equals("Class", StringComparison.OrdinalIgnoreCase) ||
                kind.Equals("Interface", StringComparison.OrdinalIgnoreCase) ||
                kind.Equals("Struct", StringComparison.OrdinalIgnoreCase) ||
                kind.Equals("Enum", StringComparison.OrdinalIgnoreCase) ||
                kind.Equals("Delegate", StringComparison.OrdinalIgnoreCase);
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
            public int CaretIndex;
            public string DocumentPath;
            public IList<CortexAcceptedCompletionEntry> RecentAcceptedCompletions;
            public readonly Dictionary<string, IdentifierObservation> IdentifierObservations = new Dictionary<string, IdentifierObservation>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, MemberChainPattern> MemberChainStats = new Dictionary<string, MemberChainPattern>(StringComparer.OrdinalIgnoreCase);
            public readonly Dictionary<string, MemberChainRootObservation> MemberChainRootStats = new Dictionary<string, MemberChainRootObservation>(StringComparer.OrdinalIgnoreCase);
            public int AcceptanceSequence;
        }

        private sealed class IdentifierObservation
        {
            public string DisplayText;
            public int Count;
            public int RecencyScore;
            public int NearCaretCount;
        }

        private sealed class MemberChainPattern
        {
            public string DisplayText;
            public string LowerDisplayText;
            public string RootIdentifier;
            public string LowerRootIdentifier;
            public int SegmentCount;
            public int Count;
            public int RecencyScore;
            public int NearCaretCount;
        }

        private sealed class MemberChainRootObservation
        {
            public int Count;
            public int RecencyScore;
        }
    }
}
