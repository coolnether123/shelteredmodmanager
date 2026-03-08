using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;

namespace ModAPI.Internal.DebugUI
{
    using Snapshot = ModAPI.Harmony.TranspilerDebugger.Snapshot;

    internal static class UIDebugSourceRewriteService
    {
        public static string ApplyRegexSourceRewrites(
            string source,
            List<UIDebugSourcePreviewHunk> hunks,
            Snapshot snapshot,
            MethodBase selectedMethod,
            string selectedMethodId,
            out List<string> summaries,
            out int replaceCount)
        {
            summaries = new List<string>();
            replaceCount = 0;
            if (string.IsNullOrEmpty(source) || hunks == null || hunks.Count == 0)
            {
                return source;
            }

            var methodName = selectedMethod != null
                ? selectedMethod.Name
                : UIDebugSourceMethodLocator.ExtractMethodNameFromSelectedId(selectedMethodId);

            int scopeStart;
            int scopeLength;
            var hasScopedBody = UIDebugSourceMethodLocator.TryFindMethodBodySpan(source, methodName, out scopeStart, out scopeLength);
            var scopePrefix = hasScopedBody ? source.Substring(0, scopeStart) : string.Empty;
            var scope = hasScopedBody ? source.Substring(scopeStart, scopeLength) : source;
            var scopeSuffix = hasScopedBody ? source.Substring(scopeStart + scopeLength) : string.Empty;
            var rewrittenScope = scope;
            var beforeInstructions = snapshot != null && snapshot.BeforeInstructions != null
                ? snapshot.BeforeInstructions
                : new List<string>();

            var anyCandidate = false;
            var appliedPairSet = new HashSet<string>(StringComparer.Ordinal);
            var pairRewriteOrdinals = new Dictionary<string, int>(StringComparer.Ordinal);

            for (var h = 0; h < hunks.Count; h++)
            {
                if (replaceCount >= 8) break;

                var hunk = hunks[h];
                var addedExpressions = ExtractAddedExpressionsFromHunk(hunk);
                var removedNames = ExtractRemovedSourceTokensFromHunk(hunk);
                if (addedExpressions.Count == 0) continue;

                if (removedNames.Count == 0 && hunk.StartIndexBefore < beforeInstructions.Count)
                {
                    for (var i = hunk.StartIndexBefore; i < Math.Min(hunk.StartIndexBefore + 5, beforeInstructions.Count); i++)
                    {
                        var tokens = ExtractTokensFromILLine(beforeInstructions[i]);
                        for (var t = 0; t < tokens.Count; t++)
                        {
                            var token = tokens[t];
                            if (!removedNames.Contains(token) && !IsHighRiskRemovedToken(token))
                            {
                                removedNames.Add(token);
                            }
                        }

                        if (removedNames.Count > 0) break;
                    }
                }

                anyCandidate = true;
                for (var a = 0; a < addedExpressions.Count; a++)
                {
                    if (replaceCount >= 8) break;
                    var replacementExpression = addedExpressions[a];
                    if (string.IsNullOrEmpty(replacementExpression)) continue;

                    for (var r = 0; r < removedNames.Count; r++)
                    {
                        if (replaceCount >= 8) break;
                        var token = removedNames[r];
                        if (string.IsNullOrEmpty(token)) continue;

                        var pairKey = token + "->" + replacementExpression;
                        if (appliedPairSet.Contains(pairKey)) continue;

                        int ordinalCursor;
                        if (!pairRewriteOrdinals.TryGetValue(pairKey, out ordinalCursor))
                        {
                            ordinalCursor = 0;
                        }

                        var before = replaceCount;
                        if (string.Equals(token, "__VECTOR2_ZERO_ZERO__", StringComparison.Ordinal) ||
                            string.Equals(token, "__VECTOR2_CTOR__", StringComparison.Ordinal))
                        {
                            var vector2LiteralPattern = string.Equals(token, "__VECTOR2_ZERO_ZERO__", StringComparison.Ordinal)
                                ? @"new\s+Vector2\s*\(\s*0(?:\.0+)?f?\s*,\s*0(?:\.0+)?f?\s*\)"
                                : @"new\s+Vector2\s*\(\s*[^,\)]+?\s*,\s*[^,\)]+?\s*\)";
                            rewrittenScope = TryApplyUniqueRegexRewrite(
                                rewrittenScope,
                                vector2LiteralPattern,
                                replacementExpression + " /* [REGEX_PATCH] */",
                                "Hunk " + (h + 1) + " literal new Vector2(...) -> " + replacementExpression,
                                summaries,
                                ref replaceCount,
                                ref ordinalCursor,
                                true);
                        }
                        else if (string.Equals(token, "__GRIDREF_HALF_COORDS__", StringComparison.Ordinal))
                        {
                            var gridRefHalfPattern =
                                @"new\s+(?:[\w\.]+\.)*GridRef\s*\(\s*" +
                                @"(?:[\w\.]+\.)*(?:m_)?width\s*(?:/[^,]+|>>\s*1)\s*," +
                                @"\s*(?:[\w\.]+\.)*(?:m_)?height\s*(?:/[^)]+|>>\s*1)\s*\)";

                            rewrittenScope = TryApplyUniqueRegexRewrite(
                                rewrittenScope,
                                gridRefHalfPattern,
                                replacementExpression,
                                "Hunk " + (h + 1) + " literal new GridRef(width/2,height/2) -> " + replacementExpression,
                                summaries,
                                ref replaceCount,
                                ref ordinalCursor,
                                true);

                            if (replaceCount == before)
                            {
                                var anyGridRefCtorPattern = @"new\s+(?:[A-Za-z_]\w*\.)*GridRef\s*\((?:[^()]|\([^()]*\))*\)";
                                rewrittenScope = TryApplyUniqueRegexRewrite(
                                    rewrittenScope,
                                    anyGridRefCtorPattern,
                                    replacementExpression + " /* [REGEX_PATCH] */",
                                    "Hunk " + (h + 1) + " fallback new GridRef(...) -> " + replacementExpression,
                                    summaries,
                                    ref replaceCount,
                                    ref ordinalCursor,
                                    true);
                            }
                        }
                        else if (token.StartsWith("__CTOR__", StringComparison.Ordinal))
                        {
                            var ctorTypePattern = BuildConstructorLiteralPattern(token);
                            if (!string.IsNullOrEmpty(ctorTypePattern))
                            {
                                rewrittenScope = TryApplyUniqueRegexRewrite(
                                    rewrittenScope,
                                    ctorTypePattern,
                                    replacementExpression + " /* [REGEX_PATCH] */",
                                    "Hunk " + (h + 1) + " ctor literal " + token + " -> " + replacementExpression,
                                    summaries,
                                    ref replaceCount,
                                    ref ordinalCursor,
                                    true);
                            }
                        }
                        else if (Regex.IsMatch(token, @"^[A-Za-z_]\w*$"))
                        {
                            if (IsHighRiskRemovedToken(token))
                            {
                                summaries.Add("[Regex Rewrite] High-risk token left unchanged: " + token + " (Hunk " + (h + 1) + ")");
                                continue;
                            }

                            var isReplacement = hunk.Removed != null && hunk.Removed.Count > 0;
                            var chainPattern = @"\b[A-Za-z_]\w*(?:\s*\.\s*[A-Za-z_]\w*)*\s*\.\s*" + Regex.Escape(token) + @"\b";
                            rewrittenScope = TryApplyUniqueRegexRewrite(
                                rewrittenScope,
                                chainPattern,
                                replacementExpression + (isReplacement ? " /* [REGEX_PATCH] */" : " /* [INSERT_PATCH] */"),
                                (isReplacement ? "Hunk " : "Anchor Hunk ") + (h + 1) + " chain ." + token + " -> " + replacementExpression,
                                summaries,
                                ref replaceCount,
                                ref ordinalCursor,
                                true,
                                !isReplacement);

                            if (replaceCount == before)
                            {
                                var symbolPattern = @"\b" + Regex.Escape(token) + @"\b";
                                rewrittenScope = TryApplyUniqueRegexRewrite(
                                    rewrittenScope,
                                    symbolPattern,
                                    replacementExpression + " /* [REGEX_PATCH] */",
                                    "Hunk " + (h + 1) + " symbol " + token + " -> " + replacementExpression,
                                    summaries,
                                    ref replaceCount,
                                    ref ordinalCursor,
                                    false);
                            }
                        }
                        else
                        {
                            var literalPattern = Regex.Escape(token);
                            rewrittenScope = TryApplyUniqueRegexRewrite(
                                rewrittenScope,
                                literalPattern,
                                replacementExpression + " /* [REGEX_PATCH] */",
                                "Hunk " + (h + 1) + " literal " + token + " -> " + replacementExpression,
                                summaries,
                                ref replaceCount,
                                ref ordinalCursor,
                                true);
                        }

                        if (replaceCount > before)
                        {
                            appliedPairSet.Add(pairKey);
                        }

                        pairRewriteOrdinals[pairKey] = ordinalCursor;
                    }
                }
            }

            if (!anyCandidate)
            {
                summaries.Add("[Regex Rewrite] No usable IL hunk pairs found (added expression + removed token).");
            }
            else if (replaceCount == 0)
            {
                summaries.Add("[Regex Rewrite] 0 applied (patterns were ambiguous or absent in method body).");
            }

            return hasScopedBody ? scopePrefix + rewrittenScope + scopeSuffix : rewrittenScope;
        }

        private static string TryApplyUniqueRegexRewrite(
            string source,
            string pattern,
            string replacement,
            string description,
            List<string> summaries,
            ref int replaceCount,
            ref int ordinalCursor,
            bool allowOrdinalFallback,
            bool anchorOnly = false)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(pattern)) return source;

            var regex = new Regex(pattern, RegexOptions.Multiline);
            var matches = regex.Matches(source);
            var validMatches = new List<Match>();
            for (var i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                if (!match.Success) continue;
                if (IsInsideCommentLine(source, match.Index)) continue;
                validMatches.Add(match);
            }

            if (validMatches.Count == 1)
            {
                replaceCount++;
                summaries.Add("[Regex Rewrite] Applied: " + description);
                var match = validMatches[0];
                ordinalCursor++;

                if (anchorOnly)
                {
                    return source.Insert(match.Index, replacement + "\n" + UIDebugSourceMethodLocator.GuessIndentationForAt(source, match.Index));
                }

                return source.Substring(0, match.Index) + replacement + source.Substring(match.Index + match.Length);
            }

            if (validMatches.Count > 1)
            {
                var allowBestGuess = description.IndexOf("literal", StringComparison.OrdinalIgnoreCase) >= 0;
                if (allowOrdinalFallback && ordinalCursor >= 0 && ordinalCursor < validMatches.Count)
                {
                    var match = validMatches[ordinalCursor];
                    replaceCount++;
                    summaries.Add("[Regex Rewrite] Applied by occurrence (" + (ordinalCursor + 1) + "/" + validMatches.Count + ", @line " + CountLineNumber(source, match.Index) + "): " + description);
                    ordinalCursor++;
                    return source.Substring(0, match.Index) + replacement + source.Substring(match.Index + match.Length);
                }

                if (allowBestGuess && validMatches.Count <= 4)
                {
                    var match = validMatches[0];
                    replaceCount++;
                    summaries.Add("[Regex Rewrite] Best-guess applied (" + validMatches.Count + " matches, chose first @line " + CountLineNumber(source, match.Index) + "): " + description);
                    ordinalCursor++;
                    return source.Substring(0, match.Index) + replacement + source.Substring(match.Index + match.Length);
                }

                summaries.Add("[Regex Rewrite] Ambiguous pattern (" + validMatches.Count + " matches), left unchanged: " + description);
            }
            else if (matches.Count > 0)
            {
                summaries.Add("[Regex Rewrite] Comment-only match, left unchanged: " + description);
            }

            return source;
        }

        private static int CountLineNumber(string source, int index)
        {
            var lineNumber = 1;
            for (var i = 0; i < index && i < source.Length; i++)
            {
                if (source[i] == '\n') lineNumber++;
            }

            return lineNumber;
        }

        private static bool IsHighRiskRemovedToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return true;
            return string.Equals(token, "instance", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(token, "current", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInsideCommentLine(string source, int index)
        {
            if (string.IsNullOrEmpty(source) || index < 0 || index >= source.Length) return false;
            var lineStart = source.LastIndexOf('\n', index);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var lineEnd = source.IndexOf('\n', index);
            if (lineEnd < 0) lineEnd = source.Length;
            var line = source.Substring(lineStart, lineEnd - lineStart);
            return line.TrimStart().StartsWith("//", StringComparison.Ordinal);
        }

        private static List<string> ExtractAddedExpressionsFromHunk(UIDebugSourcePreviewHunk hunk)
        {
            var result = new List<string>();
            if (hunk == null || hunk.Added == null) return result;

            for (var i = 0; i < hunk.Added.Count; i++)
            {
                var expression = BuildSourceExpressionFromILLine(hunk.Added[i]);
                if (!string.IsNullOrEmpty(expression) && !result.Contains(expression))
                {
                    result.Add(expression);
                }
            }

            return result;
        }

        private static List<string> ExtractRemovedSourceTokensFromHunk(UIDebugSourcePreviewHunk hunk)
        {
            var tokens = new List<string>();
            if (hunk == null || hunk.Removed == null) return tokens;

            var hasVector2Ctor = false;
            var vector2ZeroLoads = 0;
            var hasGridRefCtor = false;
            var widthFieldLoads = 0;
            var heightFieldLoads = 0;
            var intDivOps = 0;
            var removedCtorTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < hunk.Removed.Count; i++)
            {
                var line = hunk.Removed[i] ?? string.Empty;
                if (line.IndexOf("Vector2::.ctor", StringComparison.OrdinalIgnoreCase) >= 0) hasVector2Ctor = true;
                if (line.IndexOf("ldc.r4 0", StringComparison.OrdinalIgnoreCase) >= 0) vector2ZeroLoads++;
                if (line.IndexOf("GridRef::.ctor", StringComparison.OrdinalIgnoreCase) >= 0) hasGridRefCtor = true;
                TryCollectCtorToken(line, removedCtorTypes);
                if (line.IndexOf("::width", StringComparison.OrdinalIgnoreCase) >= 0) widthFieldLoads++;
                if (line.IndexOf("::height", StringComparison.OrdinalIgnoreCase) >= 0) heightFieldLoads++;
                if (line.IndexOf(" div", StringComparison.OrdinalIgnoreCase) >= 0 || line.StartsWith("div", StringComparison.OrdinalIgnoreCase)) intDivOps++;

                CollectMemberTokens(line, tokens);
            }

            if (hasVector2Ctor && !tokens.Contains("__VECTOR2_CTOR__")) tokens.Add("__VECTOR2_CTOR__");
            if (hasVector2Ctor && vector2ZeroLoads >= 2 && !tokens.Contains("__VECTOR2_ZERO_ZERO__")) tokens.Add("__VECTOR2_ZERO_ZERO__");
            if (hasGridRefCtor && widthFieldLoads > 0 && heightFieldLoads > 0 && intDivOps >= 2 && !tokens.Contains("__GRIDREF_HALF_COORDS__")) tokens.Add("__GRIDREF_HALF_COORDS__");

            foreach (var ctorType in removedCtorTypes)
            {
                var ctorToken = "__CTOR__" + ctorType;
                if (!tokens.Contains(ctorToken)) tokens.Add(ctorToken);
            }

            return tokens;
        }

        private static void CollectMemberTokens(string line, List<string> tokens)
        {
            var getterMatches = Regex.Matches(line, @"::get_([A-Za-z_]\w*)\(");
            for (var g = 0; g < getterMatches.Count; g++)
            {
                var token = getterMatches[g].Groups[1].Value;
                if (!string.IsNullOrEmpty(token) && !tokens.Contains(token)) tokens.Add(token);
            }

            var callMatches = Regex.Matches(line, @"::([A-Za-z_]\w*)\(");
            for (var c = 0; c < callMatches.Count; c++)
            {
                var token = callMatches[c].Groups[1].Value;
                if (string.IsNullOrEmpty(token)) continue;
                if (token.StartsWith("get_", StringComparison.Ordinal) || token.StartsWith("set_", StringComparison.Ordinal)) continue;
                if (!tokens.Contains(token)) tokens.Add(token);
            }
        }

        private static void TryCollectCtorToken(string ilLine, HashSet<string> ctorTypes)
        {
            if (string.IsNullOrEmpty(ilLine) || ctorTypes == null) return;

            var match = Regex.Match(ilLine, @"newobj\s+System\.Void\s+([^\s:]+)::\.ctor", RegexOptions.IgnoreCase);
            if (!match.Success) return;

            var rawType = match.Groups[1].Value ?? string.Empty;
            if (string.IsNullOrEmpty(rawType)) return;

            var normalized = rawType.Replace("/", ".").Replace("+", ".");
            var tick = normalized.IndexOf('`');
            if (tick > 0) normalized = normalized.Substring(0, tick);

            var shortType = normalized;
            var lastDot = normalized.LastIndexOf('.');
            if (lastDot >= 0 && lastDot < normalized.Length - 1)
            {
                shortType = normalized.Substring(lastDot + 1);
            }

            if (!string.IsNullOrEmpty(shortType))
            {
                ctorTypes.Add(shortType);
            }
        }

        private static string BuildConstructorLiteralPattern(string ctorToken)
        {
            if (string.IsNullOrEmpty(ctorToken) || !ctorToken.StartsWith("__CTOR__", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var typeName = ctorToken.Substring("__CTOR__".Length);
            if (string.IsNullOrEmpty(typeName)) return string.Empty;
            return @"new\s+" + Regex.Escape(typeName) + @"\s*\((?:[^()]|\([^()]*\))*\)";
        }

        private static List<string> ExtractTokensFromILLine(string line)
        {
            var tokens = new List<string>();
            if (string.IsNullOrEmpty(line)) return tokens;

            CollectMemberTokens(line, tokens);

            var fieldMatches = Regex.Matches(line, @"::([A-Za-z_]\w*)\b");
            for (var f = 0; f < fieldMatches.Count; f++)
            {
                var token = fieldMatches[f].Groups[1].Value;
                if (string.IsNullOrEmpty(token)) continue;
                if (token == ".ctor" || token == ".cctor") continue;
                if (!tokens.Contains(token)) tokens.Add(token);
            }

            return tokens;
        }

        private static string BuildSourceExpressionFromILLine(string ilLine)
        {
            if (string.IsNullOrEmpty(ilLine)) return string.Empty;

            var line = ilLine.Trim();
            var match = Regex.Match(line, @"::([A-Za-z_]\w*)\(");
            if (!match.Success) return string.Empty;

            var methodName = match.Groups[1].Value;
            var typeEnd = line.IndexOf("::", StringComparison.Ordinal);
            if (typeEnd < 0) return string.Empty;

            var left = line.Substring(0, typeEnd).Trim();
            var space = left.LastIndexOf(' ');
            var typeName = space >= 0 ? left.Substring(space + 1).Trim() : left;
            if (string.IsNullOrEmpty(typeName)) return string.Empty;

            return methodName.StartsWith("get_", StringComparison.Ordinal)
                ? typeName + "." + methodName.Substring(4)
                : typeName + "." + methodName + "()";
        }
    }
}
