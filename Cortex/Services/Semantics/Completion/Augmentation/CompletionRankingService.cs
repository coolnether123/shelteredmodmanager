using System;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Completion.Augmentation
{
    internal sealed class CompletionRankingService
    {
        private readonly CompletionContextAnalyzer _contextAnalyzer = new CompletionContextAnalyzer();

        public string GetQuery(DocumentSession session, LanguageServiceCompletionResponse response)
        {
            return _contextAnalyzer.Analyze(session, null, response).Query;
        }

        public LanguageServiceCompletionResponse Rank(
            DocumentSession session,
            CortexCompletionInteractionState editorState,
            LanguageServiceCompletionResponse response)
        {
            if (session == null || response == null || response.Items == null || response.Items.Length <= 1)
            {
                return response;
            }

            var rankingContext = _contextAnalyzer.Analyze(session, editorState, response);
            var candidates = _contextAnalyzer.BuildAugmentedCandidates(response.Items, rankingContext);
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

        private static long ScoreItem(LanguageServiceCompletionItem item, CompletionRankingContext context, QueryMatchResult match)
        {
            if (item == null)
            {
                return long.MinValue;
            }

            var candidate = CompletionMatchUtility.GetCandidateText(item);
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
                score += ScoreNoQueryContext(item, candidate, lowerCandidate, context);
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

            var candidate = CompletionMatchUtility.GetCandidateText(item);
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

            if (CompletionMatchUtility.StartsWithWordPart(candidate, query))
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

        private static long ScoreNoQueryContext(
            LanguageServiceCompletionItem item,
            string candidate,
            string lowerCandidate,
            CompletionRankingContext context)
        {
            var score = 0L;
            if (context.IsMemberAccess)
            {
                score += 1000;
            }

            score += GetReceiverMemberBoost(candidate, lowerCandidate, context, 0);

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

            score += GetReceiverMemberBoost(candidate, lowerCandidate, context, match.MatchTier);

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

        private static int GetReceiverMemberBoost(
            string candidate,
            string lowerCandidate,
            CompletionRankingContext context,
            int matchTier)
        {
            if (string.IsNullOrEmpty(candidate) ||
                context == null ||
                !context.IsMemberAccess ||
                string.IsNullOrEmpty(context.LowerMemberAccessReceiver))
            {
                return 0;
            }

            ReceiverMemberObservation observation;
            if (!context.ReceiverMemberObservations.TryGetValue(
                BuildReceiverMemberKey(context.LowerMemberAccessReceiver, lowerCandidate),
                out observation))
            {
                return 0;
            }

            var score = Math.Min(32000, (observation.Count * 2600) + observation.RecencyScore);
            if (observation.NearCaretCount > 0)
            {
                score += Math.Min(10000, observation.NearCaretCount * 2600);
            }

            if (matchTier >= 5)
            {
                score += 2400;
            }
            else if (matchTier >= 4)
            {
                score += 1200;
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

        private static string BuildReceiverMemberKey(string receiver, string member)
        {
            return (receiver ?? string.Empty) + "|" + (member ?? string.Empty);
        }

        private static int CompareRankedItems(RankedCompletionItem left, RankedCompletionItem right)
        {
            var scoreCompare = right.Score.CompareTo(left.Score);
            if (scoreCompare != 0)
            {
                return scoreCompare;
            }

            var leftText = left.Item != null ? left.Item.SortText ?? CompletionMatchUtility.GetCandidateText(left.Item) : string.Empty;
            var rightText = right.Item != null ? right.Item.SortText ?? CompletionMatchUtility.GetCandidateText(right.Item) : string.Empty;
            var textCompare = string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
            if (textCompare != 0)
            {
                return textCompare;
            }

            return left.OriginalIndex.CompareTo(right.OriginalIndex);
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
    }
}
