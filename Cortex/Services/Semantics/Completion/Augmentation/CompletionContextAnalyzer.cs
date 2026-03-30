using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services.Semantics.Completion.Augmentation
{
    internal sealed class CompletionContextAnalyzer
    {
        public CompletionRankingContext Analyze(
            DocumentSession session,
            CortexCompletionInteractionState editorState,
            LanguageServiceCompletionResponse response)
        {
            var text = session != null ? session.Text ?? string.Empty : string.Empty;
            var caretIndex = session != null && session.EditorState != null ? session.EditorState.CaretIndex : text.Length;
            caretIndex = Math.Max(0, Math.Min(text.Length, caretIndex));

            var replacementRange = response != null ? response.ReplacementRange : null;
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
            var memberAccessReceiver = previousSymbol == '.'
                ? FindMemberAccessReceiver(text, queryStart)
                : string.Empty;

            var context = new CompletionRankingContext
            {
                Query = queryTrimmed,
                LowerQuery = queryTrimmed.ToLowerInvariant(),
                PreviousToken = previousToken,
                PreviousSymbol = previousSymbol,
                IsMemberAccess = previousSymbol == '.',
                MemberAccessReceiver = memberAccessReceiver,
                LowerMemberAccessReceiver = memberAccessReceiver.ToLowerInvariant(),
                IsAfterUsing = string.Equals(previousToken, "using", StringComparison.OrdinalIgnoreCase),
                IsAfterNew = string.Equals(previousToken, "new", StringComparison.OrdinalIgnoreCase),
                IsAfterReturn = string.Equals(previousToken, "return", StringComparison.OrdinalIgnoreCase),
                IsAfterOverride = string.Equals(previousToken, "override", StringComparison.OrdinalIgnoreCase),
                IsDeclarationContext = IsDeclarationContext(text, queryStart, previousToken),
                RecentAcceptedCompletions = editorState != null ? editorState.RecentAcceptedCompletions : null,
                DocumentPath = session != null ? session.FilePath ?? string.Empty : string.Empty,
                AcceptanceSequence = editorState != null ? editorState.AcceptanceSequence : 0
            };

            PopulateObservedContext(text, caretIndex, context);
            return context;
        }

        public LanguageServiceCompletionItem[] BuildAugmentedCandidates(
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
                string segment;
                int segmentStart;
                while (TryReadMemberAccess(text, ref lookahead, limit, out segment, out segmentStart))
                {
                    if (string.IsNullOrEmpty(segment))
                    {
                        break;
                    }

                    RecordReceiverMemberObservation(context, chainBuilder, segment, rootStart, caretIndex);
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
                observation = new IdentifierObservation();
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

        private static void RecordReceiverMemberObservation(
            CompletionRankingContext context,
            string receiver,
            string member,
            int startIndex,
            int caretIndex)
        {
            if (context == null || string.IsNullOrEmpty(receiver) || string.IsNullOrEmpty(member))
            {
                return;
            }

            var key = BuildReceiverMemberKey(receiver, member);
            ReceiverMemberObservation observation;
            if (!context.ReceiverMemberObservations.TryGetValue(key, out observation))
            {
                observation = new ReceiverMemberObservation
                {
                    ReceiverText = receiver,
                    LowerReceiverText = receiver.ToLowerInvariant(),
                    MemberText = member,
                    LowerMemberText = member.ToLowerInvariant()
                };
                context.ReceiverMemberObservations[key] = observation;
            }

            observation.Count++;
            var distance = Math.Max(0, caretIndex - startIndex);
            observation.RecencyScore += ComputeOccurrenceWeight(distance);
            if (distance <= 320)
            {
                observation.NearCaretCount++;
            }
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

        private static string FindMemberAccessReceiver(string text, int index)
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

            if (current < 0 || text[current] != '.')
            {
                return string.Empty;
            }

            current--;
            var segments = new List<string>();
            while (current >= 0)
            {
                while (current >= 0 && char.IsWhiteSpace(text[current]))
                {
                    current--;
                }

                if (current < 0 || !IsIdentifierCharacter(text[current]))
                {
                    break;
                }

                var end = current + 1;
                while (current >= 0 && IsIdentifierCharacter(text[current]))
                {
                    current--;
                }

                var segment = text.Substring(current + 1, end - current - 1);
                if (segment.Length > 0 && segment[0] == '@')
                {
                    segment = segment.Substring(1);
                }

                if (string.IsNullOrEmpty(segment))
                {
                    break;
                }

                segments.Insert(0, segment);
                while (current >= 0 && char.IsWhiteSpace(text[current]))
                {
                    current--;
                }

                if (current < 0 || text[current] != '.')
                {
                    break;
                }

                current--;
            }

            return segments.Count > 0
                ? string.Join(".", segments.ToArray())
                : string.Empty;
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

        private static bool IsIdentifierCharacter(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_' || value == '@';
        }

        private static string BuildReceiverMemberKey(string receiver, string member)
        {
            return (receiver ?? string.Empty).ToLowerInvariant() + "|" +
                (member ?? string.Empty).ToLowerInvariant();
        }
    }

    internal sealed class CompletionRankingContext
    {
        public string Query;
        public string LowerQuery;
        public string PreviousToken;
        public char PreviousSymbol;
        public bool IsMemberAccess;
        public string MemberAccessReceiver;
        public string LowerMemberAccessReceiver;
        public bool IsAfterUsing;
        public bool IsAfterNew;
        public bool IsAfterReturn;
        public bool IsAfterOverride;
        public bool IsDeclarationContext;
        public string DocumentPath;
        public IList<CortexAcceptedCompletionEntry> RecentAcceptedCompletions;
        public readonly Dictionary<string, IdentifierObservation> IdentifierObservations = new Dictionary<string, IdentifierObservation>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, MemberChainPattern> MemberChainStats = new Dictionary<string, MemberChainPattern>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, MemberChainRootObservation> MemberChainRootStats = new Dictionary<string, MemberChainRootObservation>(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, ReceiverMemberObservation> ReceiverMemberObservations = new Dictionary<string, ReceiverMemberObservation>(StringComparer.OrdinalIgnoreCase);
        public int AcceptanceSequence;
    }

    internal sealed class IdentifierObservation
    {
        public int Count;
        public int RecencyScore;
        public int NearCaretCount;
    }

    internal sealed class MemberChainPattern
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

    internal sealed class MemberChainRootObservation
    {
        public int Count;
        public int RecencyScore;
    }

    internal sealed class ReceiverMemberObservation
    {
        public string ReceiverText;
        public string LowerReceiverText;
        public string MemberText;
        public string LowerMemberText;
        public int Count;
        public int RecencyScore;
        public int NearCaretCount;
    }
}
