using System;
using System.Collections.Generic;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public static class CompletionAugmentationDeclarationBuilder
    {
        public static string[] BuildDeclarations(DocumentSession session, int absolutePosition)
        {
            if (session == null)
            {
                return new string[0];
            }

            var declarations = new List<string>();
            var documentText = session.Text ?? string.Empty;
            var prefixText = BuildPrefixText(documentText, absolutePosition);

            AppendUsingDirectives(documentText, declarations);
            TryAddDeclaration(declarations, FindNearestNamespaceDeclaration(prefixText));
            TryAddDeclaration(declarations, FindNearestTypeDeclaration(prefixText));
            TryAddDeclaration(declarations, FindNearestMemberDeclaration(prefixText));

            return declarations.ToArray();
        }

        private static string BuildPrefixText(string text, int absolutePosition)
        {
            var value = text ?? string.Empty;
            var caret = Math.Max(0, Math.Min(value.Length, absolutePosition));
            return caret > 0 ? value.Substring(0, caret) : string.Empty;
        }

        private static void AppendUsingDirectives(string text, ICollection<string> declarations)
        {
            if (declarations == null || string.IsNullOrEmpty(text))
            {
                return;
            }

            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            var added = 0;
            for (var i = 0; i < lines.Length && added < 12; i++)
            {
                var trimmed = (lines[i] ?? string.Empty).Trim();
                if (!trimmed.StartsWith("using ", StringComparison.Ordinal) || !trimmed.EndsWith(";", StringComparison.Ordinal))
                {
                    continue;
                }

                TryAddDeclaration(declarations, trimmed);
                added++;
            }
        }

        private static string FindNearestNamespaceDeclaration(string prefixText)
        {
            return FindNearestLine(prefixText, delegate(string trimmed)
            {
                return trimmed.StartsWith("namespace ", StringComparison.Ordinal);
            });
        }

        private static string FindNearestTypeDeclaration(string prefixText)
        {
            return FindNearestLine(prefixText, delegate(string trimmed)
            {
                return ContainsKeyword(trimmed, " class ") ||
                    ContainsKeyword(trimmed, " struct ") ||
                    ContainsKeyword(trimmed, " interface ") ||
                    ContainsKeyword(trimmed, " enum ") ||
                    ContainsKeyword(trimmed, " record ") ||
                    trimmed.StartsWith("class ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("struct ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("interface ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("enum ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("record ", StringComparison.Ordinal);
            });
        }

        private static string FindNearestMemberDeclaration(string prefixText)
        {
            return FindNearestLine(prefixText, delegate(string trimmed)
            {
                if (string.IsNullOrEmpty(trimmed) ||
                    trimmed.StartsWith("//", StringComparison.Ordinal) ||
                    trimmed.StartsWith("if ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("if(", StringComparison.Ordinal) ||
                    trimmed.StartsWith("for ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("for(", StringComparison.Ordinal) ||
                    trimmed.StartsWith("foreach ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("foreach(", StringComparison.Ordinal) ||
                    trimmed.StartsWith("while ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("while(", StringComparison.Ordinal) ||
                    trimmed.StartsWith("switch ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("switch(", StringComparison.Ordinal) ||
                    trimmed.StartsWith("catch ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("catch(", StringComparison.Ordinal))
                {
                    return false;
                }

                if (trimmed.IndexOf('(') < 0)
                {
                    return false;
                }

                return trimmed.StartsWith("public ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("private ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("protected ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("internal ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("static ", StringComparison.Ordinal) ||
                    trimmed.StartsWith("async ", StringComparison.Ordinal) ||
                    ContainsKeyword(trimmed, " public ") ||
                    ContainsKeyword(trimmed, " private ") ||
                    ContainsKeyword(trimmed, " protected ") ||
                    ContainsKeyword(trimmed, " internal ") ||
                    ContainsKeyword(trimmed, " static ") ||
                    ContainsKeyword(trimmed, " async ");
            });
        }

        private static string FindNearestLine(string prefixText, Func<string, bool> predicate)
        {
            if (string.IsNullOrEmpty(prefixText) || predicate == null)
            {
                return string.Empty;
            }

            var lines = prefixText.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (var i = lines.Length - 1; i >= 0; i--)
            {
                var trimmed = (lines[i] ?? string.Empty).Trim();
                if (predicate(trimmed))
                {
                    return trimmed;
                }
            }

            return string.Empty;
        }

        private static bool ContainsKeyword(string text, string keyword)
        {
            return !string.IsNullOrEmpty(text) &&
                !string.IsNullOrEmpty(keyword) &&
                text.IndexOf(keyword, StringComparison.Ordinal) >= 0;
        }

        private static void TryAddDeclaration(ICollection<string> declarations, string value)
        {
            if (declarations == null || string.IsNullOrEmpty(value))
            {
                return;
            }

            foreach (var existing in declarations)
            {
                if (string.Equals(existing ?? string.Empty, value, StringComparison.Ordinal))
                {
                    return;
                }
            }

            declarations.Add(value);
        }
    }
}
