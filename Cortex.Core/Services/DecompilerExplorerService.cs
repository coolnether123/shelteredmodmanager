using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Core.Services
{
    public sealed class DecompilerExplorerService : IDecompilerExplorerService
    {
        private readonly IReferenceCatalogService _referenceCatalogService;

        public DecompilerExplorerService(IReferenceCatalogService referenceCatalogService)
        {
            _referenceCatalogService = referenceCatalogService;
        }

        public WorkspaceTreeNode BuildTree(string preferredRootPath)
        {
            var root = new WorkspaceTreeNode
            {
                Name = "Decompiler",
                FullPath = preferredRootPath ?? string.Empty,
                RelativePath = string.Empty,
                IsDirectory = true,
                HasChildren = true,
                ChildrenLoaded = true,
                IsVirtual = true,
                NodeKind = WorkspaceTreeNodeKind.DecompilerRoot
            };

            var assemblies = _referenceCatalogService != null
                ? _referenceCatalogService.GetAssemblies(preferredRootPath)
                : new List<ReferenceAssemblyDescriptor>();

            for (var i = 0; i < assemblies.Count; i++)
            {
                var assembly = assemblies[i];
                if (assembly == null || string.IsNullOrEmpty(assembly.AssemblyPath))
                {
                    continue;
                }

                root.Children.Add(new WorkspaceTreeNode
                {
                    Name = string.IsNullOrEmpty(assembly.DisplayName) ? assembly.AssemblyPath : assembly.DisplayName,
                    FullPath = assembly.AssemblyPath,
                    RelativePath = assembly.AssemblyPath,
                    IsDirectory = true,
                    HasChildren = true,
                    ChildrenLoaded = false,
                    IsVirtual = true,
                    NodeKind = WorkspaceTreeNodeKind.Assembly,
                    AssemblyPath = assembly.AssemblyPath,
                    EntityKind = DecompilerEntityKind.Type
                });
            }

            root.HasChildren = root.Children.Count > 0;
            return root;
        }

        public void EnsureChildren(WorkspaceTreeNode node)
        {
            if (node == null || node.ChildrenLoaded || _referenceCatalogService == null)
            {
                return;
            }

            node.Children.Clear();
            if (node.NodeKind == WorkspaceTreeNodeKind.Assembly)
            {
                PopulateTypes(node);
            }
            else if (node.NodeKind == WorkspaceTreeNodeKind.Type)
            {
                PopulateMembers(node);
            }

            node.ChildrenLoaded = true;
            node.HasChildren = node.Children.Count > 0;
        }

        private void PopulateTypes(WorkspaceTreeNode assemblyNode)
        {
            var types = _referenceCatalogService.GetTypes(assemblyNode.AssemblyPath);
            for (var i = 0; i < types.Count; i++)
            {
                var type = types[i];
                if (type == null || string.IsNullOrEmpty(type.FullName))
                {
                    continue;
                }

                assemblyNode.Children.Add(new WorkspaceTreeNode
                {
                    Name = type.DisplayName,
                    FullPath = BuildSymbolPath(assemblyNode.AssemblyPath, type.FullName),
                    RelativePath = type.FullName,
                    IsDirectory = true,
                    HasChildren = true,
                    ChildrenLoaded = false,
                    IsVirtual = true,
                    NodeKind = WorkspaceTreeNodeKind.Type,
                    AssemblyPath = type.AssemblyPath,
                    TypeName = type.FullName,
                    MetadataToken = type.MetadataToken,
                    EntityKind = DecompilerEntityKind.Type
                });
            }
        }

        private void PopulateMembers(WorkspaceTreeNode typeNode)
        {
            var members = _referenceCatalogService.GetMembers(typeNode.AssemblyPath, typeNode.TypeName);
            for (var i = 0; i < members.Count; i++)
            {
                var member = members[i];
                if (member == null || string.IsNullOrEmpty(member.DisplayName))
                {
                    continue;
                }

                typeNode.Children.Add(new WorkspaceTreeNode
                {
                    Name = member.DisplayName,
                    FullPath = BuildSymbolPath(member.AssemblyPath, member.DeclaringTypeName + "." + member.DisplayName),
                    RelativePath = member.DisplayName,
                    IsDirectory = false,
                    HasChildren = false,
                    ChildrenLoaded = true,
                    IsVirtual = true,
                    NodeKind = WorkspaceTreeNodeKind.Member,
                    AssemblyPath = member.AssemblyPath,
                    TypeName = member.DeclaringTypeName,
                    MetadataToken = member.MetadataToken,
                    EntityKind = DecompilerEntityKind.Method
                });
            }
        }

        private static string BuildSymbolPath(string assemblyPath, string memberPath)
        {
            var left = assemblyPath ?? string.Empty;
            var right = memberPath ?? string.Empty;
            return left + "::" + right;
        }
    }
}
