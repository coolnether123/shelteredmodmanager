using System;

namespace Cortex.Core.Models
{
    public static class SemanticTokenClassificationNames
    {
        public const string Identifier = "identifier";
        public const string Text = "text";
        public const string Namespace = "namespace";
        public const string Class = "class";
        public const string Type = "type";
        public const string Method = "method";
        public const string Property = "property";
        public const string Event = "event";
        public const string Field = "field";
        public const string Variable = "variable";
        public const string Parameter = "parameter";
        public const string Local = "local";
        public const string Keyword = "keyword";
        public const string String = "string";
        public const string Number = "number";
        public const string Comment = "comment";
        public const string Xml = "xml";
        public const string Preprocessor = "preprocessor";
        public const string Operator = "operator";
        public const string Punctuation = "punctuation";
    }

    public static class SemanticTokenClassification
    {
        public static string Normalize(string classification)
        {
            var key = (classification ?? string.Empty).Trim().ToLowerInvariant();
            if (key.Length == 0)
            {
                return string.Empty;
            }

            if (key.IndexOf("comment", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Comment;
            }

            if (key.IndexOf("xml", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Xml;
            }

            if (key.IndexOf("preprocessor", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Preprocessor;
            }

            if (key.IndexOf("keyword", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("control", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Keyword;
            }

            if (key.IndexOf("namespace", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Namespace;
            }

            if (key.IndexOf("class", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Class;
            }

            if (key.IndexOf("struct", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("interface", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("enum", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("delegate", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("record", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("typeparameter", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("type parameter", StringComparison.Ordinal) >= 0 ||
                string.Equals(key, SemanticTokenClassificationNames.Type, StringComparison.Ordinal))
            {
                return SemanticTokenClassificationNames.Type;
            }

            if (key.IndexOf("extension method", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("method", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("function", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Method;
            }

            if (key.IndexOf("property", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Property;
            }

            if (key.IndexOf("event", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Event;
            }

            if (key.IndexOf("field", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("enum member", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("constant", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Field;
            }

            if (string.Equals(key, SemanticTokenClassificationNames.Variable, StringComparison.Ordinal))
            {
                return SemanticTokenClassificationNames.Variable;
            }

            if (key.IndexOf("parameter", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Parameter;
            }

            if (key.IndexOf("local", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("range variable", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Local;
            }

            if (key.IndexOf("string", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("char", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.String;
            }

            if (key.IndexOf("numeric", StringComparison.Ordinal) >= 0 ||
                key.IndexOf("number", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Number;
            }

            if (key.IndexOf("operator", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Operator;
            }

            if (key.IndexOf("punctuation", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Punctuation;
            }

            if (key.IndexOf("identifier", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Identifier;
            }

            if (key.IndexOf("text", StringComparison.Ordinal) >= 0)
            {
                return SemanticTokenClassificationNames.Text;
            }

            return key;
        }

        public static bool IsGeneric(string classification)
        {
            var normalized = Normalize(classification);
            return normalized.Length == 0 ||
                string.Equals(normalized, SemanticTokenClassificationNames.Identifier, StringComparison.Ordinal) ||
                string.Equals(normalized, SemanticTokenClassificationNames.Text, StringComparison.Ordinal);
        }

        public static string ToLspSemanticTokenType(string classification)
        {
            var normalized = Normalize(classification);
            switch (normalized)
            {
                case SemanticTokenClassificationNames.Namespace:
                case SemanticTokenClassificationNames.Class:
                case SemanticTokenClassificationNames.Type:
                case SemanticTokenClassificationNames.Method:
                case SemanticTokenClassificationNames.Property:
                case SemanticTokenClassificationNames.Event:
                case SemanticTokenClassificationNames.Parameter:
                case SemanticTokenClassificationNames.Keyword:
                case SemanticTokenClassificationNames.String:
                case SemanticTokenClassificationNames.Number:
                case SemanticTokenClassificationNames.Comment:
                case SemanticTokenClassificationNames.Operator:
                    return normalized;
                case SemanticTokenClassificationNames.Field:
                case SemanticTokenClassificationNames.Variable:
                case SemanticTokenClassificationNames.Local:
                    return SemanticTokenClassificationNames.Variable;
                default:
                    return string.Empty;
            }
        }

        public static string FromLspSemanticTokenType(string semanticTokenType)
        {
            var normalized = Normalize(semanticTokenType);
            switch (normalized)
            {
                case SemanticTokenClassificationNames.Namespace:
                case SemanticTokenClassificationNames.Class:
                case SemanticTokenClassificationNames.Type:
                case SemanticTokenClassificationNames.Method:
                case SemanticTokenClassificationNames.Property:
                case SemanticTokenClassificationNames.Event:
                case SemanticTokenClassificationNames.Parameter:
                case SemanticTokenClassificationNames.Keyword:
                case SemanticTokenClassificationNames.String:
                case SemanticTokenClassificationNames.Number:
                case SemanticTokenClassificationNames.Comment:
                case SemanticTokenClassificationNames.Operator:
                case SemanticTokenClassificationNames.Variable:
                    return normalized;
                default:
                    return string.Empty;
            }
        }
    }
}
