using Dock.Model.Core;
using Dock.Model.Controls;
using Dock.Model.Mvvm;
using Cortex.Host.Avalonia.Models;
using Cortex.Host.Avalonia.ViewModels;
using Cortex.Host.Avalonia.Views;
using System.Collections.Generic;
using System.Linq;

namespace Cortex.Host.Avalonia.Services
{
    internal sealed class DesktopWorkbenchDockFactory : Factory
    {
        private readonly DesktopShellViewModel _viewModel;
        private readonly IReadOnlyList<DesktopWorkbenchSurfaceDefinition> _surfaces;
        private readonly DesktopDockLayoutState _layoutState;

        public DesktopWorkbenchDockFactory(
            DesktopShellViewModel viewModel,
            IReadOnlyList<DesktopWorkbenchSurfaceDefinition> surfaces,
            DesktopDockLayoutState layoutState)
        {
            _viewModel = viewModel;
            _surfaces = surfaces ?? new List<DesktopWorkbenchSurfaceDefinition>();
            _layoutState = layoutState ?? new DesktopDockLayoutState();
        }

        public override IRootDock CreateLayout()
        {
            var leftDock = CreateDockGroup(DesktopWorkbenchSurfaceRegistry.LeftGroupId);
            var centerDocumentDock = CreateDockGroup(DesktopWorkbenchSurfaceRegistry.CenterGroupId) as IDocumentDock;
            var bottomDock = CreateDockGroup(DesktopWorkbenchSurfaceRegistry.BottomGroupId);
            var rightDock = CreateDockGroup(DesktopWorkbenchSurfaceRegistry.RightGroupId);

            var centerContent = CreateCenterContent(centerDocumentDock, bottomDock);
            var rootChildren = new List<IDockable>();
            if (leftDock != null)
            {
                rootChildren.Add(leftDock);
            }

            if (centerContent != null)
            {
                rootChildren.Add(centerContent);
            }

            if (rightDock != null)
            {
                rootChildren.Add(rightDock);
            }

            var workbenchDock = CreateProportionalDock();
            workbenchDock.Id = "cortex.dock.workbench";
            workbenchDock.Title = "Workbench";
            workbenchDock.Orientation = Orientation.Horizontal;
            workbenchDock.VisibleDockables = CreateList(rootChildren.ToArray());
            workbenchDock.ActiveDockable = centerContent ?? rootChildren.FirstOrDefault();
            workbenchDock.DefaultDockable = centerContent ?? rootChildren.FirstOrDefault();

            var root = CreateRootDock();
            root.Id = "cortex.root";
            root.Title = "Cortex Desktop Workbench";
            root.VisibleDockables = CreateList<IDockable>(workbenchDock);
            root.ActiveDockable = workbenchDock;
            root.DefaultDockable = workbenchDock;
            return root;
        }

        private IDock CreateDockGroup(string groupId)
        {
            var groupState = _layoutState.Groups.FirstOrDefault(group => string.Equals(group.GroupId, groupId, System.StringComparison.OrdinalIgnoreCase));
            if (groupState == null || groupState.SurfaceIds == null || groupState.SurfaceIds.Count == 0)
            {
                return null;
            }

            var surfaceDefinitions = groupState.SurfaceIds
                .Select(surfaceId => _surfaces.FirstOrDefault(surface => string.Equals(surface.SurfaceId, surfaceId, System.StringComparison.OrdinalIgnoreCase)))
                .Where(surface => surface != null)
                .ToList();
            if (surfaceDefinitions.Count == 0)
            {
                return null;
            }

            var dockables = surfaceDefinitions
                .Select(CreateSurfaceDockable)
                .Where(dockable => dockable != null)
                .ToList();
            if (dockables.Count == 0)
            {
                return null;
            }

            IDock dock;
            if (string.Equals(groupId, DesktopWorkbenchSurfaceRegistry.CenterGroupId, System.StringComparison.OrdinalIgnoreCase))
            {
                var documentDock = CreateDocumentDock();
                documentDock.Id = "cortex.dock.center.documents";
                documentDock.Title = "Workbench Documents";
                documentDock.CanCreateDocument = false;
                documentDock.CanCloseLastDockable = false;
                dock = documentDock;
            }
            else
            {
                var toolDock = CreateToolDock();
                toolDock.Id = ResolveDockId(groupId);
                toolDock.Title = ResolveDockTitle(groupId);
                toolDock.Alignment = ResolveAlignment(groupId);
                dock = toolDock;
            }

            dock.Proportion = groupState.Proportion > 0 ? groupState.Proportion : 1.0;
            dock.VisibleDockables = CreateList(dockables.ToArray());
            dock.ActiveDockable = dockables.FirstOrDefault(item => string.Equals(item.Id, groupState.ActiveSurfaceId, System.StringComparison.OrdinalIgnoreCase))
                ?? dockables[0];
            dock.DefaultDockable = dock.ActiveDockable;
            return dock;
        }

        private IDockable CreateCenterContent(IDocumentDock centerDocumentDock, IDock bottomDock)
        {
            if (centerDocumentDock == null)
            {
                return bottomDock;
            }

            if (bottomDock == null)
            {
                return centerDocumentDock;
            }

            var centerSplit = CreateProportionalDock();
            centerSplit.Id = "cortex.dock.center.split";
            centerSplit.Title = "Center";
            centerSplit.Orientation = Orientation.Vertical;
            centerSplit.VisibleDockables = CreateList<IDockable>(centerDocumentDock, bottomDock);
            centerSplit.ActiveDockable = centerDocumentDock;
            centerSplit.DefaultDockable = centerDocumentDock;
            return centerSplit;
        }

        private IDockable CreateSurfaceDockable(DesktopWorkbenchSurfaceDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            var dockable = definition.IsDocument ? (IDockable)CreateDocument() : CreateTool();
            dockable.Id = definition.SurfaceId;
            dockable.Title = definition.Title;
            dockable.CanClose = false;
            dockable.CanFloat = false;
            dockable.CanPin = false;
            dockable.Context = CreateSurfaceContext(definition.SurfaceId);
            return dockable;
        }

        private object CreateSurfaceContext(string surfaceId)
        {
            switch (surfaceId)
            {
                case DesktopWorkbenchSurfaceRegistry.OnboardingSurfaceId:
                    return new OnboardingToolView { DataContext = _viewModel.Workbench };
                case DesktopWorkbenchSurfaceRegistry.WorkspaceSurfaceId:
                    return new WorkspaceDocumentView { DataContext = _viewModel.Workbench };
                case DesktopWorkbenchSurfaceRegistry.EditorSurfaceId:
                    return new EditorDocumentView { DataContext = _viewModel.Workbench };
                case DesktopWorkbenchSurfaceRegistry.SettingsSurfaceId:
                    return new SettingsToolView { DataContext = _viewModel.Workbench };
                case DesktopWorkbenchSurfaceRegistry.ReferenceSurfaceId:
                    return new ReferenceToolView { DataContext = _viewModel.Workbench };
                case DesktopWorkbenchSurfaceRegistry.SearchSurfaceId:
                    return new SearchToolView { DataContext = _viewModel.Workbench };
                case DesktopWorkbenchSurfaceRegistry.StatusSurfaceId:
                    return new StatusToolView { DataContext = _viewModel };
                default:
                    return null;
            }
        }

        private static string ResolveDockId(string groupId)
        {
            switch (groupId)
            {
                case DesktopWorkbenchSurfaceRegistry.LeftGroupId:
                    return "cortex.dock.left.tools";
                case DesktopWorkbenchSurfaceRegistry.RightGroupId:
                    return "cortex.dock.right.tools";
                case DesktopWorkbenchSurfaceRegistry.BottomGroupId:
                    return "cortex.dock.bottom.tools";
                default:
                    return "cortex.dock.group";
            }
        }

        private static string ResolveDockTitle(string groupId)
        {
            switch (groupId)
            {
                case DesktopWorkbenchSurfaceRegistry.LeftGroupId:
                    return "Left Tools";
                case DesktopWorkbenchSurfaceRegistry.RightGroupId:
                    return "Right Tools";
                case DesktopWorkbenchSurfaceRegistry.BottomGroupId:
                    return "Bottom Tools";
                default:
                    return "Tools";
            }
        }

        private static Alignment ResolveAlignment(string groupId)
        {
            switch (groupId)
            {
                case DesktopWorkbenchSurfaceRegistry.LeftGroupId:
                    return Alignment.Left;
                case DesktopWorkbenchSurfaceRegistry.RightGroupId:
                    return Alignment.Right;
                case DesktopWorkbenchSurfaceRegistry.BottomGroupId:
                    return Alignment.Bottom;
                default:
                    return Alignment.Unset;
            }
        }
    }
}
