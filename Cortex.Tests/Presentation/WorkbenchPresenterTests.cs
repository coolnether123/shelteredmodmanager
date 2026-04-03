using System.Linq;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Presentation.Services;
using Xunit;

namespace Cortex.Tests.Presentation
{
    public sealed class WorkbenchPresenterTests
    {
        [Fact]
        public void BuildSnapshot_UsesRuntimeStateAndPresentationMetadata()
        {
            var runtime = new TestWorkbenchRuntime();
            runtime.WorkbenchState.ActiveContainerId = "container.search";
            runtime.FocusState.FocusedRegionId = "editor.surface";
            runtime.ThemeState.ThemeId = "theme.test";

            var snapshot = new WorkbenchPresenter().BuildSnapshot(
                runtime,
                new WorkbenchPresentationMetadata
                {
                    RendererSummary = "Recording Renderer | Capabilities v3"
                });

            Assert.Equal("container.search", snapshot.ActiveContainerId);
            Assert.Equal("editor.surface", snapshot.FocusedRegionId);
            Assert.Equal("theme.test", snapshot.ActiveThemeId);
            Assert.Equal("Recording Renderer | Capabilities v3", snapshot.RendererSummary);
        }

        [Fact]
        public void BuildSnapshot_ProjectsContributionRegistryIntoShellFacingSnapshot()
        {
            var commandRegistry = new CommandRegistry();
            commandRegistry.Register(new CommandDefinition
            {
                CommandId = "command.open.review",
                DisplayName = "Open Review",
                Description = "Open the review surface.",
                DefaultGesture = "Ctrl+R"
            });
            commandRegistry.RegisterHandler(
                "command.open.review",
                delegate(CommandExecutionContext context) { },
                delegate(CommandExecutionContext context) { return true; });

            var contributionRegistry = new ContributionRegistry();
            contributionRegistry.RegisterIcon(new IconContribution
            {
                IconId = "container.review",
                Alias = "RV"
            });
            contributionRegistry.RegisterViewContainer(new ViewContainerContribution
            {
                ContainerId = "container.review",
                Title = "Review",
                IconId = "container.review",
                DefaultHostLocation = WorkbenchHostLocation.SecondarySideHost,
                SortOrder = 10
            });
            contributionRegistry.RegisterMenu(new MenuContribution
            {
                CommandId = "command.open.review",
                Group = "View",
                Location = MenuProjectionLocation.MainMenu,
                SortOrder = 10
            });
            contributionRegistry.RegisterMenu(new MenuContribution
            {
                CommandId = "command.open.review",
                Group = "Review",
                Location = MenuProjectionLocation.Toolbar,
                SortOrder = 20
            });
            contributionRegistry.RegisterStatusItem(new StatusItemContribution
            {
                ItemId = "status.review",
                Text = "Review Ready",
                Alignment = StatusItemAlignment.Left,
                Priority = 10
            });
            contributionRegistry.RegisterTheme(new ThemeContribution
            {
                ThemeId = "theme.review",
                DisplayName = "Review Theme",
                BackgroundColor = "#111111",
                SurfaceColor = "#222222",
                HeaderColor = "#333333",
                BorderColor = "#444444",
                AccentColor = "#555555",
                TextColor = "#EEEEEE",
                MutedTextColor = "#AAAAAA",
                WarningColor = "#BBBB00",
                ErrorColor = "#CC3300",
                FontRole = "review-font"
            });

            var runtime = new TestWorkbenchRuntime(commandRegistry, contributionRegistry);
            runtime.ThemeState.ThemeId = "theme.review";
            runtime.WorkbenchState.ActiveContainerId = "container.review";
            runtime.FocusState.FocusedRegionId = "review.focus";

            var snapshot = new WorkbenchPresenter().BuildSnapshot(runtime, new WorkbenchPresentationMetadata());

            var toolRailItem = Assert.Single(snapshot.ToolRailItems);
            Assert.Equal("container.review", toolRailItem.ContainerId);
            Assert.Equal("RV", toolRailItem.IconAlias);
            Assert.True(toolRailItem.Active);

            var mainMenuItem = Assert.Single(snapshot.MainMenuItems);
            Assert.Equal("command.open.review", mainMenuItem.CommandId);
            Assert.Equal("Open Review", mainMenuItem.DisplayName);

            var toolbarItem = Assert.Single(snapshot.ToolbarItems);
            Assert.Equal("command.open.review", toolbarItem.CommandId);

            Assert.Single(snapshot.LeftStatusItems);
            Assert.Equal("#111111", snapshot.ThemeTokens.BackgroundColor);
            Assert.Equal("review-font", snapshot.ThemeTokens.FontRole);
            Assert.Equal("review.focus", snapshot.FocusedRegionId);
            Assert.Equal("theme.review", snapshot.ActiveThemeId);
            Assert.Empty(snapshot.RightStatusItems);
            Assert.Equal(
                new[] { "container.review" },
                snapshot.ToolRailItems.Select(item => item.ContainerId).ToArray());
        }

        private sealed class TestWorkbenchRuntime : IWorkbenchRuntime
        {
            public TestWorkbenchRuntime()
                : this(new CommandRegistry(), new ContributionRegistry())
            {
            }

            public TestWorkbenchRuntime(ICommandRegistry commandRegistry, IContributionRegistry contributionRegistry)
            {
                CommandRegistry = commandRegistry;
                ContributionRegistry = contributionRegistry;
                WorkbenchState = new WorkbenchState();
                LayoutState = new LayoutState();
                StatusState = new StatusState();
                ThemeState = new ThemeState();
                FocusState = new FocusState();
            }

            public ICommandRegistry CommandRegistry { get; private set; }

            public IContributionRegistry ContributionRegistry { get; private set; }

            public WorkbenchState WorkbenchState { get; private set; }

            public LayoutState LayoutState { get; private set; }

            public StatusState StatusState { get; private set; }

            public ThemeState ThemeState { get; private set; }

            public FocusState FocusState { get; private set; }
        }
    }
}
