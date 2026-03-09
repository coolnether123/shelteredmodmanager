using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Host.Unity.Composition
{
    internal static class DefaultWorkbenchComposition
    {
        public static void RegisterBuiltIns(
            ICommandRegistry commandRegistry,
            IContributionRegistry contributionRegistry,
            string rendererDisplayName)
        {
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.LogsContainer, "Logs", WorkbenchHostLocation.PanelHost, 0, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.LogsContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.ProjectsContainer, "Projects", WorkbenchHostLocation.PrimarySideHost, 10, ModuleActivationKind.OnWorkspaceAvailable, "workspace");
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.EditorContainer, "Editor", WorkbenchHostLocation.DocumentHost, 20, ModuleActivationKind.OnDocumentRestore, CortexWorkbenchIds.EditorContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.BuildContainer, "Build", WorkbenchHostLocation.PanelHost, 30, ModuleActivationKind.OnCommand, "cortex.build.execute");
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.ReferenceContainer, "Reference", WorkbenchHostLocation.PrimarySideHost, 40, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.ReferenceContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.RuntimeContainer, "Runtime", WorkbenchHostLocation.PrimarySideHost, 50, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.RuntimeContainer);
            RegisterContainer(contributionRegistry, CortexWorkbenchIds.SettingsContainer, "Settings", WorkbenchHostLocation.PrimarySideHost, 60, ModuleActivationKind.OnContainerOpen, CortexWorkbenchIds.SettingsContainer);

            RegisterCommand(commandRegistry, "cortex.shell.toggle", "Toggle Cortex", "Workbench", "Show or hide the Cortex shell.", "F8", 0, true);
            RegisterCommand(commandRegistry, "cortex.logs.toggleWindow", "Toggle Detached Logs", "Logs", "Show or hide the detached log window.", string.Empty, 10, false);
            RegisterCommand(commandRegistry, "cortex.shell.fitWindow", "Fit Workbench To Screen", "Workbench", "Resize the shell to fill most of the game view.", string.Empty, 20, false);
            RegisterCommand(commandRegistry, "cortex.build.execute", "Open Build Panel", "Build", "Focus the build panel and activate build tooling.", string.Empty, 30, false);

            contributionRegistry.RegisterStatusItem(new StatusItemContribution
            {
                ItemId = "cortex.status.renderer",
                Text = rendererDisplayName,
                ToolTip = "Active Cortex renderer backend.",
                CommandId = string.Empty,
                Severity = "Info",
                Alignment = StatusItemAlignment.Right,
                Priority = 100
            });
        }

        private static void RegisterContainer(
            IContributionRegistry contributionRegistry,
            string containerId,
            string title,
            WorkbenchHostLocation hostLocation,
            int sortOrder,
            ModuleActivationKind activationKind,
            string activationTarget)
        {
            contributionRegistry.RegisterViewContainer(new ViewContainerContribution
            {
                ContainerId = containerId,
                Title = title,
                IconId = containerId,
                DefaultHostLocation = hostLocation,
                SortOrder = sortOrder,
                PinnedByDefault = true,
                ActivationKind = activationKind,
                ActivationTarget = activationTarget
            });

            contributionRegistry.RegisterView(new ViewContribution
            {
                ViewId = containerId + ".main",
                ContainerId = containerId,
                Title = title,
                PersistenceId = containerId + ".main",
                SortOrder = 0,
                VisibleByDefault = true
            });
        }

        private static void RegisterCommand(
            ICommandRegistry commandRegistry,
            string commandId,
            string displayName,
            string category,
            string description,
            string gesture,
            int sortOrder,
            bool isGlobal)
        {
            commandRegistry.Register(new CommandDefinition
            {
                CommandId = commandId,
                DisplayName = displayName,
                Category = category,
                Description = description,
                DefaultGesture = gesture,
                SortOrder = sortOrder,
                ShowInPalette = true,
                IsGlobal = isGlobal
            });
        }
    }
}
