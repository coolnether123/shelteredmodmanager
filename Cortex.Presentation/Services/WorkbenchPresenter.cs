using System.Collections.Generic;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Presentation.Models;

namespace Cortex.Presentation.Services
{
    public sealed class WorkbenchPresenter : IWorkbenchPresenter
    {
        public WorkbenchPresentationSnapshot BuildSnapshot(
            WorkbenchState workbenchState,
            LayoutState layoutState,
            StatusState statusState,
            ThemeState themeState,
            FocusState focusState,
            IContributionRegistry contributionRegistry)
        {
            var snapshot = new WorkbenchPresentationSnapshot();
            if (workbenchState != null)
            {
                snapshot.ActiveContainerId = workbenchState.ActiveContainerId ?? string.Empty;
            }

            if (focusState != null)
            {
                snapshot.FocusedRegionId = focusState.FocusedRegionId ?? string.Empty;
            }

            PopulateToolRail(snapshot, workbenchState, contributionRegistry);
            PopulateStatus(snapshot, contributionRegistry);
            return snapshot;
        }

        private static void PopulateToolRail(
            WorkbenchPresentationSnapshot snapshot,
            WorkbenchState workbenchState,
            IContributionRegistry contributionRegistry)
        {
            if (snapshot == null || contributionRegistry == null)
            {
                return;
            }

            IList<ViewContainerContribution> containers = contributionRegistry.GetViewContainers();
            for (var i = 0; i < containers.Count; i++)
            {
                var container = containers[i];
                snapshot.ToolRailItems.Add(new ToolRailItem
                {
                    ContainerId = container.ContainerId,
                    Title = container.Title,
                    IconId = container.IconId,
                    HostLocation = container.DefaultHostLocation,
                    Active = workbenchState != null &&
                        string.Equals(workbenchState.ActiveContainerId, container.ContainerId)
                });
            }
        }

        private static void PopulateStatus(WorkbenchPresentationSnapshot snapshot, IContributionRegistry contributionRegistry)
        {
            if (snapshot == null || contributionRegistry == null)
            {
                return;
            }

            IList<StatusItemContribution> statusItems = contributionRegistry.GetStatusItems();
            for (var i = 0; i < statusItems.Count; i++)
            {
                if (statusItems[i].Alignment == StatusItemAlignment.Left)
                {
                    snapshot.LeftStatusItems.Add(statusItems[i]);
                }
                else
                {
                    snapshot.RightStatusItems.Add(statusItems[i]);
                }
            }
        }
    }
}
