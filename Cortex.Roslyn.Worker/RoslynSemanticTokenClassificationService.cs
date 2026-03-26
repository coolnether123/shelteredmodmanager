using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;

namespace Cortex.Roslyn.Worker
{
    internal sealed class RoslynSemanticTokenClassificationService
    {
        public LanguageServiceClassifiedSpan CreateClassifiedSpan(
            SourceText text,
            string roslynClassification,
            TextSpan span,
            ISymbol resolvedSymbol)
        {
            var classification = ResolveClassification(roslynClassification, resolvedSymbol);
            var linePosition = text.Lines.GetLinePosition(span.Start);
            return new LanguageServiceClassifiedSpan
            {
                Classification = classification,
                SemanticTokenType = SemanticTokenClassification.ToLspSemanticTokenType(classification),
                Start = span.Start,
                Length = span.Length,
                Line = linePosition.Line + 1,
                Column = linePosition.Character + 1
            };
        }

        public LanguageServiceClassifiedSpan ChoosePreferredClassification(
            LanguageServiceClassifiedSpan existing,
            LanguageServiceClassifiedSpan incoming)
        {
            if (existing == null)
            {
                return incoming;
            }

            if (incoming == null)
            {
                return existing;
            }

            var existingScore = GetClassificationPriority(existing.Classification);
            var incomingScore = GetClassificationPriority(incoming.Classification);
            if (incomingScore != existingScore)
            {
                return incomingScore > existingScore ? incoming : existing;
            }

            var existingTokenType = existing.SemanticTokenType ?? string.Empty;
            var incomingTokenType = incoming.SemanticTokenType ?? string.Empty;
            if (!string.Equals(existingTokenType, incomingTokenType, StringComparison.Ordinal))
            {
                if (incomingTokenType.Length > existingTokenType.Length)
                {
                    return incoming;
                }

                if (existingTokenType.Length > incomingTokenType.Length)
                {
                    return existing;
                }
            }

            return existing;
        }

        public string ResolveClassification(string roslynClassification, ISymbol symbol)
        {
            var normalized = SemanticTokenClassification.Normalize(roslynClassification);
            if (!SemanticTokenClassification.IsGeneric(normalized))
            {
                return normalized;
            }

            var symbolClassification = ClassifySymbol(symbol);
            return !string.IsNullOrEmpty(symbolClassification)
                ? symbolClassification
                : normalized;
        }

        private static string ClassifySymbol(ISymbol symbol)
        {
            if (symbol == null)
            {
                return string.Empty;
            }

            var alias = symbol as IAliasSymbol;
            if (alias != null)
            {
                symbol = alias.Target;
                if (symbol == null)
                {
                    return string.Empty;
                }
            }

            var namespaceSymbol = symbol as INamespaceSymbol;
            if (namespaceSymbol != null)
            {
                return SemanticTokenClassificationNames.Namespace;
            }

            var namedType = symbol as INamedTypeSymbol;
            if (namedType != null)
            {
                return namedType.TypeKind == TypeKind.Class
                    ? SemanticTokenClassificationNames.Class
                    : SemanticTokenClassificationNames.Type;
            }

            if (symbol is ITypeSymbol || symbol is ITypeParameterSymbol)
            {
                return SemanticTokenClassificationNames.Type;
            }

            if (symbol is IMethodSymbol)
            {
                return SemanticTokenClassificationNames.Method;
            }

            if (symbol is IPropertySymbol)
            {
                return SemanticTokenClassificationNames.Property;
            }

            if (symbol is IEventSymbol)
            {
                return SemanticTokenClassificationNames.Event;
            }

            if (symbol is IFieldSymbol)
            {
                return SemanticTokenClassificationNames.Field;
            }

            if (symbol is IParameterSymbol)
            {
                return SemanticTokenClassificationNames.Parameter;
            }

            if (symbol is ILocalSymbol || symbol.Kind == SymbolKind.RangeVariable)
            {
                return SemanticTokenClassificationNames.Local;
            }

            return string.Empty;
        }

        private static int GetClassificationPriority(string classification)
        {
            switch (SemanticTokenClassification.Normalize(classification))
            {
                case SemanticTokenClassificationNames.Namespace:
                    return 200;
                case SemanticTokenClassificationNames.Class:
                case SemanticTokenClassificationNames.Type:
                    return 190;
                case SemanticTokenClassificationNames.Method:
                    return 180;
                case SemanticTokenClassificationNames.Property:
                case SemanticTokenClassificationNames.Event:
                    return 170;
                case SemanticTokenClassificationNames.Field:
                    return 160;
                case SemanticTokenClassificationNames.Parameter:
                    return 150;
                case SemanticTokenClassificationNames.Local:
                    return 140;
                case SemanticTokenClassificationNames.Keyword:
                    return 130;
                case SemanticTokenClassificationNames.String:
                case SemanticTokenClassificationNames.Number:
                    return 120;
                case SemanticTokenClassificationNames.Comment:
                case SemanticTokenClassificationNames.Xml:
                case SemanticTokenClassificationNames.Preprocessor:
                    return 110;
                case SemanticTokenClassificationNames.Operator:
                case SemanticTokenClassificationNames.Punctuation:
                    return 100;
                case SemanticTokenClassificationNames.Identifier:
                case SemanticTokenClassificationNames.Text:
                case "":
                    return 0;
                default:
                    return 50;
            }
        }
    }
}
