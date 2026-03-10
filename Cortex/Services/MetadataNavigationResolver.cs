using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;

namespace Cortex.Services
{
    internal static class MetadataNavigationResolver
    {
        public static bool TryResolveAssemblyPath(global::Cortex.CortexShellState state, ISourceLookupIndex sourceLookupIndex, string assemblyName, out string assemblyPath)
        {
            assemblyPath = string.Empty;
            if (string.IsNullOrEmpty(assemblyName))
            {
                return false;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                var assembly = loadedAssemblies[i];
                if (assembly == null || !string.Equals(assembly.GetName().Name, assemblyName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    assemblyPath = assembly.Location;
                }
                catch
                {
                    assemblyPath = string.Empty;
                }

                if (!string.IsNullOrEmpty(assemblyPath) && File.Exists(assemblyPath))
                {
                    return true;
                }
            }

            var searchRoots = new List<string>();
            AddSearchRoot(searchRoots, AppDomain.CurrentDomain.BaseDirectory);
            var baseDirectory = AppDomain.CurrentDomain.BaseDirectory ?? string.Empty;
            var smmRoot = Path.Combine(baseDirectory, "SMM");
            var smmBinRoot = Path.Combine(smmRoot, "bin");
            AddSearchRoot(searchRoots, smmRoot);
            AddSearchRoot(searchRoots, smmBinRoot);
            AddSearchRoot(searchRoots, Path.Combine(smmBinRoot, "decompiler"));
            if (state != null && state.Settings != null)
            {
                var configuredRoots = SourceRootSetBuilder.Build(state.SelectedProject, state.Settings, SourceRootSetBuilder.LanguageServiceRoots);
                for (var i = 0; i < configuredRoots.Count; i++)
                {
                    AddSearchRoot(searchRoots, configuredRoots[i]);
                }

                if (!string.IsNullOrEmpty(state.Settings.DecompilerCachePath))
                {
                    AddSearchRoot(searchRoots, state.Settings.DecompilerCachePath);
                }
            }

            assemblyPath = sourceLookupIndex != null
                ? sourceLookupIndex.ResolveAssemblyPath(searchRoots, assemblyName)
                : string.Empty;
            return !string.IsNullOrEmpty(assemblyPath);
        }

        public static bool TryResolveMetadataTarget(string assemblyPath, string documentationCommentId, string containingTypeName, string symbolKind, out int metadataToken, out DecompilerEntityKind entityKind)
        {
            metadataToken = 0;
            entityKind = DecompilerEntityKind.Type;

            var assembly = LoadAssemblyForMetadata(assemblyPath);
            if (assembly == null)
            {
                return false;
            }

            var normalizedContainingTypeName = NormalizeMetadataTypeName(containingTypeName);
            if (IsTypeLikeSymbol(symbolKind))
            {
                var type = ResolveTypeByName(
                    assembly,
                    !string.IsNullOrEmpty(documentationCommentId) && documentationCommentId.StartsWith("T:", StringComparison.Ordinal)
                        ? documentationCommentId.Substring(2)
                        : normalizedContainingTypeName);
                if (type == null)
                {
                    return false;
                }

                metadataToken = type.MetadataToken;
                entityKind = DecompilerEntityKind.Type;
                return true;
            }

            if (!string.IsNullOrEmpty(documentationCommentId) && documentationCommentId.StartsWith("M:", StringComparison.Ordinal))
            {
                var method = ResolveMethodByDocumentationId(assembly, documentationCommentId);
                if (method != null)
                {
                    metadataToken = method.MetadataToken;
                    entityKind = DecompilerEntityKind.Method;
                    return true;
                }
            }

            var containingType = ResolveTypeByName(assembly, normalizedContainingTypeName);
            if (containingType == null)
            {
                return false;
            }

            metadataToken = containingType.MetadataToken;
            entityKind = DecompilerEntityKind.Type;
            return true;
        }

        private static void AddSearchRoot(List<string> roots, string root)
        {
            if (roots == null || string.IsNullOrEmpty(root))
            {
                return;
            }

            try
            {
                var fullPath = Path.GetFullPath(root);
                if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                {
                    return;
                }

                for (var i = 0; i < roots.Count; i++)
                {
                    if (string.Equals(roots[i], fullPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }

                roots.Add(fullPath);
            }
            catch
            {
            }
        }

        private static Assembly LoadAssemblyForMetadata(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                try
                {
                    if (string.Equals(loadedAssemblies[i].Location, assemblyPath, StringComparison.OrdinalIgnoreCase))
                    {
                        return loadedAssemblies[i];
                    }
                }
                catch
                {
                }
            }

            try
            {
                return File.Exists(assemblyPath) ? Assembly.LoadFrom(assemblyPath) : null;
            }
            catch
            {
                return null;
            }
        }

        private static Type ResolveTypeByName(Assembly assembly, string typeName)
        {
            if (assembly == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var normalized = NormalizeMetadataTypeName(typeName);
            try
            {
                var direct = assembly.GetType(normalized, false);
                if (direct != null)
                {
                    return direct;
                }

                var allTypes = assembly.GetTypes();
                for (var i = 0; i < allTypes.Length; i++)
                {
                    var type = allTypes[i];
                    if (type != null &&
                        string.Equals((type.FullName ?? type.Name).Replace('+', '.'), normalized, StringComparison.Ordinal))
                    {
                        return type;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        private static MethodBase ResolveMethodByDocumentationId(Assembly assembly, string documentationCommentId)
        {
            if (assembly == null || string.IsNullOrEmpty(documentationCommentId))
            {
                return null;
            }

            try
            {
                var allTypes = assembly.GetTypes();
                for (var typeIndex = 0; typeIndex < allTypes.Length; typeIndex++)
                {
                    var type = allTypes[typeIndex];
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

            return (type.FullName ?? type.Name).Replace('+', '.');
        }

        private static string NormalizeMetadataTypeName(string typeName)
        {
            return string.IsNullOrEmpty(typeName)
                ? string.Empty
                : typeName.Replace("global::", string.Empty).Replace('+', '.');
        }

        private static bool IsTypeLikeSymbol(string symbolKind)
        {
            return string.Equals(symbolKind, "NamedType", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(symbolKind, "Namespace", StringComparison.OrdinalIgnoreCase);
        }
    }
}
