using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Xunit;

namespace Cortex.Tests.Core
{
    public sealed class DecompilerExplorerServiceTests
    {
        [Fact]
        public void EnsureChildren_GroupsTypesByNamespace_AndKeepsTypeNodesExpandable()
        {
            var assemblyPath = @"C:\Game\Managed\Assembly-CSharp.dll";
            var catalog = new FakeReferenceCatalogService(
                new[]
                {
                    new ReferenceAssemblyDescriptor
                    {
                        DisplayName = "Assembly-CSharp",
                        AssemblyPath = assemblyPath
                    }
                },
                new[]
                {
                    new ReferenceTypeDescriptor
                    {
                        DisplayName = "Game.BaseStat",
                        FullName = "Game.BaseStat",
                        AssemblyPath = assemblyPath,
                        MetadataToken = 101
                    },
                    new ReferenceTypeDescriptor
                    {
                        DisplayName = "Game.BaseStats",
                        FullName = "Game.BaseStats",
                        AssemblyPath = assemblyPath,
                        MetadataToken = 102
                    },
                    new ReferenceTypeDescriptor
                    {
                        DisplayName = "GlobalType",
                        FullName = "GlobalType",
                        AssemblyPath = assemblyPath,
                        MetadataToken = 103
                    }
                },
                new[]
                {
                    new ReferenceMemberDescriptor
                    {
                        DisplayName = "GetValue()",
                        AssemblyPath = assemblyPath,
                        DeclaringTypeName = "Game.BaseStat",
                        MetadataToken = 201
                    },
                    new ReferenceMemberDescriptor
                    {
                        DisplayName = "Ctor()",
                        AssemblyPath = assemblyPath,
                        DeclaringTypeName = "GlobalType",
                        MetadataToken = 202
                    }
                });
            var service = new DecompilerExplorerService(catalog);

            var root = service.BuildTree(@"C:\Game\Managed");
            Assert.NotNull(root);
            Assert.Single(root.Children);

            var assemblyNode = root.Children[0];
            service.EnsureChildren(assemblyNode);

            Assert.Equal(2, assemblyNode.Children.Count);
            Assert.Equal(WorkspaceTreeNodeKind.Folder, assemblyNode.Children[0].NodeKind);
            Assert.Equal("Game", assemblyNode.Children[0].Name);
            Assert.Equal(WorkspaceTreeNodeKind.Type, assemblyNode.Children[1].NodeKind);
            Assert.Equal("GlobalType", assemblyNode.Children[1].Name);
            Assert.True(assemblyNode.Children[1].HasChildren);
            Assert.False(assemblyNode.Children[1].ChildrenLoaded);

            var namespaceFolder = assemblyNode.Children[0];
            Assert.Equal(2, namespaceFolder.Children.Count);
            Assert.All(namespaceFolder.Children, delegate(WorkspaceTreeNode child)
            {
                Assert.Equal(WorkspaceTreeNodeKind.Type, child.NodeKind);
                Assert.True(child.HasChildren);
                Assert.False(child.ChildrenLoaded);
            });
            Assert.Equal("BaseStat", namespaceFolder.Children[0].Name);
            Assert.Equal("BaseStats", namespaceFolder.Children[1].Name);

            var baseStatType = namespaceFolder.Children[0];
            service.EnsureChildren(baseStatType);
            Assert.Single(baseStatType.Children);
            Assert.Equal(WorkspaceTreeNodeKind.Member, baseStatType.Children[0].NodeKind);
            Assert.Equal("GetValue()", baseStatType.Children[0].Name);
        }

        private sealed class FakeReferenceCatalogService : IReferenceCatalogService
        {
            private readonly IList<ReferenceAssemblyDescriptor> _assemblies;
            private readonly IList<ReferenceTypeDescriptor> _types;
            private readonly IList<ReferenceMemberDescriptor> _members;

            public FakeReferenceCatalogService(
                IList<ReferenceAssemblyDescriptor> assemblies,
                IList<ReferenceTypeDescriptor> types,
                IList<ReferenceMemberDescriptor> members)
            {
                _assemblies = assemblies ?? new List<ReferenceAssemblyDescriptor>();
                _types = types ?? new List<ReferenceTypeDescriptor>();
                _members = members ?? new List<ReferenceMemberDescriptor>();
            }

            public IList<ReferenceAssemblyDescriptor> GetAssemblies(string preferredRootPath)
            {
                return new List<ReferenceAssemblyDescriptor>(_assemblies);
            }

            public IList<ReferenceTypeDescriptor> GetTypes(string assemblyPath)
            {
                return new List<ReferenceTypeDescriptor>(_types);
            }

            public IList<ReferenceMemberDescriptor> GetMembers(string assemblyPath, string typeName)
            {
                var matches = new List<ReferenceMemberDescriptor>();
                for (var i = 0; i < _members.Count; i++)
                {
                    if (_members[i] != null && _members[i].DeclaringTypeName == typeName)
                    {
                        matches.Add(_members[i]);
                    }
                }

                return matches;
            }
        }
    }
}
