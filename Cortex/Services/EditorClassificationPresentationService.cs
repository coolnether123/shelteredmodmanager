using System;
using UnityEngine;

namespace Cortex.Services
{
    internal sealed class EditorClassificationPresentationService
    {
        private const string DefaultTextHex = "#D4D4D4";
        private const string CommentHex = "#6A9955";
        private const string XmlHex = "#808080";
        private const string KeywordHex = "#569CD6";
        private const string TypeHex = "#4EC9B0";
        private const string NamespaceHex = "#C8C8C8";
        private const string MethodHex = "#DCDCAA";
        private const string ValueHex = "#9CDCFE";
        private const string StringHex = "#CE9178";
        private const string NumberHex = "#B5CEA8";
        private const string PreprocessorHex = "#C586C0";

        public string GetHexColor(string classification)
        {
            var key = NormalizeClassification(classification);
            if (key.Length == 0)
            {
                return DefaultTextHex;
            }

            if (key.Contains("comment"))
            {
                return CommentHex;
            }

            if (key.Contains("xml"))
            {
                return XmlHex;
            }

            if (key.Contains("keyword") || key.Contains("control"))
            {
                return KeywordHex;
            }

            if (key.Contains("class") ||
                key.Contains("struct") ||
                key.Contains("interface") ||
                key.Contains("enum") ||
                key.Contains("delegate") ||
                key.Contains("record") ||
                key.Contains("typeparameter"))
            {
                return TypeHex;
            }

            if (key.Contains("namespace"))
            {
                return NamespaceHex;
            }

            if (key.Contains("method") || key.Contains("property") || key.Contains("event"))
            {
                return MethodHex;
            }

            if (key.Contains("field") ||
                key.Contains("enum member") ||
                key.Contains("constant") ||
                key.Contains("parameter") ||
                key.Contains("local"))
            {
                return ValueHex;
            }

            if (key.Contains("string") || key.Contains("char"))
            {
                return StringHex;
            }

            if (key.Contains("numeric") || key.Contains("number"))
            {
                return NumberHex;
            }

            if (key.Contains("preprocessor"))
            {
                return PreprocessorHex;
            }

            return DefaultTextHex;
        }

        public Color GetColor(string classification)
        {
            return CortexIdeLayout.ParseColor(GetHexColor(classification), CortexIdeLayout.GetTextColor());
        }

        public string GetEffectiveCodeViewClassification(
            string classification,
            string rawText,
            string previousTokenText,
            string nextTokenText,
            string secondNextTokenText)
        {
            var key = NormalizeClassification(classification);
            if (!IsGenericIdentifierClassification(key))
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
                return "method";
            }

            if (LooksLikeTypeReference(token, previous, next))
            {
                return "class";
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
                return NormalizeClassification(classification);
            }

            var rawText = safeLineText.Substring(start, length);
            var key = NormalizeClassification(classification);
            if (!IsGenericIdentifierClassification(key))
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
                return "namespace";
            }

            if (LooksLikeAttributeType(token, previous, secondPrevious, next))
            {
                return "class";
            }

            if (LooksLikeTypeReferenceWithExtendedContext(token, previous, secondPrevious, next))
            {
                return "class";
            }

            if (LooksLikeInvocation(next, secondNext))
            {
                return "method";
            }

            if (LooksLikeStaticMember(previous, token, next))
            {
                return "property";
            }

            return key;
        }

        public bool IsHoverCandidate(string classification, string rawText)
        {
            var token = NormalizeTokenText(rawText);
            if (token.Length == 0)
            {
                return false;
            }

            var key = NormalizeClassification(classification);
            if (IsGenericIdentifierClassification(key))
            {
                return IsIdentifierLikeTokenText(token);
            }

            if (IsExcludedInteractionClassification(key))
            {
                return false;
            }

            if (key.Contains("keyword"))
            {
                return IsPredefinedTypeKeyword(token);
            }

            return IsSymbolClassification(key, true);
        }

        public bool CanNavigateToDefinition(string classification, string rawText)
        {
            var token = NormalizeTokenText(rawText);
            if (token.Length == 0)
            {
                return false;
            }

            var key = NormalizeClassification(classification);
            if (IsGenericIdentifierClassification(key))
            {
                return IsIdentifierLikeTokenText(token);
            }

            if (IsExcludedInteractionClassification(key) || !IsSymbolClassification(key, true))
            {
                return false;
            }

            return key.IndexOf("local", StringComparison.OrdinalIgnoreCase) < 0 &&
                key.IndexOf("parameter", StringComparison.OrdinalIgnoreCase) < 0;
        }

        public string NormalizeClassification(string classification)
        {
            return (classification ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static bool IsGenericIdentifierClassification(string key)
        {
            return string.IsNullOrEmpty(key) ||
                key.Contains("identifier") ||
                key.Contains("text");
        }

        private static bool IsExcludedInteractionClassification(string key)
        {
            return key.Contains("operator") ||
                key.Contains("punctuation") ||
                key.Contains("comment") ||
                key.Contains("xml") ||
                key.Contains("preprocessor") ||
                key.Contains("string") ||
                key.Contains("char") ||
                key.Contains("numeric") ||
                key.Contains("number");
        }

        private static bool IsSymbolClassification(string key, bool includeLocalsAndParameters)
        {
            if (key.Contains("class") ||
                key.Contains("struct") ||
                key.Contains("interface") ||
                key.Contains("enum") ||
                key.Contains("delegate") ||
                key.Contains("record") ||
                key.Contains("namespace") ||
                key.Contains("method") ||
                key.Contains("property") ||
                key.Contains("event") ||
                key.Contains("field") ||
                key.Contains("constant") ||
                key.Contains("enum member") ||
                key.Contains("typeparameter"))
            {
                return true;
            }

            return includeLocalsAndParameters &&
                (key.Contains("local") || key.Contains("parameter"));
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

        private static bool IsPredefinedTypeKeyword(string rawText)
        {
            switch (rawText)
            {
                case "bool":
                case "byte":
                case "sbyte":
                case "short":
                case "ushort":
                case "int":
                case "uint":
                case "long":
                case "ulong":
                case "nint":
                case "nuint":
                case "float":
                case "double":
                case "decimal":
                case "char":
                case "string":
                case "object":
                case "dynamic":
                case "void":
                    return true;
                default:
                    return false;
            }
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
