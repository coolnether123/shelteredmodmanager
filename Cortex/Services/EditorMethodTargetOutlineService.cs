using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class EditorMethodTargetOutlineService
    {
        private const int MaxHeaderSearchLines = 8;

        private readonly EditorClassificationPresentationService _classificationPresentationService = new EditorClassificationPresentationService();
        private string _cachedKey = string.Empty;
        private EditorMethodTargetOutline[] _cachedOutlines = new EditorMethodTargetOutline[0];

        public EditorMethodTargetOutline[] GetOutlines(DocumentSession session)
        {
            var cacheKey = BuildCacheKey(session);
            if (string.Equals(_cachedKey, cacheKey, StringComparison.Ordinal))
            {
                return _cachedOutlines;
            }

            _cachedKey = cacheKey;
            _cachedOutlines = BuildOutlines(session);
            return _cachedOutlines;
        }

        public EditorMethodTargetOutline FindOutline(DocumentSession session, EditorCommandTarget target)
        {
            if (session == null || target == null)
            {
                return null;
            }

            var outlines = GetOutlines(session);
            for (var i = 0; i < outlines.Length; i++)
            {
                var outline = outlines[i];
                if (outline == null)
                {
                    continue;
                }

                if (outline.AnchorStart == target.AbsolutePosition &&
                    outline.AnchorLineNumber == target.Line &&
                    string.Equals(outline.SymbolText ?? string.Empty, target.SymbolText ?? string.Empty, StringComparison.Ordinal))
                {
                    return outline;
                }
            }

            for (var i = 0; i < outlines.Length; i++)
            {
                var outline = outlines[i];
                if (outline == null)
                {
                    continue;
                }

                if (outline.AnchorLineNumber == target.Line &&
                    string.Equals(outline.SymbolText ?? string.Empty, target.SymbolText ?? string.Empty, StringComparison.Ordinal))
                {
                    return outline;
                }
            }

            return null;
        }

        private EditorMethodTargetOutline[] BuildOutlines(DocumentSession session)
        {
            if (session == null || string.IsNullOrEmpty(session.Text))
            {
                return new EditorMethodTargetOutline[0];
            }

            var lines = BuildLines(session.Text);
            if (lines.Length == 0)
            {
                return new EditorMethodTargetOutline[0];
            }

            var candidatesByLine = BuildCandidatesByLine(session.Text, lines, session.LanguageAnalysis != null ? session.LanguageAnalysis.Classifications : null);
            var outlines = new List<EditorMethodTargetOutline>();
            var braceStack = new Stack<BraceStart>();
            for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                var line = lines[lineIndex];
                var sanitized = SanitizeLineForBraceScan(line.RawText);
                for (var charIndex = 0; charIndex < sanitized.Length; charIndex++)
                {
                    var c = sanitized[charIndex];
                    if (c == '{')
                    {
                        braceStack.Push(new BraceStart
                        {
                            HeaderLineNumber = ResolveBraceHeaderLine(lines, lineIndex, charIndex),
                            OpenLineNumber = line.LineNumber,
                            OpenBraceColumn = charIndex
                        });
                    }
                    else if (c == '}' && braceStack.Count > 0)
                    {
                        var braceStart = braceStack.Pop();
                        EditorMethodTargetOutline outline;
                        if (TryCreateOutline(lines, candidatesByLine, braceStart, line.LineNumber, out outline))
                        {
                            outlines.Add(outline);
                        }
                    }
                }
            }

            return outlines.ToArray();
        }

        private Dictionary<int, List<SymbolCandidate>> BuildCandidatesByLine(string text, LineInfo[] lines, LanguageServiceClassifiedSpan[] spans)
        {
            var result = new Dictionary<int, List<SymbolCandidate>>();
            if (string.IsNullOrEmpty(text) || lines == null || lines.Length == 0 || spans == null || spans.Length == 0)
            {
                return result;
            }

            for (var i = 0; i < spans.Length; i++)
            {
                var span = spans[i];
                if (span == null || span.Length <= 0 || span.Start < 0 || span.Start >= text.Length)
                {
                    continue;
                }

                var lineIndex = FindLineIndex(lines, span.Start);
                if (lineIndex < 0 || lineIndex >= lines.Length)
                {
                    continue;
                }

                var line = lines[lineIndex];
                var maxLength = Math.Max(0, line.EndOffset - span.Start);
                var length = Math.Min(span.Length, maxLength);
                if (length <= 0)
                {
                    continue;
                }

                var symbolText = text.Substring(span.Start, length).Trim();
                if (string.IsNullOrEmpty(symbolText) ||
                    symbolText.IndexOf('\r') >= 0 ||
                    symbolText.IndexOf('\n') >= 0)
                {
                    continue;
                }

                var resolvedClassification = _classificationPresentationService.ResolvePresentationClassification(
                    span.Classification,
                    span.SemanticTokenType);
                if (!CanInspectSymbol(symbolText, resolvedClassification))
                {
                    continue;
                }

                List<SymbolCandidate> bucket;
                if (!result.TryGetValue(line.LineNumber, out bucket))
                {
                    bucket = new List<SymbolCandidate>();
                    result[line.LineNumber] = bucket;
                }

                bucket.Add(new SymbolCandidate
                {
                    LineNumber = line.LineNumber,
                    Start = span.Start,
                    Length = length,
                    SymbolText = symbolText,
                    Classification = resolvedClassification
                });
            }

            return result;
        }

        private bool CanInspectSymbol(string symbolText, string classification)
        {
            if (string.IsNullOrEmpty(symbolText))
            {
                return false;
            }

            var normalizedClassification = _classificationPresentationService.NormalizeClassification(classification);
            return !string.IsNullOrEmpty(normalizedClassification) &&
                (normalizedClassification.IndexOf("method", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 normalizedClassification.IndexOf("property", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 normalizedClassification.IndexOf("event", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private bool TryCreateOutline(
            LineInfo[] lines,
            Dictionary<int, List<SymbolCandidate>> candidatesByLine,
            BraceStart braceStart,
            int endLineNumber,
            out EditorMethodTargetOutline outline)
        {
            outline = null;
            var candidate = FindDeclarationCandidate(lines, candidatesByLine, braceStart);
            if (candidate == null)
            {
                return false;
            }

            var declarationStartLine = FindDeclarationStartLine(lines, braceStart, candidate.LineNumber);
            var declarationText = BuildDeclarationText(lines, declarationStartLine, braceStart.OpenLineNumber, braceStart.OpenBraceColumn);
            if (!LooksLikeMethodDeclaration(declarationText))
            {
                return false;
            }

            outline = new EditorMethodTargetOutline();
            outline.StartLineNumber = declarationStartLine;
            outline.EndLineNumber = endLineNumber;
            outline.HeaderLineNumber = braceStart.HeaderLineNumber;
            outline.AnchorLineNumber = candidate.LineNumber;
            outline.AnchorStart = candidate.Start;
            outline.AnchorLength = candidate.Length;
            outline.SymbolText = candidate.SymbolText;
            outline.Classification = candidate.Classification;
            return true;
        }

        private SymbolCandidate FindDeclarationCandidate(LineInfo[] lines, Dictionary<int, List<SymbolCandidate>> candidatesByLine, BraceStart braceStart)
        {
            if (lines == null || candidatesByLine == null)
            {
                return null;
            }

            var minimumLine = Math.Max(1, braceStart.HeaderLineNumber - MaxHeaderSearchLines);
            for (var lineNumber = braceStart.HeaderLineNumber; lineNumber >= minimumLine; lineNumber--)
            {
                var line = GetLine(lines, lineNumber);
                if (line == null)
                {
                    continue;
                }

                if (lineNumber < braceStart.HeaderLineNumber && string.IsNullOrEmpty((line.RawText ?? string.Empty).Trim()))
                {
                    break;
                }

                List<SymbolCandidate> candidates;
                if (!candidatesByLine.TryGetValue(lineNumber, out candidates) || candidates == null || candidates.Count == 0)
                {
                    continue;
                }

                for (var i = 0; i < candidates.Count; i++)
                {
                    var candidate = candidates[i];
                    if (candidate == null)
                    {
                        continue;
                    }

                    if (candidate.Start < line.StartOffset || candidate.Start >= line.EndOffset)
                    {
                        continue;
                    }

                    return candidate;
                }
            }

            return null;
        }

        private static int FindDeclarationStartLine(LineInfo[] lines, BraceStart braceStart, int candidateLineNumber)
        {
            var startLine = Math.Max(1, candidateLineNumber);
            var minimumLine = Math.Max(1, braceStart.HeaderLineNumber - MaxHeaderSearchLines);
            for (var lineNumber = candidateLineNumber - 1; lineNumber >= minimumLine; lineNumber--)
            {
                var line = GetLine(lines, lineNumber);
                if (line == null)
                {
                    break;
                }

                var trimmed = (line.RawText ?? string.Empty).Trim();
                if (trimmed.Length == 0 ||
                    trimmed == "{" ||
                    trimmed == "}" ||
                    trimmed.EndsWith(";", StringComparison.Ordinal))
                {
                    break;
                }

                startLine = lineNumber;
            }

            return startLine;
        }

        private static string BuildDeclarationText(LineInfo[] lines, int startLineNumber, int openLineNumber, int openBraceColumn)
        {
            if (lines == null || lines.Length == 0)
            {
                return string.Empty;
            }

            var parts = new List<string>();
            for (var lineNumber = startLineNumber; lineNumber <= openLineNumber; lineNumber++)
            {
                var line = GetLine(lines, lineNumber);
                if (line == null)
                {
                    continue;
                }

                var raw = line.RawText ?? string.Empty;
                if (lineNumber == openLineNumber && openBraceColumn >= 0 && openBraceColumn <= raw.Length)
                {
                    raw = raw.Substring(0, openBraceColumn);
                }

                var trimmed = raw.Trim();
                if (trimmed.Length > 0)
                {
                    parts.Add(trimmed);
                }
            }

            return string.Join(" ", parts.ToArray());
        }

        private static bool LooksLikeMethodDeclaration(string declarationText)
        {
            if (string.IsNullOrEmpty(declarationText))
            {
                return false;
            }

            var normalized = declarationText.Trim();
            if (normalized.Length == 0 ||
                normalized.IndexOf("=>", StringComparison.Ordinal) >= 0 ||
                normalized.IndexOf("(", StringComparison.Ordinal) < 0 ||
                normalized.IndexOf(")", StringComparison.Ordinal) < 0 ||
                normalized.IndexOf(";", StringComparison.Ordinal) >= 0)
            {
                return false;
            }

            var lowered = normalized.ToLowerInvariant();
            return !StartsWithControlBlock(lowered);
        }

        private static bool StartsWithControlBlock(string loweredText)
        {
            return StartsWithWord(loweredText, "if") ||
                StartsWithWord(loweredText, "for") ||
                StartsWithWord(loweredText, "foreach") ||
                StartsWithWord(loweredText, "while") ||
                StartsWithWord(loweredText, "switch") ||
                StartsWithWord(loweredText, "lock") ||
                StartsWithWord(loweredText, "using") ||
                StartsWithWord(loweredText, "catch") ||
                StartsWithWord(loweredText, "finally") ||
                StartsWithWord(loweredText, "else") ||
                StartsWithWord(loweredText, "do") ||
                StartsWithWord(loweredText, "try") ||
                StartsWithWord(loweredText, "checked") ||
                StartsWithWord(loweredText, "unchecked") ||
                StartsWithWord(loweredText, "unsafe") ||
                StartsWithWord(loweredText, "fixed");
        }

        private static bool StartsWithWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word) || !text.StartsWith(word, StringComparison.Ordinal))
            {
                return false;
            }

            if (text.Length == word.Length)
            {
                return true;
            }

            var next = text[word.Length];
            return char.IsWhiteSpace(next) || next == '(';
        }

        private static LineInfo[] BuildLines(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new LineInfo[0];
            }

            var lines = new List<LineInfo>();
            var start = 0;
            var lineNumber = 1;
            while (start <= text.Length)
            {
                var end = start;
                while (end < text.Length && text[end] != '\r' && text[end] != '\n')
                {
                    end++;
                }

                lines.Add(new LineInfo
                {
                    LineNumber = lineNumber,
                    StartOffset = start,
                    EndOffset = end,
                    RawText = end > start ? text.Substring(start, end - start) : string.Empty
                });

                if (end >= text.Length)
                {
                    break;
                }

                if (text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n')
                {
                    end++;
                }

                start = end + 1;
                lineNumber++;
            }

            return lines.ToArray();
        }

        private static int FindLineIndex(LineInfo[] lines, int absolutePosition)
        {
            if (lines == null || lines.Length == 0)
            {
                return -1;
            }

            var low = 0;
            var high = lines.Length - 1;
            while (low <= high)
            {
                var mid = low + ((high - low) / 2);
                var line = lines[mid];
                if (absolutePosition < line.StartOffset)
                {
                    high = mid - 1;
                }
                else if (absolutePosition > line.EndOffset)
                {
                    low = mid + 1;
                }
                else
                {
                    return mid;
                }
            }

            return Math.Max(0, Math.Min(lines.Length - 1, low));
        }

        private static LineInfo GetLine(LineInfo[] lines, int lineNumber)
        {
            if (lines == null || lineNumber <= 0)
            {
                return null;
            }

            var index = lineNumber - 1;
            return index >= 0 && index < lines.Length ? lines[index] : null;
        }

        private static string BuildCacheKey(DocumentSession session)
        {
            if (session == null)
            {
                return string.Empty;
            }

            var classificationCount = session.LanguageAnalysis != null && session.LanguageAnalysis.Classifications != null
                ? session.LanguageAnalysis.Classifications.Length
                : 0;
            return (session.FilePath ?? string.Empty) + "|" +
                session.TextVersion + "|" +
                session.LastLanguageAnalysisUtc.Ticks + "|" +
                classificationCount + "|" +
                (session.Text != null ? session.Text.Length : 0);
        }

        private static string SanitizeLineForBraceScan(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder(raw.Length);
            var inString = false;
            var quoteChar = '\0';
            for (var i = 0; i < raw.Length; i++)
            {
                var c = raw[i];
                if (!inString && c == '/' && i + 1 < raw.Length && raw[i + 1] == '/')
                {
                    break;
                }

                if ((c == '"' || c == '\'') && (i == 0 || raw[i - 1] != '\\'))
                {
                    if (inString && quoteChar == c)
                    {
                        inString = false;
                    }
                    else if (!inString)
                    {
                        inString = true;
                        quoteChar = c;
                    }
                }

                builder.Append(inString ? ' ' : c);
            }

            return builder.ToString();
        }

        private static int ResolveBraceHeaderLine(LineInfo[] lines, int currentLineIndex, int braceIndex)
        {
            if (lines == null || currentLineIndex < 0 || currentLineIndex >= lines.Length)
            {
                return currentLineIndex + 1;
            }

            var currentLine = lines[currentLineIndex];
            var raw = currentLine.RawText ?? string.Empty;
            var beforeBrace = braceIndex > 0 && braceIndex <= raw.Length ? raw.Substring(0, braceIndex) : string.Empty;
            if (!string.IsNullOrEmpty(beforeBrace.Trim()))
            {
                return currentLine.LineNumber;
            }

            for (var index = currentLineIndex - 1; index >= 0; index--)
            {
                var candidate = lines[index];
                if (candidate != null && !string.IsNullOrEmpty((candidate.RawText ?? string.Empty).Trim()))
                {
                    return candidate.LineNumber;
                }
            }

            return currentLine.LineNumber;
        }

        private sealed class LineInfo
        {
            public int LineNumber;
            public int StartOffset;
            public int EndOffset;
            public string RawText = string.Empty;
        }

        private sealed class SymbolCandidate
        {
            public int LineNumber;
            public int Start;
            public int Length;
            public string SymbolText = string.Empty;
            public string Classification = string.Empty;
        }

        private struct BraceStart
        {
            public int HeaderLineNumber;
            public int OpenLineNumber;
            public int OpenBraceColumn;
        }
    }

    internal sealed class EditorMethodTargetOutline
    {
        public int StartLineNumber;
        public int EndLineNumber;
        public int HeaderLineNumber;
        public int AnchorLineNumber;
        public int AnchorStart;
        public int AnchorLength;
        public string SymbolText = string.Empty;
        public string Classification = string.Empty;
    }
}
