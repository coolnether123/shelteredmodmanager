using Cortex.Core.Models;
using Cortex.Core.Services;

namespace Cortex.Tests.Testing
{
    internal sealed class OnboardingTestRegistryBuilder
    {
        public const string IdeProfileId = "tests.onboarding.profile.ide";
        public const string ModderProfileId = "tests.onboarding.profile.modder";
        public const string DecompilerProfileId = "tests.onboarding.profile.decompiler";
        public const string VisualStudioLayoutId = "tests.onboarding.layout.visual-studio";
        public const string DecompilerLayoutId = "tests.onboarding.layout.decompiler";
        public const string VisualStudioThemeId = "tests.theme.vs-dark";
        public const string DecompilerThemeId = "tests.theme.dotpeek-dark";
        public const string AccentThemeId = "tests.theme.accent";

        private readonly ContributionRegistry _registry = new ContributionRegistry();

        public static ContributionRegistry CreateDefault()
        {
            return new OnboardingTestRegistryBuilder()
                .WithDefaultThemes()
                .WithDefaultProfiles()
                .WithDefaultLayouts()
                .Build();
        }

        public OnboardingTestRegistryBuilder WithDefaultThemes()
        {
            return WithTheme(new ThemeContribution
            {
                ThemeId = VisualStudioThemeId,
                DisplayName = "Visual Studio Dark",
                Description = "Default IDE theme.",
                BackgroundColor = "#1E1E1E",
                SurfaceColor = "#252526",
                HeaderColor = "#2D2D30",
                BorderColor = "#3E3E42",
                AccentColor = "#007ACC",
                TextColor = "#F0F0F0",
                MutedTextColor = "#A0A0A0",
                WarningColor = "#F0AD4E",
                ErrorColor = "#D9534F",
                ShowInOnboarding = true,
                IsDefault = true,
                SortOrder = 0
            }).WithTheme(new ThemeContribution
            {
                ThemeId = DecompilerThemeId,
                DisplayName = "dotPeek Dark",
                Description = "Decompiler-first theme.",
                BackgroundColor = "#202020",
                SurfaceColor = "#2B2B2B",
                HeaderColor = "#303030",
                BorderColor = "#4A4A4A",
                AccentColor = "#8B5CF6",
                TextColor = "#EEEEEE",
                MutedTextColor = "#A8A8A8",
                WarningColor = "#D4A017",
                ErrorColor = "#E57373",
                ShowInOnboarding = true,
                SortOrder = 10
            }).WithTheme(new ThemeContribution
            {
                ThemeId = AccentThemeId,
                DisplayName = "Accent",
                Description = "Visible alternate theme.",
                BackgroundColor = "#101826",
                SurfaceColor = "#172033",
                HeaderColor = "#1F2940",
                BorderColor = "#30415F",
                AccentColor = "#00C2A8",
                TextColor = "#F2F6FF",
                MutedTextColor = "#98A8C0",
                WarningColor = "#FFB74D",
                ErrorColor = "#FF6B6B",
                ShowInOnboarding = true,
                SortOrder = 20
            });
        }

        public OnboardingTestRegistryBuilder WithDefaultProfiles()
        {
            return WithProfile(new OnboardingProfileContribution
            {
                ProfileId = IdeProfileId,
                DisplayName = "IDE",
                Description = "Best for coding, building, navigating, and editing.",
                DefaultLayoutPresetId = VisualStudioLayoutId,
                DefaultThemeId = VisualStudioThemeId,
                PreviewTags = new[] { "Code", "Build", "Navigate" },
                IsDefault = true,
                SortOrder = 0
            }).WithProfile(new OnboardingProfileContribution
            {
                ProfileId = ModderProfileId,
                DisplayName = "Modder",
                Description = "Best for linking live mods to source projects, building DLLs, and iterating in-game.",
                DefaultLayoutPresetId = VisualStudioLayoutId,
                DefaultThemeId = VisualStudioThemeId,
                PreviewTags = new[] { "Live Mods", "Source Roots", "Build DLLs" },
                WorkflowKind = OnboardingProfileWorkflowKind.Modder,
                SortOrder = 5
            }).WithProfile(new OnboardingProfileContribution
            {
                ProfileId = DecompilerProfileId,
                DisplayName = "Decompiler",
                Description = "Best for browsing assemblies, metadata, and generated source.",
                DefaultLayoutPresetId = DecompilerLayoutId,
                DefaultThemeId = DecompilerThemeId,
                PreviewTags = new[] { "Assemblies", "Metadata", "Generated Source" },
                SortOrder = 10
            });
        }

        public OnboardingTestRegistryBuilder WithDefaultLayouts()
        {
            return WithLayoutPreset(new OnboardingLayoutPresetContribution
            {
                LayoutPresetId = VisualStudioLayoutId,
                DisplayName = "Visual Studio",
                Description = "Editor-first with diagnostics below.",
                DefaultThemeId = VisualStudioThemeId,
                DefaultFocusedContainerId = CortexWorkbenchIds.EditorContainer,
                DefaultPrimarySideContainerId = CortexWorkbenchIds.ProjectsContainer,
                DefaultSecondarySideContainerId = CortexWorkbenchIds.FileExplorerContainer,
                DefaultPanelContainerId = CortexWorkbenchIds.LogsContainer,
                DefaultEditorContainerId = CortexWorkbenchIds.EditorContainer,
                PreviewPrimaryLabel = "Navigation",
                PreviewSecondaryLabel = "Solution Explorer",
                PreviewCenterLabel = "Editor",
                PreviewPanelLabel = "Output",
                ContainerHostAssignments = new[]
                {
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.ProjectsContainer, HostLocation = WorkbenchHostLocation.PrimarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.FileExplorerContainer, HostLocation = WorkbenchHostLocation.SecondarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.LogsContainer, HostLocation = WorkbenchHostLocation.PanelHost }
                },
                PrimarySideWidth = 300f,
                SecondarySideWidth = 360f,
                PanelSize = 240f,
                IsDefault = true,
                SortOrder = 0
            }).WithLayoutPreset(new OnboardingLayoutPresetContribution
            {
                LayoutPresetId = DecompilerLayoutId,
                DisplayName = "Decompiler",
                Description = "Tree first, code second.",
                DefaultThemeId = DecompilerThemeId,
                DefaultFocusedContainerId = CortexWorkbenchIds.EditorContainer,
                DefaultPrimarySideContainerId = CortexWorkbenchIds.FileExplorerContainer,
                DefaultSecondarySideContainerId = string.Empty,
                DefaultPanelContainerId = string.Empty,
                DefaultEditorContainerId = CortexWorkbenchIds.EditorContainer,
                PreviewPrimaryLabel = "Assembly Explorer",
                PreviewCenterLabel = "Decompiled Source",
                ContainerHostAssignments = new[]
                {
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.FileExplorerContainer, HostLocation = WorkbenchHostLocation.PrimarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.ReferenceContainer, HostLocation = WorkbenchHostLocation.PrimarySideHost }
                },
                HiddenContainerIds = new[] { CortexWorkbenchIds.LogsContainer, CortexWorkbenchIds.BuildContainer },
                PrimarySideWidth = 320f,
                SecondarySideWidth = 280f,
                PanelSize = 220f,
                SortOrder = 10
            });
        }

        public OnboardingTestRegistryBuilder WithTheme(ThemeContribution contribution)
        {
            _registry.RegisterTheme(contribution);
            return this;
        }

        public OnboardingTestRegistryBuilder WithProfile(OnboardingProfileContribution contribution)
        {
            _registry.RegisterOnboardingProfile(contribution);
            return this;
        }

        public OnboardingTestRegistryBuilder WithLayoutPreset(OnboardingLayoutPresetContribution contribution)
        {
            _registry.RegisterOnboardingLayoutPreset(contribution);
            return this;
        }

        public ContributionRegistry Build()
        {
            return _registry;
        }
    }
}
