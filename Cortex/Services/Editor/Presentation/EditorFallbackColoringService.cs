using System;
using Cortex.Contracts.Text;
using Cortex.Core.Models;

namespace Cortex.Services.Editor.Presentation
{
    internal sealed class EditorFallbackColoringService
    {
        private readonly EditorSemanticTokenMappingService _semanticTokenMappingService = new EditorSemanticTokenMappingService();

        public bool ShouldApplyFallback(string classification)
        {
            return _semanticTokenMappingService.IsGenericClassification(classification);
        }

        public string GetEffectiveCodeViewClassification(
            string classification,
            string rawText,
            string previousTokenText,
            string nextTokenText,
            string secondNextTokenText)
        {
            var key = _semanticTokenMappingService.NormalizeClassification(classification);
            if (!ShouldApplyFallback(key))
            {
                return key;
            }

            var token = NormalizeTokenText(rawText);
            if (!IsIdentifierLikeTokenText(token))
            {
                return key;
            }

            var previous = NormalizeTokenText(previousTokenText);
            var next = NormalizeTokenText(nextTokenText);
            var secondNext = NormalizeTokenText(secondNextTokenText);
            if (LooksLikeInvocation(next, secondNext))
            {
                return SemanticTokenClassificationNames.Method;
            }

            if (LooksLikeTypeReference(token, previous, next))
            {
                return SemanticTokenClassificationNames.Class;
            }

            return key;
        }

        public string GetEffectiveLineTokenClassification(
            string classification,
            string lineRawText,
            int tokenStartInLine,
            int tokenLength)
        {
            var safeLineText = lineRawText ?? string.Empty;
            var start = Math.Max(0, Math.Min(tokenStartInLine, safeLineText.Length));
            var length = Math.Max(0, Math.Min(tokenLength, safeLineText.Length - start));
            if (length <= 0)
            {
                return _semanticTokenMappingService.NormalizeClassification(classification);
            }

            var rawText = safeLineText.Substring(start, length);
            var key = _semanticTokenMappingService.NormalizeClassification(classification);
            if (!ShouldApplyFallback(key))
            {
                return key;
            }

            var token = NormalizeTokenText(rawText);
            if (!IsIdentifierLikeTokenText(token))
            {
                return key;
            }

            var previous = NormalizeTokenText(FindAdjacentTokenText(safeLineText, start, false, 1));
            var secondPrevious = NormalizeTokenText(FindAdjacentTokenText(safeLineText, start, false, 2));
            var next = NormalizeTokenText(FindAdjacentTokenText(safeLineText, start + length, true, 1));
            var secondNext = NormalizeTokenText(FindAdjacentTokenText(safeLineText, start + length, true, 2));

            if (LooksLikeNamespaceReference(token, previous, secondPrevious))
            {
                return SemanticTokenClassificationNames.Namespace;
            }

            if (LooksLikeAttributeType(token, previous, secondPrevious, next))
            {
                return SemanticTokenClassificationNames.Class;
            }

            if (LooksLikeTypeReferenceWithExtendedContext(token, previous, secondPrevious, next))
            {
                return SemanticTokenClassificationNames.Class;
            }

            if (LooksLikeInvocation(next, secondNext))
            {
                return SemanticTokenClassificationNames.Method;
            }

            if (LooksLikeStaticMember(previous, token, next))
            {
                return SemanticTokenClassificationNames.Property;
            }

            return key;
        }

        private static string NormalizeTokenText(string value)
        {
            return value != null ? value.Trim() : string.Empty;
        }

        private static bool LooksLikeInvocation(string nextTokenText, string secondNextTokenText)
        {
            if (string.Equals(nextTokenText, "(", StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(nextTokenText, "<", StringComparison.Ordinal) &&
                string.Equals(secondNextTokenText, "(", StringComparison.Ordinal);
        }

        private static bool LooksLikeTypeReference(string rawText, string previousTokenText, string nextTokenText)
        {
            if (string.IsNullOrEmpty(rawText) || !char.IsUpper(rawText[0]))
            {
                return false;
            }

            if (string.Equals(nextTokenText, ".", StringComparison.Ordinal))
            {
                return true;
            }

            switch (previousTokenText)
            {
                case "new":
                case "typeof":
                case "is":
                case "as":
                case ":":
                case "[":
                case ",":
                    return true;
                default:
                    return false;
            }
        }

        private static bool LooksLikeTypeReferenceWithExtendedContext(string rawText, string previousTokenText, string secondPreviousTokenText, string nextTokenText)
        {
            if (LooksLikeTypeReference(rawText, previousTokenText, nextTokenText))
            {
                return true;
            }

            if (string.IsNullOrEmpty(rawText) || !char.IsUpper(rawText[0]))
            {
                return false;
            }

            return string.Equals(previousTokenText, "(", StringComparison.Ordinal) &&
                (string.Equals(secondPreviousTokenText, "typeof", StringComparison.Ordinal) ||
                 string.Equals(secondPreviousTokenText, "default", StringComparison.Ordinal));
        }

        private static bool LooksLikeNamespaceReference(string rawText, string previousTokenText, string secondPreviousTokenText)
        {
            if (string.IsNullOrEmpty(rawText) || !IsIdentifierLikeTokenText(rawText))
            {
                return false;
            }

            if (string.Equals(previousTokenText, "namespace", StringComparison.Ordinal) ||
                string.Equals(previousTokenText, "using", StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(previousTokenText, ".", StringComparison.Ordinal) &&
                (string.Equals(secondPreviousTokenText, "namespace", StringComparison.Ordinal) ||
                 string.Equals(secondPreviousTokenText, "using", StringComparison.Ordinal));
        }

        private static bool LooksLikeAttributeType(string rawText, string previousTokenText, string secondPreviousTokenText, string nextTokenText)
        {
            if (string.IsNullOrEmpty(rawText) || !char.IsUpper(rawText[0]))
            {
                return false;
            }

            if (rawText.EndsWith("Attribute", StringComparison.Ordinal))
            {
                return true;
            }

            if (string.Equals(previousTokenText, "[", StringComparison.Ordinal))
            {
                return true;
            }

            return string.Equals(previousTokenText, ".", StringComparison.Ordinal) &&
                string.Equals(secondPreviousTokenText, "[", StringComparison.Ordinal) &&
                string.Equals(nextTokenText, "(", StringComparison.Ordinal);
        }

        private static bool LooksLikeStaticMember(string previousTokenText, string rawText, string nextTokenText)
        {
            if (string.IsNullOrEmpty(rawText) || !IsIdentifierLikeTokenText(rawText))
            {
                return false;
            }

            if (!string.Equals(previousTokenText, ".", StringComparison.Ordinal) ||
                string.Equals(nextTokenText, "(", StringComparison.Ordinal))
            {
                return false;
            }

            return !char.IsUpper(rawText[0]);
        }

        private static bool IsIdentifierLikeTokenText(string rawText)
        {
            if (string.IsNullOrEmpty(rawText) || IsReservedHoverKeyword(rawText))
            {
                return false;
            }

            var first = rawText[0];
            if (!(char.IsLetter(first) || first == '_' || first == '@'))
            {
                return false;
            }

            for (var i = 1; i < rawText.Length; i++)
            {
                var current = rawText[i];
                if (!(char.IsLetterOrDigit(current) || current == '_'))
                {
                    return false;
                }
            }

            return true;
        }

        private static string FindAdjacentTokenText(string lineRawText, int anchorIndex, bool forward, int ordinal)
        {
            var text = lineRawText ?? string.Empty;
            if (text.Length == 0 || ordinal <= 0)
            {
                return string.Empty;
            }

            var index = Math.Max(0, Math.Min(anchorIndex, text.Length));
            var remaining = ordinal;
            while (forward ? index < text.Length : index > 0)
            {
                if (forward)
                {
                    while (index < text.Length && char.IsWhiteSpace(text[index]))
                    {
                        index++;
                    }

                    if (index >= text.Length)
                    {
                        break;
                    }

                    var tokenStart = index;
                    var tokenKind = ClassifyCharacter(text[index]);
                    index++;
                    while (tokenKind != CharacterKind.Punctuation &&
                        index < text.Length &&
                        ClassifyCharacter(text[index]) == tokenKind)
                    {
                        index++;
                    }

                    remaining--;
                    if (remaining == 0)
                    {
                        return text.Substring(tokenStart, index - tokenStart);
                    }
                }
                else
                {
                    while (index > 0 && char.IsWhiteSpace(text[index - 1]))
                    {
                        index--;
                    }

                    if (index <= 0)
                    {
                        break;
                    }

                    var tokenEnd = index;
                    var tokenKind = ClassifyCharacter(text[index - 1]);
                    index--;
                    while (tokenKind != CharacterKind.Punctuation &&
                        index > 0 &&
                        ClassifyCharacter(text[index - 1]) == tokenKind)
                    {
                        index--;
                    }

                    remaining--;
                    if (remaining == 0)
                    {
                        return text.Substring(index, tokenEnd - index);
                    }
                }
            }

            return string.Empty;
        }

        private static CharacterKind ClassifyCharacter(char c)
        {
            if (char.IsWhiteSpace(c))
            {
                return CharacterKind.Whitespace;
            }

            return char.IsLetterOrDigit(c) || c == '_'
                ? CharacterKind.Word
                : CharacterKind.Punctuation;
        }

        private static bool IsReservedHoverKeyword(string token)
        {
            switch (token)
            {
                case "abstract":
                case "as":
                case "base":
                case "break":
                case "case":
                case "catch":
                case "checked":
                case "class":
                case "const":
                case "continue":
                case "default":
                case "delegate":
                case "do":
                case "else":
                case "enum":
                case "event":
                case "explicit":
                case "extern":
                case "false":
                case "finally":
                case "fixed":
                case "for":
                case "foreach":
                case "goto":
                case "if":
                case "implicit":
                case "in":
                case "interface":
                case "internal":
                case "is":
                case "lock":
                case "namespace":
                case "new":
                case "null":
                case "operator":
                case "out":
                case "override":
                case "params":
                case "private":
                case "protected":
                case "public":
                case "readonly":
                case "ref":
                case "return":
                case "sealed":
                case "sizeof":
                case "stackalloc":
                case "static":
                case "struct":
                case "switch":
                case "this":
                case "throw":
                case "true":
                case "try":
                case "typeof":
                case "unchecked":
                case "unsafe":
                case "using":
                case "virtual":
                case "volatile":
                case "while":
                    return true;
                default:
                    return false;
            }
        }

        private enum CharacterKind
        {
            Word,
            Whitespace,
            Punctuation
        }
    }
}
