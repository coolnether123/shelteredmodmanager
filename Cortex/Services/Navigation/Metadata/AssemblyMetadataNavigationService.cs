using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;

namespace Cortex.Services.Navigation.Metadata
{
    internal sealed class MetadataNavigationTarget
    {
        public int MetadataToken;
        public DecompilerEntityKind EntityKind;
    }

    internal sealed class MethodNavigationTarget
    {
        public int DeclaringTypeMetadataToken;
        public string MethodName = string.Empty;
        public string ContainingTypeName = string.Empty;
        public string SymbolKind = "Method";
    }

    internal interface IAssemblyMetadataNavigationService
    {
        bool TryResolveAssemblyPath(CortexShellState state, ISourceLookupIndex sourceLookupIndex, string assemblyName, out string assemblyPath);
        bool TryResolveMetadataTarget(string assemblyPath, string documentationCommentId, string containingTypeName, string symbolKind, out MetadataNavigationTarget target);
        bool TryResolveMethodNavigationTarget(string assemblyPath, int methodMetadataToken, out MethodNavigationTarget target);
        bool TryResolveTypeNavigationTarget(string assemblyPath, int typeMetadataToken, out string fullTypeName);
    }

    internal sealed class AssemblyMetadataNavigationService : IAssemblyMetadataNavigationService
    {
        private readonly IRuntimeAssemblyMemberService _runtimeAssemblyMemberService;

        public AssemblyMetadataNavigationService()
            : this(new RuntimeAssemblyMemberService())
        {
        }

        internal AssemblyMetadataNavigationService(IRuntimeAssemblyMemberService runtimeAssemblyMemberService)
        {
            _runtimeAssemblyMemberService = runtimeAssemblyMemberService ?? new RuntimeAssemblyMemberService();
        }

        public bool TryResolveAssemblyPath(CortexShellState state, ISourceLookupIndex sourceLookupIndex, string assemblyName, out string assemblyPath)
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

        public bool TryResolveMetadataTarget(string assemblyPath, string documentationCommentId, string containingTypeName, string symbolKind, out MetadataNavigationTarget target)
        {
            target = null;

            var assembly = _runtimeAssemblyMemberService.LoadAssembly(assemblyPath);
            if (assembly == null)
            {
                return false;
            }

            var normalizedContainingTypeName = NormalizeMetadataTypeName(containingTypeName);
            if (IsTypeLikeSymbol(symbolKind))
            {
                var type = _runtimeAssemblyMemberService.ResolveTypeByName(
                    assembly,
                    !string.IsNullOrEmpty(documentationCommentId) && documentationCommentId.StartsWith("T:", StringComparison.Ordinal)
                        ? documentationCommentId.Substring(2)
                        : normalizedContainingTypeName);
                if (type == null)
                {
                    return false;
                }

                target = new MetadataNavigationTarget
                {
                    MetadataToken = type.MetadataToken,
                    EntityKind = DecompilerEntityKind.Type
                };
                return true;
            }

            if (!string.IsNullOrEmpty(documentationCommentId) && documentationCommentId.StartsWith("M:", StringComparison.Ordinal))
            {
                var method = _runtimeAssemblyMemberService.ResolveMethodByDocumentationId(assembly, documentationCommentId);
                if (method != null)
                {
                    target = new MetadataNavigationTarget
                    {
                        MetadataToken = method.MetadataToken,
                        EntityKind = DecompilerEntityKind.Method
                    };
                    return true;
                }
            }

            var containingType = _runtimeAssemblyMemberService.ResolveTypeByName(assembly, normalizedContainingTypeName);
            if (containingType == null)
            {
                return false;
            }

            target = new MetadataNavigationTarget
            {
                MetadataToken = containingType.MetadataToken,
                EntityKind = DecompilerEntityKind.Type
            };
            return true;
        }

        public bool TryResolveMethodNavigationTarget(string assemblyPath, int methodMetadataToken, out MethodNavigationTarget target)
        {
            target = null;

            var assembly = _runtimeAssemblyMemberService.LoadAssembly(assemblyPath);
            if (assembly == null)
            {
                return false;
            }

            var method = _runtimeAssemblyMemberService.ResolveMethod(assembly, methodMetadataToken);
            if (method == null || method.DeclaringType == null)
            {
                return false;
            }

            target = new MethodNavigationTarget
            {
                DeclaringTypeMetadataToken = method.DeclaringType.MetadataToken,
                MethodName = method.Name ?? string.Empty,
                ContainingTypeName = method.DeclaringType.FullName ?? method.DeclaringType.Name ?? string.Empty,
                SymbolKind = method.IsConstructor ? "Constructor" : "Method"
            };
            return target.DeclaringTypeMetadataToken > 0;
        }

        public bool TryResolveTypeNavigationTarget(string assemblyPath, int typeMetadataToken, out string fullTypeName)
        {
            fullTypeName = string.Empty;

            var assembly = _runtimeAssemblyMemberService.LoadAssembly(assemblyPath);
            if (assembly == null)
            {
                return false;
            }

            var type = _runtimeAssemblyMemberService.ResolveType(assembly, typeMetadataToken);
            if (type == null)
            {
                return false;
            }

            fullTypeName = type.FullName ?? type.Name ?? string.Empty;
            return !string.IsNullOrEmpty(fullTypeName);
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
