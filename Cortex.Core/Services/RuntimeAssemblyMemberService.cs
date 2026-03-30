using System;
using System.IO;
using System.Reflection;
using System.Text;

namespace Cortex.Core.Services
{
    public interface IRuntimeAssemblyMemberService
    {
        Assembly LoadAssembly(string assemblyPath);
        MethodBase ResolveMethod(string assemblyPath, int metadataToken);
        Type ResolveType(string assemblyPath, int metadataToken);
        MethodBase ResolveMethod(Assembly assembly, int metadataToken);
        Type ResolveType(Assembly assembly, int metadataToken);
        Type ResolveTypeByName(Assembly assembly, string typeName);
        MethodBase ResolveMethodByDocumentationId(Assembly assembly, string documentationCommentId);
        string BuildMethodDocumentationId(MethodBase method);
    }

    public sealed class RuntimeAssemblyMemberService : IRuntimeAssemblyMemberService
    {
        public Assembly LoadAssembly(string assemblyPath)
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
                    if (string.Equals(Path.GetFullPath(loadedAssemblies[i].Location), Path.GetFullPath(assemblyPath), StringComparison.OrdinalIgnoreCase))
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

        public MethodBase ResolveMethod(string assemblyPath, int metadataToken)
        {
            return ResolveMethod(LoadAssembly(assemblyPath), metadataToken);
        }

        public Type ResolveType(string assemblyPath, int metadataToken)
        {
            return ResolveType(LoadAssembly(assemblyPath), metadataToken);
        }

        public MethodBase ResolveMethod(Assembly assembly, int metadataToken)
        {
            if (assembly == null || metadataToken <= 0)
            {
                return null;
            }

            try
            {
                return assembly.ManifestModule.ResolveMethod(metadataToken);
            }
            catch
            {
                return null;
            }
        }

        public Type ResolveType(Assembly assembly, int metadataToken)
        {
            if (assembly == null || metadataToken <= 0)
            {
                return null;
            }

            try
            {
                return assembly.ManifestModule.ResolveType(metadataToken);
            }
            catch
            {
                return null;
            }
        }

        public Type ResolveTypeByName(Assembly assembly, string typeName)
        {
            if (assembly == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            var normalizedTypeName = NormalizeTypeName(typeName);
            try
            {
                var direct = assembly.GetType(normalizedTypeName, false);
                if (direct != null)
                {
                    return direct;
                }

                var allTypes = assembly.GetTypes();
                for (var i = 0; i < allTypes.Length; i++)
                {
                    var current = allTypes[i];
                    if (current == null)
                    {
                        continue;
                    }

                    var currentName = NormalizeTypeName(current.FullName ?? current.Name ?? string.Empty);
                    if (string.Equals(currentName, normalizedTypeName, StringComparison.Ordinal) ||
                        string.Equals(current.Name ?? string.Empty, normalizedTypeName, StringComparison.Ordinal))
                    {
                        return current;
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public MethodBase ResolveMethodByDocumentationId(Assembly assembly, string documentationCommentId)
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
                    for (var constructorIndex = 0; constructorIndex < constructors.Length; constructorIndex++)
                    {
                        if (string.Equals(BuildMethodDocumentationId(constructors[constructorIndex]), documentationCommentId, StringComparison.Ordinal))
                        {
                            return constructors[constructorIndex];
                        }
                    }
                }
            }
            catch
            {
            }

            return null;
        }

        public string BuildMethodDocumentationId(MethodBase method)
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
                var baseName = (genericType.FullName ?? genericType.Name ?? string.Empty).Replace('+', '.');
                var tickIndex = baseName.IndexOf('`');
                if (tickIndex >= 0)
                {
                    baseName = baseName.Substring(0, tickIndex);
                }

                var arguments = type.GetGenericArguments();
                var builder = new StringBuilder();
                builder.Append(baseName);
                builder.Append("{");
                for (var i = 0; i < arguments.Length; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(",");
                    }

                    builder.Append(GetXmlTypeName(arguments[i]));
                }

                builder.Append("}");
                return builder.ToString();
            }

            return (type.FullName ?? type.Name ?? string.Empty).Replace('+', '.');
        }

        private static string NormalizeTypeName(string typeName)
        {
            return string.IsNullOrEmpty(typeName)
                ? string.Empty
                : typeName.Replace("global::", string.Empty).Replace('+', '.');
        }
    }
}
