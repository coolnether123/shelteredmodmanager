using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class ReferenceCatalogService : IReferenceCatalogService
    {
        public IList<ReferenceAssemblyDescriptor> GetAssemblies(string preferredRootPath)
        {
            var results = new List<ReferenceAssemblyDescriptor>();
            var seen = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (var i = 0; i < assemblies.Length; i++)
            {
                var path = SafeAssemblyPath(assemblies[i]);
                if (string.IsNullOrEmpty(path) || seen.ContainsKey(path))
                {
                    continue;
                }

                seen[path] = true;
                results.Add(new ReferenceAssemblyDescriptor
                {
                    DisplayName = assemblies[i].GetName().Name,
                    AssemblyPath = path
                });
            }

            results.Sort(delegate(ReferenceAssemblyDescriptor left, ReferenceAssemblyDescriptor right)
            {
                var leftPreferred = IsPreferred(left != null ? left.AssemblyPath : string.Empty, preferredRootPath);
                var rightPreferred = IsPreferred(right != null ? right.AssemblyPath : string.Empty, preferredRootPath);
                if (leftPreferred != rightPreferred)
                {
                    return leftPreferred ? -1 : 1;
                }

                return string.Compare(left != null ? left.DisplayName : string.Empty, right != null ? right.DisplayName : string.Empty, StringComparison.OrdinalIgnoreCase);
            });

            return results;
        }

        public IList<ReferenceTypeDescriptor> GetTypes(string assemblyPath)
        {
            var results = new List<ReferenceTypeDescriptor>();
            var assembly = ResolveAssembly(assemblyPath);
            if (assembly == null)
            {
                return results;
            }

            Type[] types;
            try
            {
                types = assembly.GetTypes();
            }
            catch (ReflectionTypeLoadException ex)
            {
                types = ex.Types;
            }
            catch
            {
                return results;
            }

            for (var i = 0; i < types.Length; i++)
            {
                var type = types[i];
                if (type == null || type.IsNested)
                {
                    continue;
                }

                results.Add(new ReferenceTypeDescriptor
                {
                    DisplayName = type.FullName ?? type.Name,
                    FullName = type.FullName ?? type.Name,
                    AssemblyPath = assemblyPath,
                    MetadataToken = type.MetadataToken
                });
            }

            results.Sort(delegate(ReferenceTypeDescriptor left, ReferenceTypeDescriptor right)
            {
                return string.Compare(left != null ? left.DisplayName : string.Empty, right != null ? right.DisplayName : string.Empty, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        public IList<ReferenceMemberDescriptor> GetMembers(string assemblyPath, string typeName)
        {
            var results = new List<ReferenceMemberDescriptor>();
            var type = ResolveType(assemblyPath, typeName);
            if (type == null)
            {
                return results;
            }

            var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
            var methods = type.GetMethods(flags);
            for (var i = 0; i < methods.Length; i++)
            {
                results.Add(new ReferenceMemberDescriptor
                {
                    DisplayName = BuildMethodLabel(methods[i]),
                    AssemblyPath = assemblyPath,
                    DeclaringTypeName = type.FullName ?? type.Name,
                    MetadataToken = methods[i].MetadataToken
                });
            }

            var constructors = type.GetConstructors(flags);
            for (var i = 0; i < constructors.Length; i++)
            {
                results.Add(new ReferenceMemberDescriptor
                {
                    DisplayName = BuildMethodLabel(constructors[i]),
                    AssemblyPath = assemblyPath,
                    DeclaringTypeName = type.FullName ?? type.Name,
                    MetadataToken = constructors[i].MetadataToken
                });
            }

            results.Sort(delegate(ReferenceMemberDescriptor left, ReferenceMemberDescriptor right)
            {
                return string.Compare(left != null ? left.DisplayName : string.Empty, right != null ? right.DisplayName : string.Empty, StringComparison.OrdinalIgnoreCase);
            });
            return results;
        }

        private static Assembly ResolveAssembly(string assemblyPath)
        {
            if (string.IsNullOrEmpty(assemblyPath))
            {
                return null;
            }

            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < loadedAssemblies.Length; i++)
            {
                if (string.Equals(SafeAssemblyPath(loadedAssemblies[i]), assemblyPath, StringComparison.OrdinalIgnoreCase))
                {
                    return loadedAssemblies[i];
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

        private static Type ResolveType(string assemblyPath, string typeName)
        {
            var assembly = ResolveAssembly(assemblyPath);
            if (assembly == null || string.IsNullOrEmpty(typeName))
            {
                return null;
            }

            try
            {
                return assembly.GetType(typeName, false);
            }
            catch
            {
                return null;
            }
        }

        private static string SafeAssemblyPath(Assembly assembly)
        {
            try
            {
                return assembly != null ? assembly.Location : string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static bool IsPreferred(string assemblyPath, string preferredRootPath)
        {
            return !string.IsNullOrEmpty(assemblyPath) &&
                !string.IsNullOrEmpty(preferredRootPath) &&
                assemblyPath.StartsWith(preferredRootPath, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildMethodLabel(MethodBase method)
        {
            if (method == null)
            {
                return "Unknown";
            }

            var parameters = method.GetParameters();
            var builder = new StringBuilder();
            builder.Append(method.Name);
            builder.Append('(');
            for (var i = 0; i < parameters.Length; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }

                builder.Append(parameters[i].ParameterType.Name);
                builder.Append(' ');
                builder.Append(parameters[i].Name);
            }
            builder.Append(')');
            return builder.ToString();
        }
    }
}
