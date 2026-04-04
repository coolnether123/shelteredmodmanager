using System;
using System.Linq;
using Dock.Model.Controls;
using Cortex.Host.Avalonia.Models;
using Cortex.Host.Avalonia.ViewModels;

namespace Cortex.Host.Avalonia.Services
{
    internal sealed class DesktopWorkbenchCompositionService
    {
        private readonly DesktopShellStateStore _shellStateStore;
        private readonly DesktopDockLayoutPersistenceService _dockLayoutPersistenceService;

        public DesktopWorkbenchCompositionService(
            DesktopWorkbenchSurfaceRegistry surfaceRegistry,
            DesktopShellStateStore shellStateStore,
            DesktopDockLayoutPersistenceService dockLayoutPersistenceService)
        {
            SurfaceRegistry = surfaceRegistry ?? new DesktopWorkbenchSurfaceRegistry();
            _shellStateStore = shellStateStore ?? new DesktopShellStateStore(string.Empty);
            _dockLayoutPersistenceService = dockLayoutPersistenceService ?? new DesktopDockLayoutPersistenceService(string.Empty);
            CurrentShellState = _shellStateStore.Load(SurfaceRegistry.Definitions);
        }

        public DesktopWorkbenchSurfaceRegistry SurfaceRegistry { get; }
        public DesktopShellState CurrentShellState { get; private set; }

        public DesktopWorkbenchLayoutComposition ComposeLayout(DesktopShellViewModel shellViewModel, string runtimeLayoutPresetId)
        {
            CurrentShellState.LastRuntimeLayoutPresetId = runtimeLayoutPresetId ?? string.Empty;
            var effectiveLayout = BuildEffectiveLayout(runtimeLayoutPresetId);
            _shellStateStore.Save(CurrentShellState, SurfaceRegistry.Definitions);

            var factory = new DesktopWorkbenchDockFactory(shellViewModel, SurfaceRegistry.Definitions, effectiveLayout);
            IRootDock rootDock = factory.CreateLayout();
            factory.InitLayout(rootDock);

            return new DesktopWorkbenchLayoutComposition
            {
                Factory = factory,
                RootDock = rootDock
            };
        }

        public bool SetSurfaceVisibility(string surfaceId, bool isVisible, string runtimeLayoutPresetId)
        {
            var definition = SurfaceRegistry.Definitions.FirstOrDefault(surface => string.Equals(surface.SurfaceId, surfaceId, StringComparison.OrdinalIgnoreCase));
            if (definition == null)
            {
                return false;
            }

            if (definition.IsRequired && !isVisible)
            {
                return false;
            }

            var state = CurrentShellState.SurfaceStates.FirstOrDefault(surface => string.Equals(surface.SurfaceId, surfaceId, StringComparison.OrdinalIgnoreCase));
            if (state == null)
            {
                return false;
            }

            state.IsVisible = isVisible;
            if (!isVisible && string.Equals(CurrentShellState.ActiveSurfaceId, surfaceId, StringComparison.OrdinalIgnoreCase))
            {
                var activeSurface = CurrentShellState.SurfaceStates.FirstOrDefault(surface => surface.IsVisible);
                CurrentShellState.ActiveSurfaceId = activeSurface != null ? activeSurface.SurfaceId : string.Empty;
            }

            CurrentShellState.LastRuntimeLayoutPresetId = runtimeLayoutPresetId ?? string.Empty;
            _shellStateStore.Save(CurrentShellState, SurfaceRegistry.Definitions);
            return true;
        }

        public void SaveLayout(IRootDock rootDock, string runtimeLayoutPresetId)
        {
            CurrentShellState.UseSavedLayout = true;
            CurrentShellState.LastRuntimeLayoutPresetId = runtimeLayoutPresetId ?? string.Empty;
            _dockLayoutPersistenceService.Save(rootDock);
            _shellStateStore.Save(CurrentShellState, SurfaceRegistry.Definitions);
        }

        public void ResetLayout(string runtimeLayoutPresetId)
        {
            CurrentShellState.UseSavedLayout = false;
            CurrentShellState.LastRuntimeLayoutPresetId = runtimeLayoutPresetId ?? string.Empty;
            _dockLayoutPersistenceService.Delete();
            _shellStateStore.Save(CurrentShellState, SurfaceRegistry.Definitions);
        }

        private DesktopDockLayoutState BuildEffectiveLayout(string runtimeLayoutPresetId)
        {
            var defaultLayout = _dockLayoutPersistenceService.CreateDefault(runtimeLayoutPresetId, CurrentShellState, SurfaceRegistry.Definitions);
            var persistedLayout = CurrentShellState.UseSavedLayout
                ? _dockLayoutPersistenceService.Load()
                : null;
            return MergeLayoutState(persistedLayout, defaultLayout);
        }

        private DesktopDockLayoutState MergeLayoutState(DesktopDockLayoutState candidate, DesktopDockLayoutState defaults)
        {
            var layout = new DesktopDockLayoutState();
            var visibleSurfaceLookup = CurrentShellState.SurfaceStates
                .Where(surface => surface != null && surface.IsVisible)
                .Select(surface => surface.SurfaceId ?? string.Empty)
                .ToList();

            foreach (var defaultGroup in defaults.Groups)
            {
                var candidateGroup = candidate != null
                    ? candidate.Groups.FirstOrDefault(group => string.Equals(group.GroupId, defaultGroup.GroupId, StringComparison.OrdinalIgnoreCase))
                    : null;
                var group = new DesktopDockGroupState
                {
                    GroupId = defaultGroup.GroupId,
                    Proportion = candidateGroup != null && candidateGroup.Proportion > 0
                        ? candidateGroup.Proportion
                        : defaultGroup.Proportion
                };

                var candidateSurfaceIds = candidateGroup != null && candidateGroup.SurfaceIds != null
                    ? candidateGroup.SurfaceIds
                    : defaultGroup.SurfaceIds;

                foreach (var surfaceId in candidateSurfaceIds)
                {
                    if (visibleSurfaceLookup.Contains(surfaceId, StringComparer.OrdinalIgnoreCase) &&
                        AcceptsSurface(defaultGroup.GroupId, surfaceId))
                    {
                        group.SurfaceIds.Add(surfaceId);
                    }
                }

                foreach (var defaultSurfaceId in defaultGroup.SurfaceIds)
                {
                    if (visibleSurfaceLookup.Contains(defaultSurfaceId, StringComparer.OrdinalIgnoreCase) &&
                        !group.SurfaceIds.Contains(defaultSurfaceId, StringComparer.OrdinalIgnoreCase))
                    {
                        group.SurfaceIds.Add(defaultSurfaceId);
                    }
                }

                group.ActiveSurfaceId = group.SurfaceIds.FirstOrDefault(surfaceId =>
                    string.Equals(
                        candidateGroup != null ? candidateGroup.ActiveSurfaceId : defaultGroup.ActiveSurfaceId,
                        surfaceId,
                        StringComparison.OrdinalIgnoreCase))
                    ?? group.SurfaceIds.FirstOrDefault()
                    ?? string.Empty;
                layout.Groups.Add(group);
            }

            return layout;
        }

        private bool AcceptsSurface(string groupId, string surfaceId)
        {
            var definition = SurfaceRegistry.Definitions.FirstOrDefault(surface => string.Equals(surface.SurfaceId, surfaceId, StringComparison.OrdinalIgnoreCase));
            if (definition == null)
            {
                return false;
            }

            return string.Equals(groupId, DesktopWorkbenchSurfaceRegistry.CenterGroupId, StringComparison.OrdinalIgnoreCase)
                ? definition.IsDocument
                : !definition.IsDocument;
        }
    }

    internal sealed class DesktopWorkbenchLayoutComposition
    {
        public DesktopWorkbenchDockFactory Factory { get; set; }
        public IRootDock RootDock { get; set; }
    }
}
