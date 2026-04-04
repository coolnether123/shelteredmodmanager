using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Dock.Model.Core;
using Dock.Model.Controls;
using Cortex.Host.Avalonia.Models;

namespace Cortex.Host.Avalonia.Services
{
    internal sealed class DesktopDockLayoutPersistenceService
    {
        private const string FocusLayoutId = "cortex.onboarding.layout.focus";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        private readonly string _layoutFilePath;

        public DesktopDockLayoutPersistenceService(string layoutFilePath)
        {
            _layoutFilePath = layoutFilePath ?? string.Empty;
        }

        public DesktopDockLayoutState Load()
        {
            if (string.IsNullOrEmpty(_layoutFilePath) || !File.Exists(_layoutFilePath))
            {
                return null;
            }

            try
            {
                return JsonSerializer.Deserialize<DesktopDockLayoutState>(File.ReadAllText(_layoutFilePath), JsonOptions);
            }
            catch
            {
                return null;
            }
        }

        public void Save(IRootDock rootDock)
        {
            if (rootDock == null || string.IsNullOrEmpty(_layoutFilePath))
            {
                return;
            }

            var layoutState = new DesktopDockLayoutState();
            layoutState.Groups.Add(CaptureGroup(rootDock, DesktopWorkbenchSurfaceRegistry.LeftGroupId, "cortex.dock.left.tools"));
            layoutState.Groups.Add(CaptureGroup(rootDock, DesktopWorkbenchSurfaceRegistry.CenterGroupId, "cortex.dock.center.documents"));
            layoutState.Groups.Add(CaptureGroup(rootDock, DesktopWorkbenchSurfaceRegistry.RightGroupId, "cortex.dock.right.tools"));
            layoutState.Groups.Add(CaptureGroup(rootDock, DesktopWorkbenchSurfaceRegistry.BottomGroupId, "cortex.dock.bottom.tools"));

            Directory.CreateDirectory(Path.GetDirectoryName(_layoutFilePath) ?? string.Empty);
            File.WriteAllText(_layoutFilePath, JsonSerializer.Serialize(layoutState, JsonOptions));
        }

        public void Delete()
        {
            if (!string.IsNullOrEmpty(_layoutFilePath) && File.Exists(_layoutFilePath))
            {
                File.Delete(_layoutFilePath);
            }
        }

        public DesktopDockLayoutState CreateDefault(
            string runtimeLayoutPresetId,
            DesktopShellState shellState,
            IReadOnlyList<DesktopWorkbenchSurfaceDefinition> surfaces)
        {
            var isFocusLayout = string.Equals(runtimeLayoutPresetId, FocusLayoutId, StringComparison.OrdinalIgnoreCase);
            var state = new DesktopDockLayoutState();
            state.Groups.Add(CreateDefaultGroup(
                DesktopWorkbenchSurfaceRegistry.LeftGroupId,
                isFocusLayout ? 0.18 : 0.22,
                shellState,
                surfaces));
            state.Groups.Add(CreateDefaultGroup(
                DesktopWorkbenchSurfaceRegistry.CenterGroupId,
                isFocusLayout ? 0.64 : 0.56,
                shellState,
                surfaces));
            state.Groups.Add(CreateDefaultGroup(
                DesktopWorkbenchSurfaceRegistry.RightGroupId,
                isFocusLayout ? 0.18 : 0.22,
                shellState,
                surfaces));
            state.Groups.Add(CreateDefaultGroup(
                DesktopWorkbenchSurfaceRegistry.BottomGroupId,
                isFocusLayout ? 0.28 : 0.32,
                shellState,
                surfaces));
            return state;
        }

        private static DesktopDockGroupState CreateDefaultGroup(
            string groupId,
            double proportion,
            DesktopShellState shellState,
            IReadOnlyList<DesktopWorkbenchSurfaceDefinition> surfaces)
        {
            var visibleSurfaceIds = new HashSet<string>(
                (shellState != null
                    ? shellState.SurfaceStates
                    : Enumerable.Empty<DesktopShellSurfaceState>())
                    .Where(surface => surface != null && surface.IsVisible)
                    .Select(surface => surface.SurfaceId ?? string.Empty),
                StringComparer.OrdinalIgnoreCase);

            var groupSurfaces = (surfaces ?? Array.Empty<DesktopWorkbenchSurfaceDefinition>())
                .Where(surface => string.Equals(surface.DefaultGroupId, groupId, StringComparison.OrdinalIgnoreCase) &&
                    visibleSurfaceIds.Contains(surface.SurfaceId))
                .Select(surface => surface.SurfaceId)
                .ToList();

            return new DesktopDockGroupState
            {
                GroupId = groupId,
                Proportion = proportion,
                ActiveSurfaceId = groupSurfaces.FirstOrDefault() ?? string.Empty,
                SurfaceIds = groupSurfaces
            };
        }

        private static DesktopDockGroupState CaptureGroup(IRootDock rootDock, string groupId, string dockId)
        {
            var dock = FindDock(rootDock, dockId);
            var surfaceIds = dock != null && dock.VisibleDockables != null
                ? dock.VisibleDockables.Select(surface => surface != null ? surface.Id ?? string.Empty : string.Empty).Where(id => !string.IsNullOrEmpty(id)).ToList()
                : new List<string>();

            return new DesktopDockGroupState
            {
                GroupId = groupId,
                Proportion = dock != null ? dock.Proportion : 1.0,
                ActiveSurfaceId = dock != null && dock.ActiveDockable != null ? dock.ActiveDockable.Id ?? string.Empty : string.Empty,
                SurfaceIds = surfaceIds
            };
        }

        private static IDock FindDock(IDockable dockable, string targetId)
        {
            if (dockable == null)
            {
                return null;
            }

            var dock = dockable as IDock;
            if (dock != null && string.Equals(dock.Id, targetId, StringComparison.OrdinalIgnoreCase))
            {
                return dock;
            }

            if (dock == null || dock.VisibleDockables == null)
            {
                return null;
            }

            foreach (var child in dock.VisibleDockables)
            {
                var match = FindDock(child, targetId);
                if (match != null)
                {
                    return match;
                }
            }

            return null;
        }
    }
}
