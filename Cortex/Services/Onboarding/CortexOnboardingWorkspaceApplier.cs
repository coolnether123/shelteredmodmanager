using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;

namespace Cortex.Services.Onboarding
{
    internal sealed class CortexOnboardingWorkspaceApplicationResult
    {
        public static readonly CortexOnboardingWorkspaceApplicationResult Empty = new CortexOnboardingWorkspaceApplicationResult(false, new string[0]);

        public readonly bool WasApplied;
        public readonly string[] ContainersToActivate;

        public CortexOnboardingWorkspaceApplicationResult(bool wasApplied, string[] containersToActivate)
        {
            WasApplied = wasApplied;
            ContainersToActivate = containersToActivate ?? new string[0];
        }
    }

    internal sealed class CortexOnboardingWorkspaceApplier
    {
        public CortexOnboardingWorkspaceApplicationResult Preview(
            CortexShellState shellState,
            IWorkbenchRuntime workbenchRuntime,
            CortexOnboardingResolvedSelection selection)
        {
            return ApplyResolvedSelection(shellState, workbenchRuntime, selection, false);
        }

        public CortexOnboardingWorkspaceApplicationResult Apply(
            CortexShellState shellState,
            IWorkbenchRuntime workbenchRuntime,
            CortexOnboardingResolvedSelection selection)
        {
            return ApplyResolvedSelection(shellState, workbenchRuntime, selection, true);
        }

        private static CortexOnboardingWorkspaceApplicationResult ApplyResolvedSelection(
            CortexShellState shellState,
            IWorkbenchRuntime workbenchRuntime,
            CortexOnboardingResolvedSelection selection,
            bool commitSelection)
        {
            if (shellState == null || shellState.Workbench == null || workbenchRuntime == null || selection == null)
            {
                return CortexOnboardingWorkspaceApplicationResult.Empty;
            }

            var layoutPreset = selection.LayoutPreset;
            var theme = selection.Theme;
            if (layoutPreset == null || theme == null)
            {
                return CortexOnboardingWorkspaceApplicationResult.Empty;
            }

            shellState.Workbench.HostOverrides.Clear();
            shellState.Workbench.HiddenContainerIds.Clear();
            ApplyHostAssignments(shellState, layoutPreset);
            ApplyHiddenContainers(shellState, layoutPreset);

            shellState.Workbench.SideContainerId = NormalizeVisibleContainer(layoutPreset.DefaultPrimarySideContainerId, shellState.Workbench.HiddenContainerIds);
            shellState.Workbench.SecondarySideContainerId = NormalizeVisibleContainer(layoutPreset.DefaultSecondarySideContainerId, shellState.Workbench.HiddenContainerIds);
            shellState.Workbench.PanelContainerId = NormalizeVisibleContainer(layoutPreset.DefaultPanelContainerId, shellState.Workbench.HiddenContainerIds);
            shellState.Workbench.EditorContainerId = string.IsNullOrEmpty(layoutPreset.DefaultEditorContainerId)
                ? CortexWorkbenchIds.EditorContainer
                : layoutPreset.DefaultEditorContainerId;

            var focusedContainerId = NormalizeVisibleContainer(layoutPreset.DefaultFocusedContainerId, shellState.Workbench.HiddenContainerIds);
            shellState.Workbench.FocusedContainerId = string.IsNullOrEmpty(focusedContainerId)
                ? shellState.Workbench.EditorContainerId
                : focusedContainerId;

            shellState.Logs.ShowDetachedWindow = false;

            workbenchRuntime.LayoutState.HostDimensions.Clear();
            workbenchRuntime.LayoutState.PrimarySideWidth = layoutPreset.PrimarySideWidth > 0f ? layoutPreset.PrimarySideWidth : 360f;
            workbenchRuntime.LayoutState.SecondarySideWidth = layoutPreset.SecondarySideWidth > 0f ? layoutPreset.SecondarySideWidth : 320f;
            workbenchRuntime.LayoutState.PanelSize = layoutPreset.PanelSize > 0f ? layoutPreset.PanelSize : 280f;
            workbenchRuntime.ThemeState.ThemeId = theme.ThemeId;
            workbenchRuntime.WorkbenchState.ActiveEditorGroupId = shellState.Workbench.EditorContainerId;
            workbenchRuntime.WorkbenchState.ActivePanelId = shellState.Workbench.PanelContainerId;
            workbenchRuntime.WorkbenchState.ActiveContainerId = shellState.Workbench.FocusedContainerId;
            workbenchRuntime.WorkbenchState.PrimarySideHostVisible = !string.IsNullOrEmpty(shellState.Workbench.SideContainerId);
            workbenchRuntime.WorkbenchState.SecondarySideHostVisible = !string.IsNullOrEmpty(shellState.Workbench.SecondarySideContainerId);
            workbenchRuntime.WorkbenchState.PanelHostVisible = !string.IsNullOrEmpty(shellState.Workbench.PanelContainerId);
            workbenchRuntime.FocusState.FocusedRegionId = shellState.Workbench.FocusedContainerId;

            if (commitSelection)
            {
                ApplyCommittedSelection(shellState, workbenchRuntime, selection);
            }

            return new CortexOnboardingWorkspaceApplicationResult(true, BuildContainersToActivate(shellState));
        }

        private static void ApplyCommittedSelection(
            CortexShellState shellState,
            IWorkbenchRuntime workbenchRuntime,
            CortexOnboardingResolvedSelection selection)
        {
            var onboardingState = shellState.Onboarding;
            if (onboardingState != null)
            {
                onboardingState.ActiveProfileId = selection.Profile != null ? selection.Profile.ProfileId : string.Empty;
                onboardingState.ActiveLayoutPresetId = selection.LayoutPreset != null ? selection.LayoutPreset.LayoutPresetId : string.Empty;
                onboardingState.ActiveThemeId = selection.Theme != null ? selection.Theme.ThemeId : string.Empty;
                onboardingState.SelectedProfileId = onboardingState.ActiveProfileId;
                onboardingState.SelectedLayoutPresetId = onboardingState.ActiveLayoutPresetId;
                onboardingState.SelectedThemeId = onboardingState.ActiveThemeId;
            }

            if (shellState.Settings != null)
            {
                shellState.Settings.DefaultOnboardingProfileId = selection.Profile != null ? selection.Profile.ProfileId : string.Empty;
                shellState.Settings.DefaultOnboardingLayoutPresetId = selection.LayoutPreset != null ? selection.LayoutPreset.LayoutPresetId : string.Empty;
                shellState.Settings.DefaultOnboardingThemeId = selection.Theme != null ? selection.Theme.ThemeId : string.Empty;
                shellState.Settings.ThemeId = selection.Theme != null ? selection.Theme.ThemeId : string.Empty;
                shellState.Settings.ProjectsPaneWidth = workbenchRuntime.LayoutState.PrimarySideWidth;
                shellState.Settings.EditorFilePaneWidth = workbenchRuntime.LayoutState.SecondarySideWidth;
                shellState.Settings.PanelPaneSize = workbenchRuntime.LayoutState.PanelSize;
                shellState.Settings.HasCompletedOnboarding = true;
            }
        }

        private static void ApplyHostAssignments(CortexShellState shellState, OnboardingLayoutPresetContribution layoutPreset)
        {
            var assignments = layoutPreset.ContainerHostAssignments ?? new OnboardingContainerHostAssignment[0];
            for (var i = 0; i < assignments.Length; i++)
            {
                var assignment = assignments[i];
                if (assignment == null || string.IsNullOrEmpty(assignment.ContainerId))
                {
                    continue;
                }

                shellState.Workbench.AssignHost(assignment.ContainerId, assignment.HostLocation);
            }
        }

        private static void ApplyHiddenContainers(CortexShellState shellState, OnboardingLayoutPresetContribution layoutPreset)
        {
            var hiddenContainerIds = layoutPreset.HiddenContainerIds ?? new string[0];
            for (var i = 0; i < hiddenContainerIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(hiddenContainerIds[i]))
                {
                    shellState.Workbench.HiddenContainerIds.Add(hiddenContainerIds[i]);
                }
            }
        }

        private static string NormalizeVisibleContainer(string containerId, HashSet<string> hiddenContainerIds)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return string.Empty;
            }

            return hiddenContainerIds != null && hiddenContainerIds.Contains(containerId)
                ? string.Empty
                : containerId;
        }

        private static string[] BuildContainersToActivate(CortexShellState shellState)
        {
            var containers = new List<string>();
            AddContainer(containers, shellState.Workbench.SideContainerId);
            AddContainer(containers, shellState.Workbench.SecondarySideContainerId);
            AddContainer(containers, shellState.Workbench.EditorContainerId);
            AddContainer(containers, shellState.Workbench.PanelContainerId);
            AddContainer(containers, shellState.Workbench.FocusedContainerId);
            return containers.ToArray();
        }

        private static void AddContainer(List<string> containers, string containerId)
        {
            if (containers == null || string.IsNullOrEmpty(containerId) || containers.Contains(containerId))
            {
                return;
            }

            containers.Add(containerId);
        }
    }
}
