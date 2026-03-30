using System;
using System.IO;
using System.Text.RegularExpressions;

namespace Cortex.Services.Navigation.Source
{
    internal interface ISourceNavigationLineResolver
    {
        int ResolveLine(string sourceText, string symbolKind, string metadataName, string containingTypeName);
    }

    internal sealed class SourceNavigationLineResolver : ISourceNavigationLineResolver
    {
        public int ResolveLine(string sourceText, string symbolKind, string metadataName, string containingTypeName)
        {
            var lines = SplitLines(sourceText);
            if (lines == null || lines.Length == 0)
            {
                return 1;
            }

            var symbolName = GetNavigationSymbolName(metadataName, containingTypeName);
            if (string.IsNullOrEmpty(symbolName))
            {
                return 1;
            }

            var declarationPattern = BuildDeclarationPattern(symbolKind, symbolName, containingTypeName);
            if (!string.IsNullOrEmpty(declarationPattern))
            {
                var declarationLine = FindPatternLine(lines, declarationPattern);
                if (declarationLine > 0)
                {
                    return declarationLine;
                }
            }

            var symbolPattern = "\\b" + Regex.Escape(symbolName) + "\\b";
            var symbolLine = FindPatternLine(lines, symbolPattern);
            return symbolLine > 0 ? symbolLine : 1;
        }

        internal static string GetTypeLeafName(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return string.Empty;
            }

            var normalized = fullTypeName.Replace('+', '.');
            var lastDot = normalized.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < normalized.Length
                ? normalized.Substring(lastDot + 1)
                : normalized;
        }

        internal static string ReadAllTextSafe(string filePath)
        {
            try
            {
                return !string.IsNullOrEmpty(filePath) && File.Exists(filePath)
                    ? File.ReadAllText(filePath)
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string[] SplitLines(string sourceText)
        {
            return (sourceText ?? string.Empty)
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Split('\n');
        }

        private static int FindPatternLine(string[] lines, string pattern)
        {
            if (lines == null || lines.Length == 0 || string.IsNullOrEmpty(pattern))
            {
                return 0;
            }

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i] ?? string.Empty;
                if (Regex.IsMatch(line, pattern))
                {
                    return i + 1;
                }
            }

            return 0;
        }

        private static string GetNavigationSymbolName(string metadataName, string containingTypeName)
        {
            if (!string.IsNullOrEmpty(metadataName))
            {
                return metadataName;
            }

            if (string.IsNullOrEmpty(containingTypeName))
            {
                return string.Empty;
            }

            var lastDot = containingTypeName.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < containingTypeName.Length
                ? containingTypeName.Substring(lastDot + 1)
                : containingTypeName;
        }

        private static string BuildDeclarationPattern(string symbolKind, string symbolName, string containingTypeName)
        {
            var normalizedKind = (symbolKind ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(symbolName))
            {
                return string.Empty;
            }

            if (IsTypeLikeSymbol(normalizedKind))
            {
                return "\\b(class|struct|interface|enum|delegate|record)\\s+" + Regex.Escape(symbolName) + "\\b";
            }

            if (string.Equals(normalizedKind, "Method", StringComparison.OrdinalIgnoreCase))
            {
                return "\\b" + Regex.Escape(symbolName) + "\\s*(<[^>]+>\\s*)?\\(";
            }

            if (string.Equals(normalizedKind, "Constructor", StringComparison.OrdinalIgnoreCase))
            {
                var typeName = !string.IsNullOrEmpty(containingTypeName)
                    ? GetNavigationSymbolName(string.Empty, containingTypeName)
                    : GetNavigationSymbolName(symbolName, containingTypeName);
                return "\\b" + Regex.Escape(typeName) + "\\s*\\(";
            }

            if (string.Equals(normalizedKind, "Property", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedKind, "Event", StringComparison.OrdinalIgnoreCase))
            {
                return "\\b" + Regex.Escape(symbolName) + "\\b[^\\n]*\\{";
            }

            if (string.Equals(normalizedKind, "Field", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedKind, "EnumMember", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedKind, "Constant", StringComparison.OrdinalIgnoreCase))
            {
                return "\\b" + Regex.Escape(symbolName) + "\\b\\s*(=|;|,)";
            }

            return "\\b" + Regex.Escape(symbolName) + "\\b";
        }

        private static bool IsTypeLikeSymbol(string symbolKind)
        {
            return string.Equals(symbolKind, "NamedType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Class", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Struct", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Interface", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Enum", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Delegate", StringComparison.OrdinalIgnoreCase);
        }
    }
}
