using Cortex;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Services.Explorer;
using Xunit;

namespace Cortex.Tests.Explorer
{
    public sealed class ExplorerFilterPlanBuilderTests
    {
        [Fact]
        public void Build_AppliesTextAndActiveContributionFilters()
        {
            var registry = new ContributionRegistry();
            registry.RegisterExplorerFilter(new ExplorerFilterContribution
            {
                FilterId = "explorer.keep",
                DisplayName = "Keep",
                Scope = ExplorerFilterScope.Decompiler,
                CreateMatcher = delegate
                {
                    return delegate(WorkspaceTreeNode node)
                    {
                        return node != null && (node.Name ?? string.Empty).IndexOf("Keep", System.StringComparison.OrdinalIgnoreCase) >= 0;
                    };
                }
            });

            var state = new CortexShellState();
            state.Explorer.FilterText = "Target";
            state.Explorer.ActiveFilterIds.Add("explorer.keep");

            var plan = new ExplorerFilterPlanBuilder().Build(registry, state, ExplorerFilterScope.Decompiler);

            Assert.True(plan.Matches(new WorkspaceTreeNode
            {
                Name = "TargetKeep",
                NodeKind = WorkspaceTreeNodeKind.Member
            }));
            Assert.False(plan.Matches(new WorkspaceTreeNode
            {
                Name = "TargetDrop",
                NodeKind = WorkspaceTreeNodeKind.Member
            }));
            Assert.False(plan.Matches(new WorkspaceTreeNode
            {
                Name = "OtherKeep",
                NodeKind = WorkspaceTreeNodeKind.Member
            }));
        }

        [Fact]
        public void Build_KeepsParentVisibleWhenLoadedDescendantMatches()
        {
            var registry = new ContributionRegistry();
            registry.RegisterExplorerFilter(new ExplorerFilterContribution
            {
                FilterId = "explorer.child",
                DisplayName = "Child Match",
                Scope = ExplorerFilterScope.Workspace,
                CreateMatcher = delegate
                {
                    return delegate(WorkspaceTreeNode node)
                    {
                        return node != null && node.NodeKind == WorkspaceTreeNodeKind.File && node.Name == "MatchChild.cs";
                    };
                }
            });

            var state = new CortexShellState();
            state.Explorer.ActiveFilterIds.Add("explorer.child");

            var parent = new WorkspaceTreeNode
            {
                Name = "Parent",
                FullPath = @"C:\Workspace\Parent",
                NodeKind = WorkspaceTreeNodeKind.Folder,
                HasChildren = true,
                ChildrenLoaded = true
            };
            parent.Children.Add(new WorkspaceTreeNode
            {
                Name = "MatchChild.cs",
                FullPath = @"C:\Workspace\Parent\MatchChild.cs",
                NodeKind = WorkspaceTreeNodeKind.File
            });

            var plan = new ExplorerFilterPlanBuilder().Build(registry, state, ExplorerFilterScope.Workspace);

            Assert.True(plan.Matches(parent));
        }
    }
}
