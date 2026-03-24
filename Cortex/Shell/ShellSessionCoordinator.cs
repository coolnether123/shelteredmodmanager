using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;
using Cortex.Core.Services;
using Cortex.Presentation.Models;
using Cortex.Presentation.Abstractions;
using Cortex.Services;
using UnityEngine;

namespace Cortex.Shell
{
    internal sealed class ShellSessionCoordinator
    {
        private const string DefaultWorkspaceId = "default";
        private readonly CortexShellState _state;
        private readonly CortexShellLifecycleCoordinator _lifecycleCoordinator;
        private readonly Func<CortexNavigationService> _navigationServiceProvider;
        private readonly Func<ILoadedModCatalog> _loadedModCatalogProvider;
        private readonly Func<IProjectCatalog> _projectCatalogProvider;
        private readonly Func<IWorkbenchPersistenceService> _workbenchPersistenceServiceProvider;
        private readonly Func<ICortexSettingsStore> _settingsStoreProvider;

        private bool _visible;

        public ShellSessionCoordinator(
            CortexShellState state,
            CortexShellLifecycleCoordinator lifecycleCoordinator,
            Func<CortexNavigationService> navigationServiceProvider,
            Func<ILoadedModCatalog> loadedModCatalogProvider,
            Func<IProjectCatalog> projectCatalogProvider,
            Func<IWorkbenchPersistenceService> workbenchPersistenceServiceProvider,
            Func<ICortexSettingsStore> settingsStoreProvider)
        {
            _state = state;
            _lifecycleCoordinator = lifecycleCoordinator;
            _navigationServiceProvider = navigationServiceProvider;
            _loadedModCatalogProvider = loadedModCatalogProvider;
            _projectCatalogProvider = projectCatalogProvider;
            _workbenchPersistenceServiceProvider = workbenchPersistenceServiceProvider;
            _settingsStoreProvider = settingsStoreProvider;
        }

        public bool Visible
        {
            get { return _visible; }
            set { _visible = value; }
        }

        public void Start(CortexShellController controller)
        {
            _lifecycleCoordinator.Start(controller);
        }

        public void Shutdown(CortexShellController controller)
        {
            _lifecycleCoordinator.Destroy(controller);
        }

        public void Update(CortexShellController controller)
        {
            _lifecycleCoordinator.Update(controller);
        }

        public void OnGui(CortexShellController controller)
        {
            _lifecycleCoordinator.OnGui(controller);
        }

        public void RestoreSession()
        {
            var workbenchPersistenceService = _workbenchPersistenceServiceProvider != null ? _workbenchPersistenceServiceProvider() : null;
            if (workbenchPersistenceService == null) return;

            var persisted = workbenchPersistenceService.Load(DefaultWorkspaceId) ?? new PersistedWorkbenchState();
            _state.Workbench.FocusedContainerId = NormalizeContainerId(persisted.FocusedContainerId, CortexWorkbenchIds.EditorContainer);
            _state.Workbench.SideContainerId = NormalizeWorkspaceContainer(persisted.SideContainerId, string.Empty);
            _state.Workbench.SecondarySideContainerId = NormalizeWorkspaceContainer(persisted.SecondarySideContainerId, CortexWorkbenchIds.FileExplorerContainer);
            _state.Workbench.EditorContainerId = NormalizeContainerId(persisted.EditorContainerId, CortexWorkbenchIds.EditorContainer);
            _state.Workbench.PanelContainerId = NormalizeContainerId(persisted.PanelContainerId, CortexWorkbenchIds.LogsContainer);
            _state.Logs.ShowDetachedWindow = persisted.ShowDetachedLogWindow;

            var assignments = persisted.ContainerHostAssignments ?? new ContainerHostAssignment[0];
            foreach (var assignment in assignments)
            {
                if (assignment != null && !string.IsNullOrEmpty(assignment.ContainerId))
                {
                    _state.Workbench.AssignHost(assignment.ContainerId, assignment.HostLocation);
                }
            }

            var hiddenContainerIds = persisted.HiddenContainerIds ?? new string[0];
            foreach (var id in hiddenContainerIds)
            {
                if (!string.IsNullOrEmpty(id)) _state.Workbench.HiddenContainerIds.Add(id);
            }

            var restoredDocuments = persisted.OpenDocumentPaths ?? new string[0];
            var navigationService = _navigationServiceProvider != null ? _navigationServiceProvider() : null;
            foreach (var path in restoredDocuments)
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path) && navigationService != null)
                {
                    navigationService.OpenDocument(_state, path, 0, string.Empty, string.Empty);
                }
            }

            if (!string.IsNullOrEmpty(persisted.ActiveDocumentPath))
            {
                var fullActivePath = Path.GetFullPath(persisted.ActiveDocumentPath);
                foreach (var doc in _state.Documents.OpenDocuments)
                {
                    if (string.Equals(doc.FilePath, fullActivePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _state.Documents.ActiveDocument = doc;
                        _state.Documents.ActiveDocumentPath = fullActivePath;
                        break;
                    }
                }
            }

            RestoreSelectedProject(persisted);
        }

        public void PersistSession()
        {
            var workbenchPersistenceService = _workbenchPersistenceServiceProvider != null ? _workbenchPersistenceServiceProvider() : null;
            if (workbenchPersistenceService == null) return;

            var openPaths = new List<string>();
            foreach (var doc in _state.Documents.OpenDocuments)
            {
                if (!string.IsNullOrEmpty(doc?.FilePath)) openPaths.Add(doc.FilePath);
            }

            var assignments = new List<ContainerHostAssignment>();
            foreach (var pair in _state.Workbench.HostOverrides)
            {
                assignments.Add(new ContainerHostAssignment { ContainerId = pair.Key, HostLocation = pair.Value });
            }

            var persistedState = new PersistedWorkbenchState
            {
                FocusedContainerId = _state.Workbench.FocusedContainerId,
                SideContainerId = _state.Workbench.SideContainerId,
                SecondarySideContainerId = _state.Workbench.SecondarySideContainerId,
                EditorContainerId = _state.Workbench.EditorContainerId,
                PanelContainerId = _state.Workbench.PanelContainerId,
                ShowDetachedLogWindow = _state.Logs.ShowDetachedWindow,
                SelectedProjectModId = _state.SelectedProject?.ModId ?? string.Empty,
                SelectedProjectSourceRoot = _state.SelectedProject?.SourceRootPath ?? string.Empty,
                ActiveDocumentPath = _state.Documents.ActiveDocumentPath ?? string.Empty,
                OpenDocumentPaths = openPaths.ToArray(),
                ContainerHostAssignments = assignments.ToArray(),
                HiddenContainerIds = new List<string>(_state.Workbench.HiddenContainerIds).ToArray()
            };

            workbenchPersistenceService.Save(DefaultWorkspaceId, persistedState);
        }

        public void PersistWindowSettings(Rect windowRect, bool isCollapsed, Rect expandedRect)
        {
            var settingsStore = _settingsStoreProvider != null ? _settingsStoreProvider() : null;
            if (_state.Settings == null || settingsStore == null) return;

            var persistedRect = isCollapsed ? expandedRect : windowRect;
            _state.Settings.WindowX = persistedRect.x;
            _state.Settings.WindowY = persistedRect.y;
            _state.Settings.WindowWidth = persistedRect.width;
            _state.Settings.WindowHeight = persistedRect.height;
            settingsStore.Save(_state.Settings);
        }

        private void RestoreSelectedProject(PersistedWorkbenchState persisted)
        {
            _state.SelectedProject = ResolvePersistedProject(persisted);
            if (_state.SelectedProject != null) return;

            var activeDocumentPath = _state.Documents.ActiveDocument?.FilePath ?? _state.Documents.ActiveDocumentPath;
            _state.SelectedProject = ResolveProjectFromPath(activeDocumentPath);
            if (_state.SelectedProject != null) return;

            foreach (var session in _state.Documents.OpenDocuments)
            {
                var project = ResolveProjectFromPath(session?.FilePath);
                if (project != null) { _state.SelectedProject = project; return; }
            }

            var loadedModCatalog = _loadedModCatalogProvider != null ? _loadedModCatalogProvider() : null;
            var projectCatalog = _projectCatalogProvider != null ? _projectCatalogProvider() : null;
            var loadedMods = loadedModCatalog != null ? loadedModCatalog.GetLoadedMods() : null;
            if (loadedMods == null) return;

            foreach (var mod in loadedMods)
            {
                if (string.IsNullOrEmpty(mod?.ModId)) continue;
                var project = projectCatalog != null ? projectCatalog.GetProject(mod.ModId) : null;
                if (project != null) { _state.SelectedProject = project; return; }
            }
        }

        private CortexProjectDefinition ResolvePersistedProject(PersistedWorkbenchState persisted)
        {
            var projectCatalog = _projectCatalogProvider != null ? _projectCatalogProvider() : null;
            if (persisted == null || projectCatalog == null) return null;

            if (!string.IsNullOrEmpty(persisted.SelectedProjectModId))
            {
                var byId = projectCatalog.GetProject(persisted.SelectedProjectModId);
                if (byId != null) return byId;
            }

            if (string.IsNullOrEmpty(persisted.SelectedProjectSourceRoot)) return null;

            var persistedRoot = SafeNormalizeDirectory(persisted.SelectedProjectSourceRoot);
            foreach (var project in projectCatalog.GetProjects())
            {
                if (project == null) continue;
                if (string.Equals(SafeNormalizeDirectory(project.SourceRootPath), persistedRoot, StringComparison.OrdinalIgnoreCase)) return project;
            }

            return null;
        }

        private CortexProjectDefinition ResolveProjectFromPath(string filePath)
        {
            var projectCatalog = _projectCatalogProvider != null ? _projectCatalogProvider() : null;
            if (projectCatalog == null || string.IsNullOrEmpty(filePath)) return null;

            string normalizedFilePath;
            try { normalizedFilePath = Path.GetFullPath(filePath); }
            catch { return null; }

            foreach (var project in projectCatalog.GetProjects())
            {
                var normalizedRoot = SafeNormalizeDirectory(project?.SourceRootPath);
                if (string.IsNullOrEmpty(normalizedRoot)) continue;
                if (normalizedFilePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)) return project;
            }

            return null;
        }

        private static string SafeNormalizeDirectory(string path)
        {
            if (string.IsNullOrEmpty(path)) return string.Empty;
            try
            {
                return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            }
            catch { return string.Empty; }
        }

        private static string NormalizeContainerId(string containerId, string fallback)
        {
            return string.IsNullOrEmpty(containerId) ? fallback : containerId;
        }

        private static string NormalizeWorkspaceContainer(string containerId, string fallback)
        {
            if (string.IsNullOrEmpty(containerId) || string.Equals(containerId, CortexWorkbenchIds.SettingsContainer, StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }
            return containerId;
        }
    }
}
