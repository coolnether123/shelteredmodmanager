using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cortex.Core.Models;

namespace Cortex.Plugin.Harmony.Services.Resolution
{
    internal sealed class HarmonySourceSymbolService
    {
        public bool TryBuildHarmonyPatchBinding(string text, int absolutePosition, string symbolText, out HarmonyPatchAttributeBinding binding)
        {
            binding = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var declarationStart = FindEnclosingTypeDeclarationStart(text, absolutePosition);
            if (declarationStart < 0)
            {
                return false;
            }

            var attributes = CollectPrecedingHarmonyPatchAttributes(text, declarationStart);
            if (attributes.Count == 0)
            {
                return false;
            }

            binding = new HarmonyPatchAttributeBinding();
            for (var i = 0; i < attributes.Count; i++)
            {
                MergeHarmonyPatchAttribute(attributes[i], binding);
            }

            if (string.IsNullOrEmpty(binding.TypeName) || string.IsNullOrEmpty(binding.MethodName))
            {
                return false;
            }

            var normalizedSymbol = HarmonySymbolNameUtility.NormalizeMethodName(symbolText);
            if (string.IsNullOrEmpty(normalizedSymbol))
            {
                return true;
            }

            string methodHeader;
            bool resolvedFromAttribute;
            if (TryExtractEnclosingMethodHeader(text, absolutePosition, out methodHeader) &&
                !string.IsNullOrEmpty(ResolveSourcePatchKind(methodHeader, normalizedSymbol, out resolvedFromAttribute)))
            {
                return true;
            }

            var normalizedType = HarmonySymbolNameUtility.NormalizeTypeName(binding.TypeName);
            if (string.Equals(normalizedSymbol, binding.MethodName, StringComparison.Ordinal) ||
                string.Equals(normalizedSymbol, normalizedType, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedSymbol, HarmonySymbolNameUtility.GetSimpleTypeName(normalizedType), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var typeName = FindDeclaredTypeName(text, declarationStart);
            return string.IsNullOrEmpty(typeName) ||
                string.Equals(normalizedSymbol, typeName, StringComparison.Ordinal) ||
                string.Equals(normalizedSymbol, HarmonySymbolNameUtility.NormalizeMethodName(typeName), StringComparison.Ordinal);
        }

        public bool TryExtractEnclosingMethodHeader(string text, int absolutePosition, out string header)
        {
            header = string.Empty;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var safePosition = ClampPosition(text, absolutePosition);
            for (var i = safePosition; i >= 0; i--)
            {
                if (text[i] != '{')
                {
                    continue;
                }

                string candidate;
                if (!TryExtractDeclarationHeader(text, i, out candidate))
                {
                    continue;
                }

                var normalized = NormalizeHeader(candidate);
                if (IsMethodLikeHeader(normalized))
                {
                    header = candidate;
                    return true;
                }
            }

            return false;
        }

        public string ResolveSourcePatchKind(string declarationHeader, string methodName, out bool resolvedFromAttribute)
        {
            resolvedFromAttribute = false;
            var normalizedHeader = NormalizeHeader(declarationHeader);
            if (normalizedHeader.IndexOf("HarmonyPrefix", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                resolvedFromAttribute = true;
                return "Prefix";
            }

            if (normalizedHeader.IndexOf("HarmonyPostfix", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                resolvedFromAttribute = true;
                return "Postfix";
            }

            if (normalizedHeader.IndexOf("HarmonyTranspiler", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                resolvedFromAttribute = true;
                return "Transpiler";
            }

            if (normalizedHeader.IndexOf("HarmonyFinalizer", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                resolvedFromAttribute = true;
                return "Finalizer";
            }

            switch (HarmonySymbolNameUtility.NormalizeMethodName(methodName))
            {
                case "Prefix":
                    return "Prefix";
                case "Postfix":
                    return "Postfix";
                case "Transpiler":
                    return "Transpiler";
                case "Finalizer":
                    return "Finalizer";
                default:
                    return string.Empty;
            }
        }

        public bool TryResolveRuntimeTypeByName(string typeName, out string assemblyPath, out Type runtimeType)
        {
            assemblyPath = string.Empty;
            runtimeType = null;
            var normalizedTypeName = HarmonySymbolNameUtility.NormalizeTypeName(typeName);
            if (string.IsNullOrEmpty(normalizedTypeName))
            {
                return false;
            }

            var bestScore = -1;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var assemblyIndex = 0; assemblyIndex < assemblies.Length; assemblyIndex++)
            {
                Type[] types;
                try
                {
                    types = assemblies[assemblyIndex].GetTypes();
                }
                catch
                {
                    continue;
                }

                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var score = ScoreRuntimeTypeMatch(types[typeIndex], normalizedTypeName);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        runtimeType = types[typeIndex];
                    }
                }
            }

            if (runtimeType == null)
            {
                return false;
            }

            try
            {
                assemblyPath = runtimeType.Assembly.Location;
            }
            catch
            {
                assemblyPath = string.Empty;
            }

            return !string.IsNullOrEmpty(assemblyPath);
        }

        public bool TryBuildLookupHint(string documentPath, int absolutePosition, string symbolText, out HarmonyMethodLookupHint hint)
        {
            hint = null;
            var text = GetDocumentText(documentPath);
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            var safePosition = ClampPosition(text, absolutePosition);
            for (var i = safePosition; i >= 0; i--)
            {
                if (text[i] != '{')
                {
                    continue;
                }

                if (!TryBuildLookupHintFromBrace(text, i, out hint))
                {
                    continue;
                }

                if (hint == null ||
                    string.IsNullOrEmpty(symbolText) ||
                    string.Equals(HarmonySymbolNameUtility.NormalizeMethodName(symbolText), hint.Name, StringComparison.Ordinal) ||
                    string.Equals(HarmonySymbolNameUtility.GetSimpleTypeName(HarmonySymbolNameUtility.NormalizeMethodName(symbolText)), hint.Name, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return TryBuildForwardLookupHint(text, safePosition, out hint);
        }

        public bool IsDeclaringTypeSymbol(Type declaringType, string symbolText)
        {
            return HarmonySymbolNameUtility.IsDeclaringTypeSymbol(declaringType, symbolText);
        }

        public string NormalizeMethodName(string value)
        {
            return HarmonySymbolNameUtility.NormalizeMethodName(value);
        }

        public string GetDocumentText(string documentPath)
        {
            try
            {
                return !string.IsNullOrEmpty(documentPath) && File.Exists(documentPath)
                    ? File.ReadAllText(documentPath)
                    : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool TryBuildForwardLookupHint(string text, int position, out HarmonyMethodLookupHint hint)
        {
            hint = null;
            var end = Math.Min(text.Length - 1, position + 1024);
            for (var i = position; i <= end; i++)
            {
                if (text[i] == '{' && TryBuildLookupHintFromBrace(text, i, out hint))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool TryBuildLookupHintFromBrace(string text, int openBraceIndex, out HarmonyMethodLookupHint hint)
        {
            hint = null;
            string header;
            if (!TryExtractDeclarationHeader(text, openBraceIndex, out header))
            {
                return false;
            }

            var normalized = NormalizeHeader(header);
            if (!IsMethodLikeHeader(normalized))
            {
                return false;
            }

            var accessorKind = GetAccessorKind(normalized);
            if (!string.IsNullOrEmpty(accessorKind))
            {
                var ownerHeader = FindAccessorOwnerHeader(text, openBraceIndex);
                var ownerName = ExtractPropertyLikeMemberName(ownerHeader);
                if (string.IsNullOrEmpty(ownerName))
                {
                    return false;
                }

                hint = new HarmonyMethodLookupHint
                {
                    Name = ownerName,
                    ParameterCount = string.Equals(accessorKind, "set", StringComparison.Ordinal) ||
                        string.Equals(accessorKind, "add", StringComparison.Ordinal) ||
                        string.Equals(accessorKind, "remove", StringComparison.Ordinal)
                        ? 1
                        : 0
                };
                return true;
            }

            return TryCreateMethodLookupHint(normalized, out hint);
        }

        private static bool TryCreateMethodLookupHint(string header, out HarmonyMethodLookupHint hint)
        {
            hint = null;
            var openParen = header.IndexOf('(');
            if (openParen < 0)
            {
                return false;
            }

            var closeParen = FindClosingParenthesis(header, openParen);
            if (closeParen <= openParen)
            {
                return false;
            }

            var methodName = ExtractHeaderMemberName(header, openParen);
            if (string.IsNullOrEmpty(methodName))
            {
                return false;
            }

            hint = new HarmonyMethodLookupHint();
            hint.Name = methodName;
            hint.ParameterTypeNames = ParseParameterTypeNames(header.Substring(openParen + 1, closeParen - openParen - 1));
            hint.ParameterCount = hint.ParameterTypeNames.Length;
            hint.IsConstructor = string.Equals(methodName, ".ctor", StringComparison.Ordinal);
            hint.IsStaticConstructor = string.Equals(methodName, ".cctor", StringComparison.Ordinal);
            return true;
        }

        private static bool TryExtractDeclarationHeader(string text, int openBraceIndex, out string header)
        {
            header = string.Empty;
            if (openBraceIndex <= 0 || string.IsNullOrEmpty(text))
            {
                return false;
            }

            var start = Math.Max(0, openBraceIndex - 512);
            var window = text.Substring(start, openBraceIndex - start);
            var lastDelimiter = Math.Max(window.LastIndexOf(';'), Math.Max(window.LastIndexOf('{'), window.LastIndexOf('}')));
            header = (lastDelimiter >= 0 ? window.Substring(lastDelimiter + 1) : window).Trim();
            return !string.IsNullOrEmpty(header);
        }

        private static string FindAccessorOwnerHeader(string text, int openBraceIndex)
        {
            for (var i = openBraceIndex - 1; i >= 0; i--)
            {
                if (text[i] != '{')
                {
                    continue;
                }

                string header;
                if (!TryExtractDeclarationHeader(text, i, out header))
                {
                    continue;
                }

                var normalized = NormalizeHeader(header);
                if (!string.IsNullOrEmpty(normalized) && normalized.IndexOf('(') < 0 && !IsTypeOrControlHeader(normalized))
                {
                    return normalized;
                }
            }

            return string.Empty;
        }

        private static string NormalizeHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(header.Length);
            var lastWasWhitespace = false;
            for (var i = 0; i < header.Length; i++)
            {
                if (char.IsWhiteSpace(header[i]))
                {
                    if (!lastWasWhitespace)
                    {
                        builder.Append(' ');
                        lastWasWhitespace = true;
                    }

                    continue;
                }

                builder.Append(header[i]);
                lastWasWhitespace = false;
            }

            return builder.ToString().Trim();
        }

        private static bool IsMethodLikeHeader(string header)
        {
            return !string.IsNullOrEmpty(header) && !IsTypeOrControlHeader(header);
        }

        private static bool IsTypeOrControlHeader(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return true;
            }

            var lowered = header.ToLowerInvariant();
            return lowered.StartsWith("namespace ") ||
                lowered.StartsWith("class ") ||
                lowered.StartsWith("struct ") ||
                lowered.StartsWith("interface ") ||
                lowered.StartsWith("enum ") ||
                lowered.StartsWith("record ") ||
                lowered.StartsWith("if(") ||
                lowered.StartsWith("if ") ||
                lowered.StartsWith("else") ||
                lowered.StartsWith("for(") ||
                lowered.StartsWith("for ") ||
                lowered.StartsWith("foreach") ||
                lowered.StartsWith("while") ||
                lowered.StartsWith("switch") ||
                lowered.StartsWith("using(") ||
                lowered.StartsWith("using ") ||
                lowered.StartsWith("catch") ||
                lowered.StartsWith("try") ||
                lowered.StartsWith("finally");
        }

        private static string GetAccessorKind(string header)
        {
            if (string.Equals(header, "get", StringComparison.OrdinalIgnoreCase))
            {
                return "get";
            }

            if (string.Equals(header, "set", StringComparison.OrdinalIgnoreCase))
            {
                return "set";
            }

            if (string.Equals(header, "add", StringComparison.OrdinalIgnoreCase))
            {
                return "add";
            }

            if (string.Equals(header, "remove", StringComparison.OrdinalIgnoreCase))
            {
                return "remove";
            }

            return string.Empty;
        }

        private static string ExtractPropertyLikeMemberName(string header)
        {
            if (string.IsNullOrEmpty(header))
            {
                return string.Empty;
            }

            var lastSpace = header.LastIndexOf(' ');
            var candidate = lastSpace >= 0 ? header.Substring(lastSpace + 1) : header;
            var genericIndex = candidate.IndexOf('<');
            return genericIndex >= 0 ? candidate.Substring(0, genericIndex).Trim() : candidate.Trim();
        }

        private static string ExtractHeaderMemberName(string header, int openParen)
        {
            var prefix = header.Substring(0, openParen).Trim();
            var operatorIndex = prefix.LastIndexOf("operator ", StringComparison.Ordinal);
            if (operatorIndex >= 0)
            {
                return MapOperatorMethodName(prefix.Substring(operatorIndex + 9).Trim());
            }

            if (prefix.EndsWith(".this", StringComparison.Ordinal) || prefix.EndsWith(" this", StringComparison.Ordinal))
            {
                return "Item";
            }

            var lastSpace = prefix.LastIndexOf(' ');
            var candidate = lastSpace >= 0 ? prefix.Substring(lastSpace + 1) : prefix;
            if (string.Equals(candidate, "this", StringComparison.Ordinal))
            {
                return "Item";
            }

            var genericIndex = candidate.IndexOf('<');
            return genericIndex >= 0 ? candidate.Substring(0, genericIndex).Trim() : candidate.Trim();
        }

        private static string MapOperatorMethodName(string token)
        {
            switch ((token ?? string.Empty).Trim())
            {
                case "+": return "op_Addition";
                case "-": return "op_Subtraction";
                case "*": return "op_Multiply";
                case "/": return "op_Division";
                case "%": return "op_Modulus";
                case "==": return "op_Equality";
                case "!=": return "op_Inequality";
                case ">": return "op_GreaterThan";
                case "<": return "op_LessThan";
                case ">=": return "op_GreaterThanOrEqual";
                case "<=": return "op_LessThanOrEqual";
                case "!": return "op_LogicalNot";
                case "true": return "op_True";
                case "false": return "op_False";
                case "implicit": return "op_Implicit";
                case "explicit": return "op_Explicit";
                default: return string.Empty;
            }
        }

        private static int FindEnclosingTypeDeclarationStart(string text, int absolutePosition)
        {
            var safePosition = ClampPosition(text, absolutePosition);
            var lineStart = FindLineStart(text, safePosition);
            while (lineStart >= 0)
            {
                var lineEnd = FindLineEnd(text, lineStart);
                var normalized = NormalizeHeader(text.Substring(lineStart, lineEnd - lineStart));
                if (normalized.IndexOf(" class ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf(" struct ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.IndexOf(" interface ", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    normalized.StartsWith("class ", StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith("struct ", StringComparison.OrdinalIgnoreCase) ||
                    normalized.StartsWith("interface ", StringComparison.OrdinalIgnoreCase))
                {
                    return lineStart;
                }

                if (lineStart == 0)
                {
                    break;
                }

                lineStart = FindLineStart(text, Math.Max(0, lineStart - 1));
                if (lineStart == 0 && FindLineEnd(text, lineStart) >= safePosition)
                {
                    break;
                }
            }

            return -1;
        }

        private static List<string> CollectPrecedingHarmonyPatchAttributes(string text, int declarationStart)
        {
            var attributes = new List<string>();
            var scan = declarationStart;
            while (scan > 0)
            {
                var end = scan - 1;
                while (end >= 0 && char.IsWhiteSpace(text[end]))
                {
                    end--;
                }

                if (end < 0 || text[end] != ']')
                {
                    break;
                }

                var start = FindMatchingAttributeOpen(text, end);
                if (start < 0)
                {
                    break;
                }

                var attribute = text.Substring(start, end - start + 1);
                if (attribute.IndexOf("HarmonyPatch", StringComparison.Ordinal) >= 0)
                {
                    attributes.Insert(0, attribute);
                }

                scan = start;
            }

            return attributes;
        }

        private static int FindMatchingAttributeOpen(string text, int closeBracketIndex)
        {
            var depth = 0;
            for (var i = closeBracketIndex; i >= 0; i--)
            {
                if (text[i] == ']')
                {
                    depth++;
                }
                else if (text[i] == '[')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static string FindDeclaredTypeName(string text, int declarationStart)
        {
            var lineEnd = FindLineEnd(text, declarationStart);
            var line = NormalizeHeader(text.Substring(declarationStart, lineEnd - declarationStart));
            var keywords = new[] { "class", "struct", "interface" };
            for (var i = 0; i < keywords.Length; i++)
            {
                var marker = keywords[i] + " ";
                var start = line.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    continue;
                }

                start += marker.Length;
                var end = start;
                while (end < line.Length && (char.IsLetterOrDigit(line[end]) || line[end] == '_' || line[end] == '@'))
                {
                    end++;
                }

                return end > start ? line.Substring(start, end - start).Trim() : string.Empty;
            }

            return string.Empty;
        }

        private static void MergeHarmonyPatchAttribute(string attributeText, HarmonyPatchAttributeBinding binding)
        {
            var index = 0;
            while (index < attributeText.Length)
            {
                var patchIndex = attributeText.IndexOf("HarmonyPatch", index, StringComparison.Ordinal);
                if (patchIndex < 0)
                {
                    break;
                }

                var openParen = attributeText.IndexOf('(', patchIndex);
                if (openParen < 0)
                {
                    break;
                }

                var closeParen = FindClosingParenthesis(attributeText, openParen);
                if (closeParen <= openParen)
                {
                    break;
                }

                ApplyHarmonyPatchArguments(attributeText.Substring(openParen + 1, closeParen - openParen - 1), binding);
                index = closeParen + 1;
            }
        }

        private static void ApplyHarmonyPatchArguments(string argumentsText, HarmonyPatchAttributeBinding binding)
        {
            var arguments = SplitTopLevel(argumentsText);
            for (var i = 0; i < arguments.Length; i++)
            {
                var current = arguments[i].Trim();
                if (string.IsNullOrEmpty(binding.TypeName))
                {
                    var typeName = ExtractTypeNameArgument(current);
                    if (!string.IsNullOrEmpty(typeName))
                    {
                        binding.TypeName = typeName;
                        continue;
                    }
                }

                if (string.IsNullOrEmpty(binding.MethodName))
                {
                    var methodName = ExtractMethodNameArgument(current);
                    if (!string.IsNullOrEmpty(methodName))
                    {
                        binding.MethodName = methodName;
                        continue;
                    }
                }

                if ((binding.ParameterTypeNames == null || binding.ParameterTypeNames.Length == 0) &&
                    current.IndexOf("new Type", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    binding.ParameterTypeNames = ExtractParameterTypeArray(current);
                }
            }
        }

        private static string ExtractTypeNameArgument(string argument)
        {
            if (!argument.StartsWith("typeof(", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var openParen = argument.IndexOf('(');
            var closeParen = FindClosingParenthesis(argument, openParen);
            return closeParen > openParen
                ? HarmonySymbolNameUtility.NormalizeTypeName(argument.Substring(openParen + 1, closeParen - openParen - 1))
                : string.Empty;
        }

        private static string ExtractMethodNameArgument(string argument)
        {
            if (argument.StartsWith("\"", StringComparison.Ordinal) && argument.EndsWith("\"", StringComparison.Ordinal) && argument.Length >= 2)
            {
                return argument.Substring(1, argument.Length - 2);
            }

            if (!argument.StartsWith("nameof(", StringComparison.Ordinal))
            {
                return string.Empty;
            }

            var openParen = argument.IndexOf('(');
            var closeParen = FindClosingParenthesis(argument, openParen);
            if (closeParen <= openParen)
            {
                return string.Empty;
            }

            var value = argument.Substring(openParen + 1, closeParen - openParen - 1).Trim();
            var lastDot = value.LastIndexOf('.');
            return HarmonySymbolNameUtility.NormalizeMethodName(lastDot >= 0 ? value.Substring(lastDot + 1) : value);
        }

        private static string[] ExtractParameterTypeArray(string argument)
        {
            var results = new List<string>();
            var index = 0;
            while (index < argument.Length)
            {
                var typeofIndex = argument.IndexOf("typeof(", index, StringComparison.Ordinal);
                if (typeofIndex < 0)
                {
                    break;
                }

                var openParen = argument.IndexOf('(', typeofIndex);
                var closeParen = FindClosingParenthesis(argument, openParen);
                if (closeParen <= openParen)
                {
                    break;
                }

                results.Add(HarmonySymbolNameUtility.NormalizeTypeName(argument.Substring(openParen + 1, closeParen - openParen - 1)));
                index = closeParen + 1;
            }

            return results.ToArray();
        }

        private static string[] ParseParameterTypeNames(string parameterList)
        {
            if (string.IsNullOrEmpty(parameterList))
            {
                return new string[0];
            }

            var items = SplitTopLevel(parameterList);
            var results = new string[items.Length];
            for (var i = 0; i < items.Length; i++)
            {
                var text = items[i];
                var equalsIndex = text.IndexOf('=');
                if (equalsIndex >= 0)
                {
                    text = text.Substring(0, equalsIndex);
                }

                text = text.Replace("ref ", string.Empty).Replace("out ", string.Empty).Replace("params ", string.Empty).Replace("this ", string.Empty).Trim();
                var lastSpace = text.LastIndexOf(' ');
                results[i] = HarmonySymbolNameUtility.NormalizeTypeName(lastSpace > 0 ? text.Substring(0, lastSpace) : text);
            }

            return results;
        }

        private static string[] SplitTopLevel(string text)
        {
            var values = new List<string>();
            var depth = 0;
            var start = 0;
            for (var i = 0; i < text.Length; i++)
            {
                if (text[i] == '<' || text[i] == '[' || text[i] == '(')
                {
                    depth++;
                }
                else if (text[i] == '>' || text[i] == ']' || text[i] == ')')
                {
                    depth = Math.Max(0, depth - 1);
                }
                else if (text[i] == ',' && depth == 0)
                {
                    values.Add(text.Substring(start, i - start));
                    start = i + 1;
                }
            }

            values.Add(text.Substring(start));
            return values.ToArray();
        }

        private static int FindClosingParenthesis(string text, int openParen)
        {
            var depth = 0;
            for (var i = openParen; i < text.Length; i++)
            {
                if (text[i] == '(')
                {
                    depth++;
                }
                else if (text[i] == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        return i;
                    }
                }
            }

            return -1;
        }

        private static int ScoreRuntimeTypeMatch(Type runtimeType, string normalizedTypeName)
        {
            if (runtimeType == null)
            {
                return -1;
            }

            var fullName = HarmonySymbolNameUtility.NormalizeTypeName(runtimeType.FullName ?? runtimeType.Name ?? string.Empty);
            if (string.Equals(fullName, normalizedTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return 100;
            }

            var name = HarmonySymbolNameUtility.NormalizeTypeName(runtimeType.Name ?? string.Empty);
            if (string.Equals(name, normalizedTypeName, StringComparison.OrdinalIgnoreCase))
            {
                return 90;
            }

            var simple = HarmonySymbolNameUtility.GetSimpleTypeName(normalizedTypeName);
            if (!string.IsNullOrEmpty(simple) && string.Equals(name, simple, StringComparison.OrdinalIgnoreCase))
            {
                return 80;
            }

            return fullName.EndsWith("." + normalizedTypeName, StringComparison.OrdinalIgnoreCase) ? 70 : -1;
        }

        private static int ClampPosition(string text, int position)
        {
            return string.IsNullOrEmpty(text) ? 0 : Math.Max(0, Math.Min(position, text.Length - 1));
        }

        private static int FindLineStart(string text, int position)
        {
            var index = Math.Max(0, Math.Min(position, text.Length));
            while (index > 0 && text[index - 1] != '\n')
            {
                index--;
            }

            return index;
        }

        private static int FindLineEnd(string text, int lineStart)
        {
            var index = Math.Max(0, Math.Min(lineStart, text.Length));
            while (index < text.Length && text[index] != '\n')
            {
                index++;
            }

            return index;
        }
    }
}
