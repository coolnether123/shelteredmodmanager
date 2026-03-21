using System;
using System.Collections.Generic;
using Cortex.Core.Models;
using Cortex.Presentation.Models;

namespace Cortex
{
    internal sealed class CortexShellLayoutHostRouter
    {
        public void ActivateContainer(CortexShellLayoutContext context, string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return;
            }

            var state = context.State;
            var runtime = context.WorkbenchRuntime;
            var hostLocation = ResolveHostLocationCore(context, containerId);

            state.Workbench.HiddenContainerIds.Remove(containerId);
            state.Workbench.FocusedContainerId = containerId;
            if (hostLocation == WorkbenchHostLocation.PanelHost)
            {
                state.Workbench.PanelContainerId = containerId;
            }
            else if (hostLocation == WorkbenchHostLocation.SecondarySideHost)
            {
                state.Workbench.SecondarySideContainerId = containerId;
            }
            else if (hostLocation == WorkbenchHostLocation.DocumentHost)
            {
                state.Workbench.EditorContainerId = containerId;
            }
            else
            {
                state.Workbench.SideContainerId = containerId;
            }

            if (runtime != null)
            {
                if (hostLocation == WorkbenchHostLocation.PanelHost)
                {
                    runtime.WorkbenchState.ActivePanelId = containerId;
                }
                else if (hostLocation == WorkbenchHostLocation.DocumentHost)
                {
                    runtime.WorkbenchState.ActiveEditorGroupId = containerId;
                    runtime.WorkbenchState.ActiveContainerId = containerId;
                }
                else if (hostLocation == WorkbenchHostLocation.SecondarySideHost)
                {
                    runtime.WorkbenchState.ActiveContainerId = containerId;
                    runtime.WorkbenchState.SecondarySideHostVisible = true;
                }
                else
                {
                    runtime.WorkbenchState.ActiveContainerId = containerId;
                }

                runtime.WorkbenchState.PrimarySideHostVisible = !string.IsNullOrEmpty(state.Workbench.SideContainerId);
                runtime.WorkbenchState.PanelHostVisible = !string.IsNullOrEmpty(state.Workbench.PanelContainerId);
                runtime.WorkbenchState.SecondarySideHostVisible = !string.IsNullOrEmpty(state.Workbench.SecondarySideContainerId);
                runtime.FocusState.FocusedRegionId = containerId;
            }
        }

        public WorkbenchHostLocation ResolveHostLocation(CortexShellLayoutContext context, string containerId)
        {
            return ResolveHostLocationCore(context, containerId);
        }

        public void DockContainer(CortexShellLayoutContext context, string containerId, WorkbenchHostLocation hostLocation)
        {
            if (string.IsNullOrEmpty(containerId) || hostLocation == WorkbenchHostLocation.ToolRail)
            {
                return;
            }

            var state = context.State;
            state.Workbench.HiddenContainerIds.Remove(containerId);
            state.Workbench.AssignHost(containerId, hostLocation);
            if (hostLocation == WorkbenchHostLocation.DocumentHost)
            {
                state.Workbench.EditorContainerId = containerId;
            }
            else if (hostLocation == WorkbenchHostLocation.PanelHost)
            {
                state.Workbench.PanelContainerId = containerId;
            }
            else if (hostLocation == WorkbenchHostLocation.SecondarySideHost)
            {
                state.Workbench.SecondarySideContainerId = containerId;
            }
            else
            {
                state.Workbench.SideContainerId = containerId;
            }

            if (string.Equals(state.Workbench.SecondarySideContainerId, containerId, StringComparison.OrdinalIgnoreCase) &&
                hostLocation != WorkbenchHostLocation.SecondarySideHost)
            {
                state.Workbench.SecondarySideContainerId = FindFirstContainerForHost(context, WorkbenchHostLocation.SecondarySideHost, containerId);
            }

            if (string.Equals(state.Workbench.EditorContainerId, containerId, StringComparison.OrdinalIgnoreCase) &&
                hostLocation != WorkbenchHostLocation.DocumentHost)
            {
                state.Workbench.EditorContainerId = FindFirstContainerForHost(context, WorkbenchHostLocation.DocumentHost, containerId);
            }

            if (string.Equals(state.Workbench.PanelContainerId, containerId, StringComparison.OrdinalIgnoreCase) &&
                hostLocation != WorkbenchHostLocation.PanelHost)
            {
                state.Workbench.PanelContainerId = FindFirstContainerForHost(context, WorkbenchHostLocation.PanelHost, containerId);
            }

            if (string.Equals(state.Workbench.SideContainerId, containerId, StringComparison.OrdinalIgnoreCase) &&
                hostLocation != WorkbenchHostLocation.PrimarySideHost)
            {
                state.Workbench.SideContainerId = FindFirstContainerForHost(context, WorkbenchHostLocation.PrimarySideHost, containerId);
            }

            ActivateContainer(context, containerId);
        }

        public void HideContainer(CortexShellLayoutContext context, string containerId)
        {
            if (string.IsNullOrEmpty(containerId) || string.Equals(containerId, CortexWorkbenchIds.EditorContainer, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var state = context.State;
            var runtime = context.WorkbenchRuntime;
            state.Workbench.HiddenContainerIds.Add(containerId);
            if (string.Equals(state.Workbench.PanelContainerId, containerId, StringComparison.OrdinalIgnoreCase))
            {
                state.Workbench.PanelContainerId = FindFirstContainerForHost(context, WorkbenchHostLocation.PanelHost, containerId);
            }

            if (string.Equals(state.Workbench.SecondarySideContainerId, containerId, StringComparison.OrdinalIgnoreCase))
            {
                state.Workbench.SecondarySideContainerId = FindFirstContainerForHost(context, WorkbenchHostLocation.SecondarySideHost, containerId);
            }

            if (string.Equals(state.Workbench.SideContainerId, containerId, StringComparison.OrdinalIgnoreCase))
            {
                state.Workbench.SideContainerId = FindFirstContainerForHost(context, WorkbenchHostLocation.PrimarySideHost, containerId);
            }

            if (string.Equals(state.Workbench.FocusedContainerId, containerId, StringComparison.OrdinalIgnoreCase))
            {
                state.Workbench.FocusedContainerId = !string.IsNullOrEmpty(state.Workbench.EditorContainerId)
                    ? state.Workbench.EditorContainerId
                    : string.Empty;
            }

            if (runtime != null)
            {
                runtime.WorkbenchState.PrimarySideHostVisible = !string.IsNullOrEmpty(state.Workbench.SideContainerId);
                runtime.WorkbenchState.PanelHostVisible = !string.IsNullOrEmpty(state.Workbench.PanelContainerId);
                runtime.WorkbenchState.SecondarySideHostVisible = !string.IsNullOrEmpty(state.Workbench.SecondarySideContainerId);
            }
        }

        public bool HasHostItems(CortexShellLayoutContext context, WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            return GetHostItems(context, snapshot, hostLocation).Count > 0;
        }

        public List<ToolRailItem> GetHostItems(CortexShellLayoutContext context, WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            var items = new List<ToolRailItem>();
            if (snapshot == null)
            {
                return items;
            }

            for (var i = 0; i < snapshot.ToolRailItems.Count; i++)
            {
                var item = snapshot.ToolRailItems[i];
                if (item != null &&
                    !context.State.Workbench.IsHidden(item.ContainerId) &&
                    ResolveHostLocationCore(context, item.ContainerId) == hostLocation)
                {
                    items.Add(item);
                }
            }

            return items;
        }

        public string GetActiveContainerForHost(CortexShellLayoutContext context, WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            switch (hostLocation)
            {
                case WorkbenchHostLocation.PanelHost:
                    return !string.IsNullOrEmpty(context.State.Workbench.PanelContainerId)
                        ? context.State.Workbench.PanelContainerId
                        : FindFirstHostItem(context, snapshot, hostLocation);
                case WorkbenchHostLocation.SecondarySideHost:
                    return !string.IsNullOrEmpty(context.State.Workbench.SecondarySideContainerId)
                        ? context.State.Workbench.SecondarySideContainerId
                        : FindFirstHostItem(context, snapshot, hostLocation);
                case WorkbenchHostLocation.DocumentHost:
                    return context.State.Workbench.EditorContainerId;
                case WorkbenchHostLocation.PrimarySideHost:
                default:
                    return !string.IsNullOrEmpty(context.State.Workbench.SideContainerId)
                        ? context.State.Workbench.SideContainerId
                        : FindFirstHostItem(context, snapshot, hostLocation);
            }
        }

        public string FindFirstContainerForHost(CortexShellLayoutContext context, WorkbenchHostLocation hostLocation, string excludedContainerId)
        {
            var runtime = context.WorkbenchRuntime;
            if (runtime == null || runtime.ContributionRegistry == null)
            {
                return string.Empty;
            }

            var containers = runtime.ContributionRegistry.GetViewContainers();
            for (var i = 0; i < containers.Count; i++)
            {
                var containerId = containers[i].ContainerId;
                if (string.Equals(containerId, excludedContainerId, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (context.State.Workbench.IsHidden(containerId))
                {
                    continue;
                }

                if (ResolveHostLocationCore(context, containerId) == hostLocation)
                {
                    return containerId;
                }
            }

            return string.Empty;
        }

        public string MapLegacyTabIndex(int index)
        {
            switch (index)
            {
                case 0: return CortexWorkbenchIds.LogsContainer;
                case 1: return CortexWorkbenchIds.ProjectsContainer;
                case 2: return CortexWorkbenchIds.EditorContainer;
                case 3: return CortexWorkbenchIds.BuildContainer;
                case 4: return CortexWorkbenchIds.ReferenceContainer;
                case 5: return CortexWorkbenchIds.RuntimeContainer;
                case 6: return CortexWorkbenchIds.ProjectsContainer;
                default: return CortexWorkbenchIds.LogsContainer;
            }
        }

        private string FindFirstHostItem(CortexShellLayoutContext context, WorkbenchPresentationSnapshot snapshot, WorkbenchHostLocation hostLocation)
        {
            var items = GetHostItems(context, snapshot, hostLocation);
            return items.Count > 0 && items[0] != null ? items[0].ContainerId : string.Empty;
        }

        private WorkbenchHostLocation ResolveHostLocationCore(CortexShellLayoutContext context, string containerId)
        {
            var defaultHost = WorkbenchHostLocation.PrimarySideHost;
            var runtime = context.WorkbenchRuntime;
            if (runtime != null)
            {
                var containers = runtime.ContributionRegistry.GetViewContainers();
                for (var i = 0; i < containers.Count; i++)
                {
                    if (string.Equals(containers[i].ContainerId, containerId, StringComparison.OrdinalIgnoreCase))
                    {
                        defaultHost = containers[i].DefaultHostLocation;
                        break;
                    }
                }
            }
            else if (string.Equals(containerId, CortexWorkbenchIds.LogsContainer, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(containerId, CortexWorkbenchIds.BuildContainer, StringComparison.OrdinalIgnoreCase))
            {
                defaultHost = WorkbenchHostLocation.PanelHost;
            }
            else if (string.Equals(containerId, CortexWorkbenchIds.EditorContainer, StringComparison.OrdinalIgnoreCase))
            {
                defaultHost = WorkbenchHostLocation.DocumentHost;
            }

            return context.State.Workbench.GetAssignedHost(containerId, defaultHost);
        }
    }
}
