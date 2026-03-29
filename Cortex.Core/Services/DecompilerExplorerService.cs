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

                AddTypeNode(assemblyNode, type);
            }

            SortChildrenRecursive(assemblyNode);
        }

        private static void AddTypeNode(WorkspaceTreeNode assemblyNode, ReferenceTypeDescriptor type)
        {
            var namespacePath = GetNamespacePath(type.FullName);
            var parent = EnsureNamespaceFolderPath(assemblyNode, namespacePath);
            parent.Children.Add(new WorkspaceTreeNode
            {
                Name = GetTypeLeafName(type.FullName, type.DisplayName),
                FullPath = BuildSymbolPath(type.AssemblyPath, type.FullName),
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

            typeNode.Children.Sort(CompareNodes);
        }

        private static WorkspaceTreeNode EnsureNamespaceFolderPath(WorkspaceTreeNode assemblyNode, string namespacePath)
        {
            var parent = assemblyNode;
            if (string.IsNullOrEmpty(namespacePath))
            {
                return parent;
            }

            var segments = namespacePath.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
            var accumulated = string.Empty;
            for (var i = 0; i < segments.Length; i++)
            {
                var segment = segments[i];
                accumulated = string.IsNullOrEmpty(accumulated)
                    ? segment
                    : accumulated + "." + segment;

                var child = FindFolderChild(parent, accumulated);
                if (child == null)
                {
                    child = new WorkspaceTreeNode
                    {
                        Name = segment,
                        FullPath = BuildSymbolPath(assemblyNode.AssemblyPath, accumulated),
                        RelativePath = accumulated,
                        IsDirectory = true,
                        HasChildren = true,
                        ChildrenLoaded = true,
                        IsVirtual = true,
                        NodeKind = WorkspaceTreeNodeKind.Folder,
                        AssemblyPath = assemblyNode.AssemblyPath,
                        TypeName = accumulated
                    };
                    parent.Children.Add(child);
                }

                parent = child;
            }

            return parent;
        }

        private static WorkspaceTreeNode FindFolderChild(WorkspaceTreeNode parent, string relativePath)
        {
            if (parent == null)
            {
                return null;
            }

            for (var i = 0; i < parent.Children.Count; i++)
            {
                var child = parent.Children[i];
                if (child != null &&
                    child.NodeKind == WorkspaceTreeNodeKind.Folder &&
                    string.Equals(child.RelativePath, relativePath, StringComparison.Ordinal))
                {
                    return child;
                }
            }

            return null;
        }

        private static string GetNamespacePath(string fullTypeName)
        {
            if (string.IsNullOrEmpty(fullTypeName))
            {
                return string.Empty;
            }

            var lastDot = fullTypeName.LastIndexOf('.');
            return lastDot > 0
                ? fullTypeName.Substring(0, lastDot)
                : string.Empty;
        }

        private static string GetTypeLeafName(string fullTypeName, string displayName)
        {
            var typeName = !string.IsNullOrEmpty(fullTypeName)
                ? fullTypeName
                : (displayName ?? string.Empty);
            if (string.IsNullOrEmpty(typeName))
            {
                return "UnknownType";
            }

            var lastDot = typeName.LastIndexOf('.');
            var leaf = lastDot >= 0 && lastDot + 1 < typeName.Length
                ? typeName.Substring(lastDot + 1)
                : typeName;
            return leaf.Replace('+', '.');
        }

        private static void SortChildrenRecursive(WorkspaceTreeNode node)
        {
            if (node == null || node.Children.Count == 0)
            {
                return;
            }

            node.Children.Sort(CompareNodes);
            for (var i = 0; i < node.Children.Count; i++)
            {
                SortChildrenRecursive(node.Children[i]);
            }
        }

        private static int CompareNodes(WorkspaceTreeNode left, WorkspaceTreeNode right)
        {
            var leftIsFolder = left != null && left.NodeKind == WorkspaceTreeNodeKind.Folder;
            var rightIsFolder = right != null && right.NodeKind == WorkspaceTreeNodeKind.Folder;
            if (leftIsFolder != rightIsFolder)
            {
                return leftIsFolder ? -1 : 1;
            }

            var leftName = left != null ? left.Name ?? string.Empty : string.Empty;
            var rightName = right != null ? right.Name ?? string.Empty : string.Empty;
            return string.Compare(leftName, rightName, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildSymbolPath(string assemblyPath, string memberPath)
        {
            var left = assemblyPath ?? string.Empty;
            var right = memberPath ?? string.Empty;
            return left + "::" + right;
        }
    }
}
