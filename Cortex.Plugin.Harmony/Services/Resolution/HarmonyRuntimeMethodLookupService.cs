using System;
using System.Reflection;

namespace Cortex.Plugin.Harmony.Services.Resolution
{
    internal interface IHarmonyRuntimeMethodLookupService
    {
        MethodBase ResolveMethod(Type declaringType, string symbolText, HarmonyMethodLookupHint hint, out string reason);
        string NormalizeMethodName(string value);
    }

    internal sealed class HarmonyMethodLookupHint
    {
        public string Name = string.Empty;
        public int ParameterCount = -1;
        public string[] ParameterTypeNames = new string[0];
        public bool IsConstructor;
        public bool IsStaticConstructor;
    }

    internal sealed class HarmonyRuntimeMethodLookupService : IHarmonyRuntimeMethodLookupService
    {
        public MethodBase ResolveMethod(Type declaringType, string symbolText, HarmonyMethodLookupHint hint, out string reason)
        {
            reason = string.Empty;
            if (declaringType == null)
            {
                reason = "The declaring runtime type could not be determined.";
                return null;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var matches = new MethodBase[32];
            var count = 0;
            var methods = declaringType.GetMethods(flags);
            for (var i = 0; i < methods.Length; i++)
            {
                count = AddMatch(matches, count, methods[i]);
            }

            var constructors = declaringType.GetConstructors(flags);
            for (var i = 0; i < constructors.Length; i++)
            {
                count = AddMatch(matches, count, constructors[i]);
            }

            if (declaringType.TypeInitializer != null)
            {
                count = AddMatch(matches, count, declaringType.TypeInitializer);
            }

            MethodBase unique = null;
            for (var i = 0; i < count; i++)
            {
                var candidate = matches[i];
                if (!IsMethodMatch(candidate, declaringType, symbolText, hint))
                {
                    continue;
                }

                if (unique != null && unique.MetadataToken != candidate.MetadataToken)
                {
                    reason = "Multiple runtime overloads matched the current decompiled member.";
                    return null;
                }

                unique = candidate;
            }

            if (unique == null)
            {
                reason = "The selected symbol could not be resolved to a runtime method.";
            }

            return unique;
        }

        public string NormalizeMethodName(string value)
        {
            return HarmonySymbolNameUtility.NormalizeMethodName(value);
        }

        private static int AddMatch(MethodBase[] matches, int count, MethodBase method)
        {
            if (matches == null || method == null || count >= matches.Length)
            {
                return count;
            }

            matches[count] = method;
            return count + 1;
        }

        private bool IsMethodMatch(MethodBase method, Type declaringType, string symbolText, HarmonyMethodLookupHint hint)
        {
            if (method == null)
            {
                return false;
            }

            if (hint == null)
            {
                return string.IsNullOrEmpty(symbolText) || NameMatches(method, declaringType, symbolText);
            }

            if (hint.IsStaticConstructor && method.Name != ".cctor")
            {
                return false;
            }

            if (hint.IsConstructor && !method.IsConstructor && method.Name != ".ctor")
            {
                return false;
            }

            if (!string.IsNullOrEmpty(hint.Name) && !NameMatches(method, declaringType, hint.Name))
            {
                return false;
            }

            var parameterCount = method.GetParameters().Length;
            if (hint.ParameterCount >= 0 && parameterCount != hint.ParameterCount)
            {
                return false;
            }

            if (!ParameterTypesMatch(method, hint.ParameterTypeNames))
            {
                return false;
            }

            return string.IsNullOrEmpty(symbolText) || NameMatches(method, declaringType, symbolText);
        }

        private static bool ParameterTypesMatch(MethodBase method, string[] parameterTypeNames)
        {
            if (method == null || parameterTypeNames == null || parameterTypeNames.Length == 0)
            {
                return true;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != parameterTypeNames.Length)
            {
                return false;
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                var type = parameters[i].ParameterType;
                if (type != null && type.IsByRef)
                {
                    type = type.GetElementType();
                }

                if (!TypeNameMatches(type, parameterTypeNames[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private bool NameMatches(MethodBase method, Type declaringType, string symbolText)
        {
            var normalizedSymbol = NormalizeMethodName(symbolText);
            if (string.IsNullOrEmpty(normalizedSymbol) || method == null)
            {
                return false;
            }

            var normalizedSymbolSuffix = normalizedSymbol;
            var normalizedSymbolDot = normalizedSymbol.LastIndexOf('.');
            if (normalizedSymbolDot >= 0 && normalizedSymbolDot + 1 < normalizedSymbol.Length)
            {
                normalizedSymbolSuffix = normalizedSymbol.Substring(normalizedSymbolDot + 1);
            }

            var methodName = method.Name ?? string.Empty;
            if (string.Equals(methodName, normalizedSymbol, StringComparison.Ordinal) ||
                string.Equals(NormalizeMethodName(methodName), normalizedSymbol, StringComparison.Ordinal) ||
                string.Equals(methodName, normalizedSymbolSuffix, StringComparison.Ordinal) ||
                string.Equals(NormalizeMethodName(methodName), normalizedSymbolSuffix, StringComparison.Ordinal))
            {
                return true;
            }

            if (method.IsConstructor)
            {
                return string.Equals(normalizedSymbol, ".ctor", StringComparison.Ordinal) ||
                    string.Equals(normalizedSymbol, ".cctor", StringComparison.Ordinal) ||
                    string.Equals(normalizedSymbol, NormalizeMethodName(declaringType != null ? declaringType.Name : string.Empty), StringComparison.Ordinal) ||
                    string.Equals(normalizedSymbolSuffix, NormalizeMethodName(declaringType != null ? declaringType.Name : string.Empty), StringComparison.Ordinal);
            }

            var explicitIndex = methodName.LastIndexOf('.');
            if (explicitIndex >= 0)
            {
                var suffix = methodName.Substring(explicitIndex + 1);
                if (string.Equals(NormalizeMethodName(suffix), normalizedSymbol, StringComparison.Ordinal) ||
                    string.Equals(NormalizeMethodName(suffix), normalizedSymbolSuffix, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            var accessorBaseName = GetAccessorBaseName(methodName);
            if (!string.IsNullOrEmpty(accessorBaseName) &&
                (string.Equals(NormalizeMethodName(accessorBaseName), normalizedSymbol, StringComparison.Ordinal) ||
                string.Equals(NormalizeMethodName(accessorBaseName), normalizedSymbolSuffix, StringComparison.Ordinal)))
            {
                return true;
            }

            return string.Equals(HarmonySymbolNameUtility.GetOperatorMethodName(symbolText), methodName, StringComparison.Ordinal);
        }

        private static string GetAccessorBaseName(string methodName)
        {
            if (string.IsNullOrEmpty(methodName))
            {
                return string.Empty;
            }

            if (methodName.StartsWith("get_", StringComparison.Ordinal) ||
                methodName.StartsWith("set_", StringComparison.Ordinal) ||
                methodName.StartsWith("add_", StringComparison.Ordinal) ||
                methodName.StartsWith("remove_", StringComparison.Ordinal))
            {
                return methodName.Substring(4);
            }

            var explicitIndex = methodName.LastIndexOf('.');
            if (explicitIndex > 0)
            {
                var suffix = methodName.Substring(explicitIndex + 1);
                if (suffix.StartsWith("get_", StringComparison.Ordinal) ||
                    suffix.StartsWith("set_", StringComparison.Ordinal) ||
                    suffix.StartsWith("add_", StringComparison.Ordinal) ||
                    suffix.StartsWith("remove_", StringComparison.Ordinal))
                {
                    return suffix.Substring(4);
                }
            }

            return string.Empty;
        }

        private static bool TypeNameMatches(Type runtimeType, string requestedTypeName)
        {
            return HarmonySymbolNameUtility.TypeNameMatches(runtimeType, requestedTypeName);
        }
    }
}
