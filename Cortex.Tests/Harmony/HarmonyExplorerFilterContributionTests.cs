using Cortex.Core.Models;
using Cortex.Modules.Harmony;
using Xunit;

namespace Cortex.Tests.Harmony
{
    public sealed class HarmonyExplorerFilterContributionTests
    {
        [Fact]
        public void BuildPatchedNodeMatcher_LeavesUnpatchedMembersHiddenUnderPatchedType()
        {
            var assemblyPath = @"C:\Game\Managed\Assembly-CSharp.dll";
            var matcher = HarmonyExplorerFilterContributions.BuildPatchedNodeMatcher(new[]
            {
                new HarmonyMethodPatchSummary
                {
                    AssemblyPath = assemblyPath,
                    DeclaringType = "Game.UI.BasePanel",
                    MethodName = "Show",
                    IsPatched = true,
                    Target = new HarmonyPatchNavigationTarget
                    {
                        AssemblyPath = assemblyPath,
                        MetadataToken = 101,
                        DeclaringTypeName = "Game.UI.BasePanel",
                        MethodName = "Show"
                    }
                }
            });

            Assert.True(matcher(new WorkspaceTreeNode
            {
                NodeKind = WorkspaceTreeNodeKind.Type,
                AssemblyPath = assemblyPath,
                TypeName = "Game.UI.BasePanel"
            }));
            Assert.True(matcher(new WorkspaceTreeNode
            {
                NodeKind = WorkspaceTreeNodeKind.Member,
                AssemblyPath = assemblyPath,
                TypeName = "Game.UI.BasePanel",
                MetadataToken = 101,
                Name = "Show()"
            }));
            Assert.False(matcher(new WorkspaceTreeNode
            {
                NodeKind = WorkspaceTreeNodeKind.Member,
                AssemblyPath = assemblyPath,
                TypeName = "Game.UI.BasePanel",
                MetadataToken = 202,
                Name = "Close()"
            }));
        }

        [Fact]
        public void BuildPatchedNodeMatcher_ScopesToSelectedProjectOwnerAssociations()
        {
            var assemblyPath = @"C:\Game\Managed\Assembly-CSharp.dll";
            var context = new ExplorerFilterRuntimeContext
            {
                Scope = ExplorerFilterScope.Decompiler,
                RestrictToSelectedProject = true,
                SelectedProject = new CortexProjectDefinition
                {
                    ModId = "coolnether123.sheltereddisplayfixes",
                    SourceRootPath = @"D:\Projects\Sheltered Modding\Sheltered Display Fixes"
                }
            };
            var matcher = HarmonyExplorerFilterContributions.BuildPatchedNodeMatcher(new[]
            {
                new HarmonyMethodPatchSummary
                {
                    AssemblyPath = assemblyPath,
                    DeclaringType = "Game.UI.BasePanel",
                    MethodName = "Initialise",
                    IsPatched = true,
                    Target = new HarmonyPatchNavigationTarget
                    {
                        AssemblyPath = assemblyPath,
                        MetadataToken = 101,
                        DeclaringTypeName = "Game.UI.BasePanel",
                        MethodName = "Initialise"
                    },
                    Entries = new[]
                    {
                        new HarmonyPatchEntry
                        {
                            OwnerAssociation = new HarmonyPatchOwnerAssociation
                            {
                                ProjectModId = "coolnether123.sheltereddisplayfixes",
                                ProjectSourceRootPath = @"D:\Projects\Sheltered Modding\Sheltered Display Fixes"
                            }
                        }
                    }
                },
                new HarmonyMethodPatchSummary
                {
                    AssemblyPath = assemblyPath,
                    DeclaringType = "ModAPI.UI.ModManagerPanel",
                    MethodName = "Initialise",
                    IsPatched = true,
                    Target = new HarmonyPatchNavigationTarget
                    {
                        AssemblyPath = assemblyPath,
                        MetadataToken = 202,
                        DeclaringTypeName = "ModAPI.UI.ModManagerPanel",
                        MethodName = "Initialise"
                    },
                    Entries = new[]
                    {
                        new HarmonyPatchEntry
                        {
                            OwnerAssociation = new HarmonyPatchOwnerAssociation
                            {
                                ProjectModId = "ModAPI",
                                ProjectSourceRootPath = @"D:\Projects\Other\ModAPI"
                            }
                        }
                    }
                }
            }, context);

            Assert.True(matcher(new WorkspaceTreeNode
            {
                NodeKind = WorkspaceTreeNodeKind.Type,
                AssemblyPath = assemblyPath,
                TypeName = "Game.UI.BasePanel"
            }));
            Assert.False(matcher(new WorkspaceTreeNode
            {
                NodeKind = WorkspaceTreeNodeKind.Type,
                AssemblyPath = assemblyPath,
                TypeName = "ModAPI.UI.ModManagerPanel"
            }));
            Assert.False(matcher(new WorkspaceTreeNode
            {
                NodeKind = WorkspaceTreeNodeKind.Member,
                AssemblyPath = assemblyPath,
                TypeName = "ModAPI.UI.ModManagerPanel",
                MetadataToken = 202,
                Name = "Initialise()"
            }));
        }
    }
}
