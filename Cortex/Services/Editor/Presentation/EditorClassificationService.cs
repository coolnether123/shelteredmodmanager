using System;
using Cortex.Contracts.Text;

namespace Cortex.Services.Editor.Presentation
{
    internal sealed class EditorClassificationService
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

        private readonly EditorSemanticTokenMappingService _semanticTokenMappingService = new EditorSemanticTokenMappingService();

        public string GetHexColor(string classification)
        {
            return GetHexColor(classification, string.Empty);
        }

        public string GetHexColor(string classification, string semanticTokenType)
        {
            var key = ResolvePresentationClassification(classification, semanticTokenType);
            switch (key)
            {
                case SemanticTokenClassificationNames.Comment:
                    return CommentHex;
                case SemanticTokenClassificationNames.Xml:
                    return XmlHex;
                case SemanticTokenClassificationNames.Keyword:
                    return KeywordHex;
                case SemanticTokenClassificationNames.Class:
                case SemanticTokenClassificationNames.Type:
                    return TypeHex;
                case SemanticTokenClassificationNames.Namespace:
                    return NamespaceHex;
                case SemanticTokenClassificationNames.Method:
                case SemanticTokenClassificationNames.Property:
                case SemanticTokenClassificationNames.Event:
                    return MethodHex;
                case SemanticTokenClassificationNames.Field:
                case SemanticTokenClassificationNames.Variable:
                case SemanticTokenClassificationNames.Parameter:
                case SemanticTokenClassificationNames.Local:
                    return ValueHex;
                case SemanticTokenClassificationNames.String:
                    return StringHex;
                case SemanticTokenClassificationNames.Number:
                    return NumberHex;
                case SemanticTokenClassificationNames.Preprocessor:
                    return PreprocessorHex;
                default:
                    return DefaultTextHex;
            }
        }

        public bool IsHoverCandidate(string classification, string rawText)
        {
            return IsHoverCandidate(classification, string.Empty, rawText);
        }

        public bool IsHoverCandidate(string classification, string semanticTokenType, string rawText)
        {
            var token = NormalizeTokenText(rawText);
            if (token.Length == 0)
            {
                return false;
            }

            var key = ResolvePresentationClassification(classification, semanticTokenType);
            if (_semanticTokenMappingService.IsGenericClassification(key))
            {
                return IsIdentifierLikeTokenText(token);
            }

            if (IsExcludedInteractionClassification(key))
            {
                return false;
            }

            if (string.Equals(key, SemanticTokenClassificationNames.Keyword, StringComparison.Ordinal))
            {
                return IsPredefinedTypeKeyword(token);
            }

            return IsSymbolClassification(key, true);
        }

        public bool CanNavigateToDefinition(string classification, string rawText)
        {
            return CanNavigateToDefinition(classification, string.Empty, rawText);
        }

        public bool CanNavigateToDefinition(string classification, string semanticTokenType, string rawText)
        {
            var token = NormalizeTokenText(rawText);
            if (token.Length == 0)
            {
                return false;
            }

            var key = ResolvePresentationClassification(classification, semanticTokenType);
            if (_semanticTokenMappingService.IsGenericClassification(key))
            {
                return IsIdentifierLikeTokenText(token);
            }

            if (IsExcludedInteractionClassification(key) || !IsSymbolClassification(key, true))
            {
                return false;
            }

            return !string.Equals(key, SemanticTokenClassificationNames.Local, StringComparison.Ordinal) &&
                !string.Equals(key, SemanticTokenClassificationNames.Parameter, StringComparison.Ordinal);
        }

        public string NormalizeClassification(string classification)
        {
            return _semanticTokenMappingService.NormalizeClassification(classification);
        }

        public string ResolvePresentationClassification(string classification, string semanticTokenType)
        {
            return _semanticTokenMappingService.ResolvePresentationClassification(classification, semanticTokenType);
        }

        private static bool IsExcludedInteractionClassification(string key)
        {
            return string.Equals(key, SemanticTokenClassificationNames.Operator, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Punctuation, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Comment, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Xml, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Preprocessor, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.String, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Number, StringComparison.Ordinal);
        }

        private static bool IsSymbolClassification(string key, bool includeLocalsAndParameters)
        {
            if (string.Equals(key, SemanticTokenClassificationNames.Class, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Type, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Namespace, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Method, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Property, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Event, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Field, StringComparison.Ordinal) ||
                string.Equals(key, SemanticTokenClassificationNames.Variable, StringComparison.Ordinal))
            {
                return true;
            }

            return includeLocalsAndParameters &&
                (string.Equals(key, SemanticTokenClassificationNames.Local, StringComparison.Ordinal) ||
                 string.Equals(key, SemanticTokenClassificationNames.Parameter, StringComparison.Ordinal));
        }

        private static string NormalizeTokenText(string value)
        {
            return value != null ? value.Trim() : string.Empty;
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
            if (string.IsNullOrEmpty(rawText) || IsReservedIdentifierKeyword(rawText))
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

        private static bool IsReservedIdentifierKeyword(string token)
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
    }
}
