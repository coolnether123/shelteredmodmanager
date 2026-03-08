using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace ModAPI.Internal.DebugUI
{
    internal static class UIDebugSourceMethodLocator
    {
        public static string ExtractMethodNameFromSelectedId(string methodId)
        {
            if (string.IsNullOrEmpty(methodId)) return string.Empty;
            var normalized = methodId.Trim();
            var paren = normalized.IndexOf('(');
            if (paren > 0) normalized = normalized.Substring(0, paren);
            var dot = normalized.LastIndexOf('.');
            return dot >= 0 && dot < normalized.Length - 1 ? normalized.Substring(dot + 1) : normalized;
        }

        public static bool TryFindMethodBodySpan(string source, string methodName, out int bodyStart, out int bodyLength)
        {
            bodyStart = 0;
            bodyLength = 0;
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(methodName)) return false;

            var match = FindMethodDeclaration(source, methodName);
            if (!match.Success) return false;

            var open = source.IndexOf('{', match.Index);
            if (open < 0) return false;

            int close;
            if (!TryFindMatchingBrace(source, open, out close)) return false;

            bodyStart = open + 1;
            bodyLength = close - bodyStart;
            return bodyLength > 0;
        }

        public static bool TryGetMethodBodyLineRange(List<string> lines, string methodName, out int startLine, out int endLine)
        {
            startLine = 0;
            endLine = lines != null && lines.Count > 0 ? lines.Count - 1 : 0;

            int openLine;
            int closeLine;
            if (!TryLocateMethodBodyLines(lines, methodName, out openLine, out closeLine))
            {
                return false;
            }

            startLine = Math.Min(openLine + 1, lines.Count - 1);
            endLine = closeLine > openLine ? Math.Max(startLine, closeLine - 1) : lines.Count - 1;
            return true;
        }

        public static int FindMethodBodyInsertLine(List<string> lines, string methodName)
        {
            int openLine;
            int closeLine;
            if (!TryLocateMethodBodyLines(lines, methodName, out openLine, out closeLine))
            {
                return -1;
            }

            return Math.Min(openLine + 1, lines.Count);
        }

        public static int FindFirstBlockInsertLine(List<string> lines)
        {
            if (lines == null || lines.Count == 0) return 0;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line != null && line.IndexOf("{", StringComparison.Ordinal) >= 0)
                {
                    return Math.Min(i + 1, lines.Count);
                }
            }

            return lines.Count;
        }

        public static string GuessIndentation(List<string> lines, int insertLine)
        {
            if (lines == null || lines.Count == 0) return "    ";
            var probeStart = Math.Max(0, insertLine - 1);
            var probeEnd = Math.Min(lines.Count - 1, insertLine + 3);
            for (var i = probeStart; i <= probeEnd; i++)
            {
                var line = lines[i];
                if (string.IsNullOrEmpty(line)) continue;
                var trimmed = line.TrimStart();
                if (trimmed.Length == 0 || trimmed.StartsWith("}", StringComparison.Ordinal)) continue;

                var indentLength = line.Length - trimmed.Length;
                if (indentLength > 0)
                {
                    return line.Substring(0, indentLength);
                }
            }

            return "    ";
        }

        public static string GuessIndentationForAt(string source, int index)
        {
            if (string.IsNullOrEmpty(source) || index < 0 || index >= source.Length) return "    ";
            var lineStart = source.LastIndexOf('\n', index);
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            var lineEnd = source.IndexOf('\n', lineStart);
            if (lineEnd < 0) lineEnd = source.Length;
            var fullLine = source.Substring(lineStart, lineEnd - lineStart);
            var trimmed = fullLine.TrimStart();
            return fullLine.Substring(0, fullLine.Length - trimmed.Length);
        }

        private static bool TryLocateMethodBodyLines(List<string> lines, string methodName, out int openLine, out int closeLine)
        {
            openLine = -1;
            closeLine = -1;
            if (lines == null || lines.Count == 0 || string.IsNullOrEmpty(methodName)) return false;

            var declarationRegex = BuildMethodDeclarationRegex(methodName);
            var declarationSeen = false;
            var depth = 0;

            for (var i = 0; i < lines.Count; i++)
            {
                var line = lines[i] ?? string.Empty;
                if (!declarationSeen)
                {
                    if (!declarationRegex.IsMatch(line)) continue;
                    declarationSeen = true;
                }

                for (var c = 0; c < line.Length; c++)
                {
                    var ch = line[c];
                    if (ch == '{')
                    {
                        depth++;
                        if (openLine < 0)
                        {
                            openLine = i;
                        }
                    }
                    else if (ch == '}' && depth > 0)
                    {
                        depth--;
                        if (openLine >= 0 && depth == 0)
                        {
                            closeLine = i;
                            return true;
                        }
                    }
                }
            }

            return openLine >= 0;
        }

        private static Match FindMethodDeclaration(string source, string methodName)
        {
            var preferred = BuildMethodDeclarationRegex(methodName).Match(source);
            if (preferred.Success) return preferred;
            return new Regex(@"\b" + Regex.Escape(methodName) + @"\s*\(", RegexOptions.Multiline).Match(source);
        }

        private static Regex BuildMethodDeclarationRegex(string methodName)
        {
            return new Regex(
                @"^\s*(?:public|private|protected|internal)\s+[^=\r\n;]*\b" + Regex.Escape(methodName) + @"\s*\(",
                RegexOptions.Multiline);
        }

        private static bool TryFindMatchingBrace(string source, int openIndex, out int closeIndex)
        {
            closeIndex = -1;
            var depth = 0;
            for (var i = openIndex; i < source.Length; i++)
            {
                var ch = source[i];
                if (ch == '{') depth++;
                else if (ch == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        closeIndex = i;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
