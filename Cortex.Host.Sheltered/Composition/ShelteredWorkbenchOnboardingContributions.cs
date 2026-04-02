using Cortex.Core.Models;
using Cortex.Plugins.Abstractions;

namespace Cortex.Host.Sheltered.Composition
{
    internal sealed class ShelteredWorkbenchOnboardingContributions
    {
        public void Register(WorkbenchPluginContext context)
        {
            if (context == null)
            {
                return;
            }

            context.RegisterOnboardingProfile(new OnboardingProfileContribution
            {
                ProfileId = "cortex.onboarding.profile.ide",
                DisplayName = "IDE",
                Description = "Best for coding, building, navigating, and editing.",
                DefaultLayoutPresetId = "cortex.onboarding.layout.visual-studio",
                DefaultThemeId = "cortex.vs-dark",
                PreviewTags = new[] { "Code", "Build", "Navigate" },
                IsDefault = true,
                Keywords = new[] { "ide", "coding", "build", "edit" },
                SortOrder = 0
            });

            context.RegisterOnboardingProfile(new OnboardingProfileContribution
            {
                ProfileId = "cortex.onboarding.profile.modder",
                DisplayName = "Modder",
                Description = "Best for linking live mods to source projects, building DLLs, and iterating in-game.",
                DefaultLayoutPresetId = "cortex.onboarding.layout.visual-studio",
                DefaultThemeId = "cortex.vs-dark",
                PreviewTags = new[] { "Live Mods", "Source Roots", "Build DLLs" },
                WorkflowKind = OnboardingProfileWorkflowKind.Modder,
                IsDefault = false,
                Keywords = new[] { "modder", "mods", "source", "dll", "project" },
                SortOrder = 5
            });

            context.RegisterOnboardingProfile(new OnboardingProfileContribution
            {
                ProfileId = "cortex.onboarding.profile.decompiler",
                DisplayName = "Decompiler",
                Description = "Best for browsing assemblies, metadata, and generated source.",
                DefaultLayoutPresetId = "cortex.onboarding.layout.decompiler",
                DefaultThemeId = "cortex.dotpeek-dark",
                PreviewTags = new[] { "Assemblies", "Metadata", "Generated Source" },
                IsDefault = false,
                Keywords = new[] { "decompiler", "assembly", "metadata", "source" },
                SortOrder = 10
            });

            context.RegisterOnboardingLayoutPreset(new OnboardingLayoutPresetContribution
            {
                LayoutPresetId = "cortex.onboarding.layout.visual-studio",
                DisplayName = "Visual Studio",
                Description = "Editor-first with Solution Explorer docked to the right and diagnostics parked below.",
                DefaultThemeId = "cortex.vs-dark",
                DefaultFocusedContainerId = CortexWorkbenchIds.EditorContainer,
                DefaultPrimarySideContainerId = string.Empty,
                DefaultSecondarySideContainerId = CortexWorkbenchIds.FileExplorerContainer,
                DefaultPanelContainerId = CortexWorkbenchIds.LogsContainer,
                DefaultEditorContainerId = CortexWorkbenchIds.EditorContainer,
                PreviewPrimaryLabel = string.Empty,
                PreviewSecondaryLabel = "Solution Explorer",
                PreviewCenterLabel = "Editor",
                PreviewPanelLabel = "Error List / Output",
                ContainerHostAssignments = new[]
                {
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.FileExplorerContainer, HostLocation = WorkbenchHostLocation.SecondarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.ProjectsContainer, HostLocation = WorkbenchHostLocation.SecondarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.ReferenceContainer, HostLocation = WorkbenchHostLocation.SecondarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.LogsContainer, HostLocation = WorkbenchHostLocation.PanelHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.BuildContainer, HostLocation = WorkbenchHostLocation.PanelHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.SearchContainer, HostLocation = WorkbenchHostLocation.PanelHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.RuntimeContainer, HostLocation = WorkbenchHostLocation.PanelHost }
                },
                HiddenContainerIds = new string[0],
                Keywords = new[] { "visual studio", "solution", "project", "output" },
                PrimarySideWidth = 300f,
                SecondarySideWidth = 360f,
                PanelSize = 250f,
                IsDefault = true,
                SortOrder = 0
            });

            context.RegisterOnboardingLayoutPreset(new OnboardingLayoutPresetContribution
            {
                LayoutPresetId = "cortex.onboarding.layout.vs-code",
                DisplayName = "VS Code",
                Description = "Lighter chrome, a stronger sidebar identity, an editor-first center, and a compact utility strip below.",
                DefaultThemeId = "cortex.vs-dark",
                DefaultFocusedContainerId = CortexWorkbenchIds.EditorContainer,
                DefaultPrimarySideContainerId = CortexWorkbenchIds.FileExplorerContainer,
                DefaultSecondarySideContainerId = string.Empty,
                DefaultPanelContainerId = CortexWorkbenchIds.LogsContainer,
                DefaultEditorContainerId = CortexWorkbenchIds.EditorContainer,
                PreviewPrimaryLabel = "Sidebar",
                PreviewSecondaryLabel = string.Empty,
                PreviewCenterLabel = "Editor",
                PreviewPanelLabel = "Panel",
                ContainerHostAssignments = new[]
                {
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.FileExplorerContainer, HostLocation = WorkbenchHostLocation.PrimarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.ProjectsContainer, HostLocation = WorkbenchHostLocation.PrimarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.ReferenceContainer, HostLocation = WorkbenchHostLocation.PrimarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.LogsContainer, HostLocation = WorkbenchHostLocation.PanelHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.BuildContainer, HostLocation = WorkbenchHostLocation.PanelHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.SearchContainer, HostLocation = WorkbenchHostLocation.PanelHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.RuntimeContainer, HostLocation = WorkbenchHostLocation.PanelHost }
                },
                HiddenContainerIds = new string[0],
                Keywords = new[] { "vs code", "sidebar", "editor", "utility" },
                PrimarySideWidth = 280f,
                SecondarySideWidth = 280f,
                PanelSize = 220f,
                IsDefault = false,
                SortOrder = 10
            });

            context.RegisterOnboardingLayoutPreset(new OnboardingLayoutPresetContribution
            {
                LayoutPresetId = "cortex.onboarding.layout.decompiler",
                DisplayName = "Decompiler",
                Description = "dotPeek-style browsing with Assembly Explorer on the left and decompiled source centered.",
                DefaultThemeId = "cortex.dotpeek-dark",
                DefaultFocusedContainerId = CortexWorkbenchIds.EditorContainer,
                DefaultPrimarySideContainerId = CortexWorkbenchIds.FileExplorerContainer,
                DefaultSecondarySideContainerId = string.Empty,
                DefaultPanelContainerId = string.Empty,
                DefaultEditorContainerId = CortexWorkbenchIds.EditorContainer,
                PreviewPrimaryLabel = "Assembly Explorer",
                PreviewSecondaryLabel = string.Empty,
                PreviewCenterLabel = "Decompiled Source",
                PreviewPanelLabel = string.Empty,
                ContainerHostAssignments = new[]
                {
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.FileExplorerContainer, HostLocation = WorkbenchHostLocation.PrimarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.ReferenceContainer, HostLocation = WorkbenchHostLocation.PrimarySideHost },
                    new OnboardingContainerHostAssignment { ContainerId = CortexWorkbenchIds.SearchContainer, HostLocation = WorkbenchHostLocation.PrimarySideHost }
                },
                HiddenContainerIds = new[]
                {
                    CortexWorkbenchIds.ProjectsContainer,
                    CortexWorkbenchIds.RuntimeContainer,
                    CortexWorkbenchIds.LogsContainer,
                    CortexWorkbenchIds.BuildContainer
                },
                Keywords = new[] { "decompiler", "tree", "metadata", "search" },
                PrimarySideWidth = 320f,
                SecondarySideWidth = 280f,
                PanelSize = 220f,
                IsDefault = false,
                SortOrder = 20
            });
        }
    }
}
