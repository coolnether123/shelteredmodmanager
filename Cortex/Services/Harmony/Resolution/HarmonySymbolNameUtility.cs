using System;

namespace Cortex.Services.Harmony.Resolution
{
    internal static class HarmonySymbolNameUtility
    {
        public static string NormalizeMethodName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = value.Trim();
            var parenIndex = value.IndexOf('(');
            if (parenIndex >= 0)
            {
                value = value.Substring(0, parenIndex);
            }

            var whitespaceIndex = value.LastIndexOf(' ');
            if (whitespaceIndex >= 0)
            {
                value = value.Substring(whitespaceIndex + 1);
            }

            return value.Trim();
        }

        public static string NormalizeTypeName(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            value = value.Replace("global::", string.Empty).Replace('+', '.').Trim();
            var tickIndex = value.IndexOf('`');
            if (tickIndex >= 0)
            {
                value = value.Substring(0, tickIndex);
            }

            var genericIndex = value.IndexOf('<');
            if (genericIndex >= 0)
            {
                value = value.Substring(0, genericIndex);
            }

            return value;
        }

        public static string GetSimpleTypeName(string typeName)
        {
            var normalizedTypeName = NormalizeTypeName(typeName);
            if (string.IsNullOrEmpty(normalizedTypeName))
            {
                return string.Empty;
            }

            var lastDot = normalizedTypeName.LastIndexOf('.');
            return lastDot >= 0 && lastDot + 1 < normalizedTypeName.Length
                ? normalizedTypeName.Substring(lastDot + 1)
                : normalizedTypeName;
        }

        public static bool TypeNameMatches(Type runtimeType, string requestedTypeName)
        {
            if (runtimeType == null || string.IsNullOrEmpty(requestedTypeName))
            {
                return false;
            }

            var normalizedRequested = NormalizeTypeName(requestedTypeName);
            var runtimeFullName = NormalizeTypeName(runtimeType.FullName ?? runtimeType.Name ?? string.Empty);
            var runtimeName = NormalizeTypeName(runtimeType.Name ?? string.Empty);
            return string.Equals(runtimeFullName, normalizedRequested, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(runtimeName, normalizedRequested, StringComparison.OrdinalIgnoreCase) ||
                runtimeFullName.EndsWith("." + normalizedRequested, StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDeclaringTypeSymbol(Type declaringType, string symbolText)
        {
            if (declaringType == null || string.IsNullOrEmpty(symbolText))
            {
                return false;
            }

            var normalizedSymbol = NormalizeTypeName(symbolText);
            if (string.IsNullOrEmpty(normalizedSymbol))
            {
                return false;
            }

            var declaringTypeName = NormalizeTypeName(declaringType.Name ?? string.Empty);
            var declaringTypeFullName = NormalizeTypeName(declaringType.FullName ?? string.Empty);
            return string.Equals(normalizedSymbol, declaringTypeName, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedSymbol, declaringTypeFullName, StringComparison.OrdinalIgnoreCase) ||
                (!string.IsNullOrEmpty(declaringTypeFullName) &&
                declaringTypeFullName.EndsWith("." + normalizedSymbol, StringComparison.OrdinalIgnoreCase));
        }

        public static string GetOperatorMethodName(string symbolText)
        {
            switch (NormalizeMethodName(symbolText))
            {
                case "+":
                    return "op_Addition";
                case "-":
                    return "op_Subtraction";
                case "*":
                    return "op_Multiply";
                case "/":
                    return "op_Division";
                case "%":
                    return "op_Modulus";
                case "==":
                    return "op_Equality";
                case "!=":
                    return "op_Inequality";
                case ">":
                    return "op_GreaterThan";
                case "<":
                    return "op_LessThan";
                case ">=":
                    return "op_GreaterThanOrEqual";
                case "<=":
                    return "op_LessThanOrEqual";
                case "!":
                    return "op_LogicalNot";
                case "true":
                    return "op_True";
                case "false":
                    return "op_False";
                default:
                    return string.Empty;
            }
        }
    }
}