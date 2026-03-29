using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Cortex.Core.Models;
using Cortex.LanguageService.Protocol;

namespace Cortex.Services
{
    internal sealed class EditorMethodRelationshipItem
    {
        public string Title = string.Empty;
        public string Detail = string.Empty;
        public string SymbolKind = string.Empty;
        public string MetadataName = string.Empty;
        public string ContainingTypeName = string.Empty;
        public string ContainingAssemblyName = string.Empty;
        public string DocumentationCommentId = string.Empty;
        public string Relationship = string.Empty;
        public int CallCount = 1;
    }

    internal sealed class EditorMethodRelationshipsContext
    {
        public bool IsExpanded;
        public bool IsLoading;
        public bool HasResponse;
        public string StatusMessage = string.Empty;
        public LanguageServiceCallHierarchyItem[] IncomingCallHierarchy = new LanguageServiceCallHierarchyItem[0];
        public LanguageServiceCallHierarchyItem[] OutgoingCallHierarchy = new LanguageServiceCallHierarchyItem[0];
        public EditorMethodRelationshipItem[] IncomingCalls = new EditorMethodRelationshipItem[0];
        public EditorMethodRelationshipItem[] OutgoingCalls = new EditorMethodRelationshipItem[0];
        public int IncomingCallCount;
        public int OutgoingCallCount;
    }

    internal sealed class EditorMethodRelationshipsContextService
    {
        private static readonly string[] ParameterModifiers = new[] { "ref ", "out ", "in ", "params ", "this " };

        public EditorMethodRelationshipsContext BuildContext(CortexMethodInspectorState inspector, EditorCommandTarget target)
        {
            var context = new EditorMethodRelationshipsContext();
            if (inspector == null)
            {
                context.StatusMessage = "Method relationships are not available.";
                return context;
            }

            context.IsExpanded = inspector.RelationshipsExpanded;
            if (!inspector.RelationshipsExpanded)
            {
                context.StatusMessage = "Expand this section to analyze method relationships.";
                return context;
            }

            if (!inspector.RelationshipsRequested || !string.IsNullOrEmpty(inspector.RelationshipsRequestKey))
            {
                context.IsLoading = true;
                context.StatusMessage = !string.IsNullOrEmpty(inspector.RelationshipsStatusMessage)
                    ? inspector.RelationshipsStatusMessage
                    : "Analyzing method relationships.";
                return context;
            }

            var response = inspector.RelationshipsCallHierarchy;
            if (response == null)
            {
                context.StatusMessage = !string.IsNullOrEmpty(inspector.RelationshipsStatusMessage)
                    ? inspector.RelationshipsStatusMessage
                    : "Method relationship analysis has not produced any results yet.";
                return context;
            }

            context.HasResponse = true;
            var outgoing = new List<EditorMethodRelationshipItem>();
            var incoming = new List<EditorMethodRelationshipItem>();
            if (!response.Success)
            {
                context.StatusMessage = !string.IsNullOrEmpty(response.StatusMessage)
                    ? response.StatusMessage
                    : "Method relationship analysis failed for the selected method.";
                return context;
            }

            context.IncomingCallHierarchy = response.IncomingCalls ?? new LanguageServiceCallHierarchyItem[0];
            context.OutgoingCallHierarchy = response.OutgoingCalls ?? new LanguageServiceCallHierarchyItem[0];
            AppendCallHierarchyItems(outgoing, response.OutgoingCalls);
            AppendSignatureDependencies(outgoing, target);
            AppendCallHierarchyItems(incoming, response.IncomingCalls);
            AppendContractUsages(incoming, target);

            context.IncomingCalls = incoming.ToArray();
            context.OutgoingCalls = outgoing.ToArray();
            context.IncomingCallCount = context.IncomingCalls.Length;
            context.OutgoingCallCount = context.OutgoingCalls.Length;
            context.StatusMessage = !string.IsNullOrEmpty(response.StatusMessage)
                ? response.StatusMessage
                : "Method relationships resolved.";
            return context;
        }

        private static void AppendCallHierarchyItems(List<EditorMethodRelationshipItem> items, LanguageServiceCallHierarchyItem[] sourceItems)
        {
            if (items == null || sourceItems == null)
            {
                return;
            }

            for (var i = 0; i < sourceItems.Length; i++)
            {
                var item = sourceItems[i];
                if (item == null)
                {
                    continue;
                }

                AddDistinct(items, new EditorMethodRelationshipItem
                {
                    Title = item.SymbolDisplay ?? string.Empty,
                    Detail = item.ContainingTypeName ?? string.Empty,
                    SymbolKind = item.SymbolKind ?? string.Empty,
                    MetadataName = item.MetadataName ?? string.Empty,
                    ContainingTypeName = item.ContainingTypeName ?? string.Empty,
                    ContainingAssemblyName = item.ContainingAssemblyName ?? string.Empty,
                    DocumentationCommentId = item.DocumentationCommentId ?? string.Empty,
                    Relationship = item.Relationship ?? "Call",
                    CallCount = item.CallCount > 0 ? item.CallCount : 1
                });
            }
        }

        private static void AppendSignatureDependencies(List<EditorMethodRelationshipItem> items, EditorCommandTarget target)
        {
            MethodBase method;
            if (items == null || !TryResolveMethod(target, out method) || method == null)
            {
                return;
            }

            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                if (parameter == null || parameter.ParameterType == null)
                {
                    continue;
                }

                AddDistinct(items, CreateTypeRelationship(
                    parameter.ParameterType,
                    "Parameter Type",
                    parameter.Name ?? string.Empty));
            }

            var methodInfo = method as MethodInfo;
            if (methodInfo != null && methodInfo.ReturnType != null && methodInfo.ReturnType != typeof(void))
            {
                AddDistinct(items, CreateTypeRelationship(methodInfo.ReturnType, "Return Type", string.Empty));
            }
        }

        private static void AppendContractUsages(List<EditorMethodRelationshipItem> items, EditorCommandTarget target)
        {
            MethodBase method;
            if (items == null || !TryResolveMethod(target, out method) || method == null || method.DeclaringType == null)
            {
                return;
            }

            var methodInfo = method as MethodInfo;
            if (methodInfo != null)
            {
                var baseDefinition = methodInfo.GetBaseDefinition();
                if (baseDefinition != null && baseDefinition != methodInfo)
                {
                    AddDistinct(items, CreateMethodRelationship(baseDefinition, "Override Contract"));
                }
            }

            var interfaces = method.DeclaringType.GetInterfaces();
            for (var i = 0; i < interfaces.Length; i++)
            {
                InterfaceMapping map;
                try
                {
                    map = method.DeclaringType.GetInterfaceMap(interfaces[i]);
                }
                catch
                {
                    continue;
                }

                for (var index = 0; index < map.TargetMethods.Length; index++)
                {
                    if (map.TargetMethods[index] != method)
                    {
                        continue;
                    }

                    AddDistinct(items, CreateMethodRelationship(map.InterfaceMethods[index], "Implemented Contract"));
                }
            }
        }

        private static EditorMethodRelationshipItem CreateTypeRelationship(Type type, string relationship, string detail)
        {
            return new EditorMethodRelationshipItem
            {
                Title = type != null ? (type.Name ?? string.Empty) : string.Empty,
                Detail = detail ?? string.Empty,
                SymbolKind = GetTypeSymbolKind(type),
                MetadataName = type != null ? (type.Name ?? string.Empty) : string.Empty,
                ContainingTypeName = GetNormalizedTypeName(type),
                ContainingAssemblyName = type != null && type.Assembly != null ? type.Assembly.GetName().Name ?? string.Empty : string.Empty,
                DocumentationCommentId = BuildTypeDocumentationId(type),
                Relationship = relationship ?? string.Empty,
                CallCount = 1
            };
        }

        private static EditorMethodRelationshipItem CreateMethodRelationship(MethodBase method, string relationship)
        {
            var declaringType = method != null ? method.DeclaringType : null;
            return new EditorMethodRelationshipItem
            {
                Title = method != null ? BuildMethodDisplayName(method) : string.Empty,
                Detail = declaringType != null ? (declaringType.FullName ?? declaringType.Name ?? string.Empty) : string.Empty,
                SymbolKind = method != null && method.IsConstructor ? "Constructor" : "Method",
                MetadataName = method != null ? method.Name ?? string.Empty : string.Empty,
                ContainingTypeName = declaringType != null ? (declaringType.FullName ?? declaringType.Name ?? string.Empty).Replace('+', '.') : string.Empty,
                ContainingAssemblyName = declaringType != null && declaringType.Assembly != null ? declaringType.Assembly.GetName().Name ?? string.Empty : string.Empty,
                DocumentationCommentId = BuildMethodDocumentationId(method),
                Relationship = relationship ?? string.Empty,
                CallCount = 1
            };
        }

        private static void AddDistinct(List<EditorMethodRelationshipItem> items, EditorMethodRelationshipItem candidate)
        {
            if (items == null || candidate == null)
            {
                return;
            }

            var candidateKey = BuildRelationshipKey(candidate);
            for (var i = 0; i < items.Count; i++)
            {
                if (string.Equals(BuildRelationshipKey(items[i]), candidateKey, StringComparison.Ordinal))
                {
                    return;
                }
            }

            items.Add(candidate);
        }

        private static string BuildRelationshipKey(EditorMethodRelationshipItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            return (item.Relationship ?? string.Empty) + "|" +
                (item.DocumentationCommentId ?? string.Empty) + "|" +
                (item.ContainingAssemblyName ?? string.Empty) + "|" +
                (item.MetadataName ?? string.Empty) + "|" +
                (item.ContainingTypeName ?? string.Empty);
        }

        private static bool TryResolveMethod(EditorCommandTarget target, out MethodBase method)
        {
            method = null;
            var assembly = ResolveAssembly(target);
            if (assembly == null)
            {
                return false;
            }

            if (!string.IsNullOrEmpty(target != null ? target.DocumentationCommentId : string.Empty))
            {
                method = ResolveMethodByDocumentationId(assembly, target.DocumentationCommentId);
                if (method != null)
                {
                    return true;
                }
            }

            var typeName = ExtractFullContainingTypeName(target);
            if (string.IsNullOrEmpty(typeName))
            {
                return false;
            }

            Type declaringType;
            try
            {
                declaringType = assembly.GetType(typeName, false);
            }
            catch
            {
                declaringType = null;
            }

            if (declaringType == null)
            {
                return false;
            }

            var metadataName = target != null ? target.MetadataName ?? target.SymbolText ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(metadataName))
            {
                return false;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
            var candidates = new List<MethodBase>();
            if (string.Equals(target != null ? target.SymbolKind ?? string.Empty : string.Empty, "Constructor", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(metadataName, ".ctor", StringComparison.Ordinal))
            {
                var constructors = declaringType.GetConstructors(flags);
                for (var i = 0; i < constructors.Length; i++)
                {
                    candidates.Add(constructors[i]);
                }
            }
            else
            {
                var methods = declaringType.GetMethods(flags);
                for (var i = 0; i < methods.Length; i++)
                {
                    if (string.Equals(methods[i].Name, metadataName, StringComparison.Ordinal))
                    {
                        candidates.Add(methods[i]);
                    }
                }
            }

            return TryResolveCandidate(target, candidates, out method);
        }

        private static Assembly ResolveAssembly(EditorCommandTarget target)
        {
            var assemblyName = target != null ? target.ContainingAssemblyName ?? string.Empty : string.Empty;
            if (string.IsNullOrEmpty(assemblyName))
            {
                return null;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                try
                {
                    if (string.Equals(loadedAssemblies[i].GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return loadedAssemblies[i];
                    }
                }
                catch
                {
                }
            }

            return null;
        }

        private static string ExtractFullContainingTypeName(EditorCommandTarget target)
        {
            var documentationCommentId = target != null ? target.DocumentationCommentId ?? string.Empty : string.Empty;
            if (!string.IsNullOrEmpty(documentationCommentId))
            {
                var colonIndex = documentationCommentId.IndexOf(':');
                if (colonIndex >= 0 && colonIndex + 1 < documentationCommentId.Length)
                {
                    var body = documentationCommentId.Substring(colonIndex + 1);
                    var parenIndex = body.IndexOf('(');
                    if (parenIndex >= 0)
                    {
                        body = body.Substring(0, parenIndex);
                    }

                    var lastDot = body.LastIndexOf('.');
                    if (lastDot > 0)
                    {
                        return body.Substring(0, lastDot).Replace('+', '.');
                    }
                }
            }

            var qualifiedSymbolDisplay = target != null ? target.QualifiedSymbolDisplay ?? string.Empty : string.Empty;
            if (!string.IsNullOrEmpty(qualifiedSymbolDisplay))
            {
                var parenIndex = qualifiedSymbolDisplay.IndexOf('(');
                var memberPath = parenIndex >= 0
                    ? qualifiedSymbolDisplay.Substring(0, parenIndex)
                    : qualifiedSymbolDisplay;
                var lastDot = memberPath.LastIndexOf('.');
                if (lastDot > 0)
                {
                    var typePath = memberPath.Substring(0, lastDot).Trim();
                    var spaceIndex = typePath.LastIndexOf(' ');
                    if (spaceIndex >= 0 && spaceIndex + 1 < typePath.Length)
                    {
                        typePath = typePath.Substring(spaceIndex + 1);
                    }

                    if (!string.IsNullOrEmpty(typePath))
                    {
                        return typePath.Replace('+', '.');
                    }
                }
            }

            return target != null ? target.ContainingTypeName ?? string.Empty : string.Empty;
        }

        private static bool TryResolveCandidate(EditorCommandTarget target, List<MethodBase> candidates, out MethodBase method)
        {
            method = null;
            if (candidates == null || candidates.Count == 0)
            {
                return false;
            }

            if (candidates.Count == 1)
            {
                method = candidates[0];
                return true;
            }

            var qualifiedSymbolDisplay = target != null ? target.QualifiedSymbolDisplay ?? string.Empty : string.Empty;
            bool hasSignature;
            var expectedParameters = ParseQualifiedDisplayParameterTypes(qualifiedSymbolDisplay, out hasSignature);
            if (!hasSignature)
            {
                return false;
            }

            var matches = new List<MethodBase>();
            for (var i = 0; i < candidates.Count; i++)
            {
                if (MatchesSignature(candidates[i], expectedParameters))
                {
                    matches.Add(candidates[i]);
                }
            }

            if (matches.Count == 1)
            {
                method = matches[0];
                return true;
            }

            return false;
        }

        private static MethodBase ResolveMethodByDocumentationId(Assembly assembly, string documentationCommentId)
        {
            if (assembly == null || string.IsNullOrEmpty(documentationCommentId))
            {
                return null;
            }

            try
            {
                var types = assembly.GetTypes();
                for (var typeIndex = 0; typeIndex < types.Length; typeIndex++)
                {
                    var type = types[typeIndex];
                    if (type == null)
                    {
                        continue;
                    }

                    var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
                    var methods = type.GetMethods(flags);
                    for (var methodIndex = 0; methodIndex < methods.Length; methodIndex++)
                    {
                        if (string.Equals(BuildMethodDocumentationId(methods[methodIndex]), documentationCommentId, StringComparison.Ordinal))
                        {
                            return methods[methodIndex];
                        }
                    }

                    var constructors = type.GetConstructors(flags);
                    for (var ctorIndex = 0; ctorIndex < constructors.Length; ctorIndex++)
                    {
                        if (string.Equals(BuildMethodDocumentationId(constructors[ctorIndex]), documentationCommentId, StringComparison.Ordinal))
                        {
                            return constructors[ctorIndex];
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static string BuildMethodDisplayName(MethodBase method)
        {
            if (method == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append(method.Name ?? string.Empty);
            builder.Append("(");
            var parameters = method.GetParameters();
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(parameters[i].ParameterType != null ? parameters[i].ParameterType.Name ?? string.Empty : string.Empty);
            }

            builder.Append(")");
            return builder.ToString();
        }

        private static List<string> ParseQualifiedDisplayParameterTypes(string qualifiedSymbolDisplay, out bool hasSignature)
        {
            var parameters = new List<string>();
            hasSignature = false;
            if (string.IsNullOrEmpty(qualifiedSymbolDisplay))
            {
                return parameters;
            }

            var start = qualifiedSymbolDisplay.IndexOf('(');
            var end = qualifiedSymbolDisplay.LastIndexOf(')');
            if (start < 0 || end <= start)
            {
                return parameters;
            }

            hasSignature = true;
            var parameterList = qualifiedSymbolDisplay.Substring(start + 1, end - start - 1);
            if (string.IsNullOrWhiteSpace(parameterList))
            {
                return parameters;
            }

            var entries = SplitParameterList(parameterList);
            for (var i = 0; i < entries.Count; i++)
            {
                var normalized = NormalizeParameterDisplay(entries[i]);
                if (!string.IsNullOrEmpty(normalized))
                {
                    parameters.Add(normalized);
                }
            }

            return parameters;
        }

        private static List<string> SplitParameterList(string parameterList)
        {
            var entries = new List<string>();
            if (string.IsNullOrEmpty(parameterList))
            {
                return entries;
            }

            var start = 0;
            var angleDepth = 0;
            var braceDepth = 0;
            var bracketDepth = 0;
            for (var i = 0; i < parameterList.Length; i++)
            {
                var ch = parameterList[i];
                switch (ch)
                {
                    case '<':
                        angleDepth++;
                        break;
                    case '>':
                        angleDepth = Math.Max(0, angleDepth - 1);
                        break;
                    case '{':
                        braceDepth++;
                        break;
                    case '}':
                        braceDepth = Math.Max(0, braceDepth - 1);
                        break;
                    case '[':
                        bracketDepth++;
                        break;
                    case ']':
                        bracketDepth = Math.Max(0, bracketDepth - 1);
                        break;
                    case ',':
                        if (angleDepth == 0 && braceDepth == 0 && bracketDepth == 0)
                        {
                            entries.Add(parameterList.Substring(start, i - start));
                            start = i + 1;
                        }
                        break;
                }
            }

            if (start <= parameterList.Length)
            {
                entries.Add(parameterList.Substring(start));
            }

            return entries;
        }

        private static string NormalizeParameterDisplay(string parameterDisplay)
        {
            if (string.IsNullOrWhiteSpace(parameterDisplay))
            {
                return string.Empty;
            }

            var value = parameterDisplay.Trim();
            var equalsIndex = value.IndexOf('=');
            if (equalsIndex >= 0)
            {
                value = value.Substring(0, equalsIndex).Trim();
            }

            for (var i = 0; i < ParameterModifiers.Length; i++)
            {
                if (value.StartsWith(ParameterModifiers[i], StringComparison.Ordinal))
                {
                    value = value.Substring(ParameterModifiers[i].Length).Trim();
                    break;
                }
            }

            var lastSpaceIndex = FindLastTopLevelSpace(value);
            if (lastSpaceIndex > 0)
            {
                value = value.Substring(0, lastSpaceIndex).Trim();
            }

            return NormalizeTypeName(value);
        }

        private static int FindLastTopLevelSpace(string value)
        {
            var angleDepth = 0;
            var braceDepth = 0;
            var bracketDepth = 0;
            for (var i = value.Length - 1; i >= 0; i--)
            {
                var ch = value[i];
                switch (ch)
                {
                    case '>':
                        angleDepth++;
                        break;
                    case '<':
                        angleDepth = Math.Max(0, angleDepth - 1);
                        break;
                    case '}':
                        braceDepth++;
                        break;
                    case '{':
                        braceDepth = Math.Max(0, braceDepth - 1);
                        break;
                    case ']':
                        bracketDepth++;
                        break;
                    case '[':
                        bracketDepth = Math.Max(0, bracketDepth - 1);
                        break;
                    case ' ':
                        if (angleDepth == 0 && braceDepth == 0 && bracketDepth == 0)
                        {
                            return i;
                        }
                        break;
                }
            }

            return -1;
        }

        private static bool MatchesSignature(MethodBase method, List<string> expectedParameters)
        {
            if (method == null || expectedParameters == null)
            {
                return false;
            }

            var parameters = method.GetParameters();
            if (parameters.Length != expectedParameters.Count)
            {
                return false;
            }

            for (var i = 0; i < parameters.Length; i++)
            {
                if (!TypeMatchesDisplay(parameters[i].ParameterType, expectedParameters[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TypeMatchesDisplay(Type parameterType, string expectedTypeName)
        {
            if (parameterType == null || string.IsNullOrEmpty(expectedTypeName))
            {
                return false;
            }

            return string.Equals(NormalizeTypeName(GetReadableTypeName(parameterType)), expectedTypeName, StringComparison.Ordinal) ||
                string.Equals(NormalizeTypeName(GetXmlTypeName(parameterType)), expectedTypeName, StringComparison.Ordinal) ||
                string.Equals(NormalizeTypeName(parameterType.FullName ?? string.Empty), expectedTypeName, StringComparison.Ordinal) ||
                string.Equals(NormalizeTypeName(parameterType.Name ?? string.Empty), expectedTypeName, StringComparison.Ordinal);
        }

        private static string GetReadableTypeName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.IsByRef)
            {
                return GetReadableTypeName(type.GetElementType()) + "@";
            }

            if (type.IsArray)
            {
                return GetReadableTypeName(type.GetElementType()) + "[]";
            }

            var alias = TryGetKeywordAlias(type);
            if (!string.IsNullOrEmpty(alias))
            {
                return alias;
            }

            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                var name = genericType.FullName ?? genericType.Name ?? string.Empty;
                var tickIndex = name.IndexOf('`');
                if (tickIndex >= 0)
                {
                    name = name.Substring(0, tickIndex);
                }

                var args = type.GetGenericArguments();
                var builder = new StringBuilder();
                builder.Append(name.Replace('+', '.'));
                builder.Append("<");
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }

                    builder.Append(GetReadableTypeName(args[i]));
                }

                builder.Append(">");
                return builder.ToString();
            }

            return (type.FullName ?? type.Name ?? string.Empty).Replace('+', '.');
        }

        private static string NormalizeTypeName(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return string.Empty;
            }

            var normalized = typeName.Trim().Replace('+', '.');
            normalized = normalized.Replace("System.String", "string");
            normalized = normalized.Replace("System.Boolean", "bool");
            normalized = normalized.Replace("System.Byte", "byte");
            normalized = normalized.Replace("System.SByte", "sbyte");
            normalized = normalized.Replace("System.Int16", "short");
            normalized = normalized.Replace("System.UInt16", "ushort");
            normalized = normalized.Replace("System.Int32", "int");
            normalized = normalized.Replace("System.UInt32", "uint");
            normalized = normalized.Replace("System.Int64", "long");
            normalized = normalized.Replace("System.UInt64", "ulong");
            normalized = normalized.Replace("System.Single", "float");
            normalized = normalized.Replace("System.Double", "double");
            normalized = normalized.Replace("System.Decimal", "decimal");
            normalized = normalized.Replace("System.Object", "object");
            normalized = normalized.Replace("System.Char", "char");
            normalized = normalized.Replace("System.Void", "void");
            normalized = normalized.Replace("{", "<").Replace("}", ">");
            return normalized;
        }

        private static string TryGetKeywordAlias(Type type)
        {
            if (type == typeof(string))
            {
                return "string";
            }

            if (type == typeof(bool))
            {
                return "bool";
            }

            if (type == typeof(byte))
            {
                return "byte";
            }

            if (type == typeof(sbyte))
            {
                return "sbyte";
            }

            if (type == typeof(short))
            {
                return "short";
            }

            if (type == typeof(ushort))
            {
                return "ushort";
            }

            if (type == typeof(int))
            {
                return "int";
            }

            if (type == typeof(uint))
            {
                return "uint";
            }

            if (type == typeof(long))
            {
                return "long";
            }

            if (type == typeof(ulong))
            {
                return "ulong";
            }

            if (type == typeof(float))
            {
                return "float";
            }

            if (type == typeof(double))
            {
                return "double";
            }

            if (type == typeof(decimal))
            {
                return "decimal";
            }

            if (type == typeof(object))
            {
                return "object";
            }

            if (type == typeof(char))
            {
                return "char";
            }

            if (type == typeof(void))
            {
                return "void";
            }

            return string.Empty;
        }

        private static string GetTypeSymbolKind(Type type)
        {
            if (type == null)
            {
                return "NamedType";
            }

            if (type.IsInterface)
            {
                return "Interface";
            }

            if (type.IsEnum)
            {
                return "Enum";
            }

            if (typeof(MulticastDelegate).IsAssignableFrom(type.BaseType))
            {
                return "Delegate";
            }

            if (type.IsValueType)
            {
                return "Struct";
            }

            return "Class";
        }

        private static string GetNormalizedTypeName(Type type)
        {
            return type != null
                ? (type.FullName ?? type.Name ?? string.Empty).Replace('+', '.')
                : string.Empty;
        }

        private static string BuildTypeDocumentationId(Type type)
        {
            return type != null
                ? "T:" + GetXmlTypeName(type)
                : string.Empty;
        }

        private static string BuildMethodDocumentationId(MethodBase method)
        {
            if (method == null || method.DeclaringType == null)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            builder.Append("M:");
            builder.Append(GetXmlTypeName(method.DeclaringType));
            builder.Append(".");
            builder.Append(method.Name);

            var parameters = method.GetParameters();
            if (parameters.Length > 0)
            {
                builder.Append("(");
                for (var i = 0; i < parameters.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(GetXmlTypeName(parameters[i].ParameterType));
                }

                builder.Append(")");
            }

            return builder.ToString();
        }

        private static string GetXmlTypeName(Type type)
        {
            if (type == null)
            {
                return string.Empty;
            }

            if (type.IsByRef)
            {
                return GetXmlTypeName(type.GetElementType()) + "@";
            }

            if (type.IsArray)
            {
                return GetXmlTypeName(type.GetElementType()) + "[]";
            }

            if (type.IsGenericType)
            {
                var genericType = type.GetGenericTypeDefinition();
                var baseName = (genericType.FullName ?? genericType.Name).Replace('+', '.');
                var tickIndex = baseName.IndexOf('`');
                if (tickIndex >= 0)
                {
                    baseName = baseName.Substring(0, tickIndex);
                }

                var args = type.GetGenericArguments();
                var builder = new StringBuilder();
                builder.Append(baseName);
                builder.Append("{");
                for (var i = 0; i < args.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(GetXmlTypeName(args[i]));
                }

                builder.Append("}");
                return builder.ToString();
            }

            return (type.FullName ?? type.Name ?? string.Empty).Replace('+', '.');
        }
    }
}
