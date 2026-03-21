using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.Modules.Editor;
using Cortex.Modules.Shared;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShell
    {
        private const string DefaultWorkspaceId = "default";

        private void EnsureModuleActivated(string containerId)
        {
            GetModuleActivationService().EnsureActivated(containerId);
        }

        private void RegisterCommandHandlers()
        {
            _commandRouter.RegisterCommandHandlers(GetCommandContext());
        }

        private bool ExecuteCommand(string commandId, object parameter)
        {
            if (_workbenchRuntime == null || _workbenchRuntime.CommandRegistry == null)
            {
                return false;
            }

            return _workbenchRuntime.CommandRegistry.Execute(commandId, BuildCommandContext(parameter));
        }

        private CommandExecutionContext BuildCommandContext(object parameter)
        {
            return new CommandExecutionContext
            {
                ActiveContainerId = _state.Workbench.FocusedContainerId,
                ActiveDocumentId = _state.Documents.ActiveDocumentPath,
                FocusedRegionId = _workbenchRuntime != null ? _workbenchRuntime.FocusState.FocusedRegionId : string.Empty,
                Parameter = parameter
            };
        }

        private CortexShellCommandContext GetCommandContext()
        {
            if (_commandContext == null)
            {
                _commandContext = new CortexShellCommandContext(
                    _state,
                    delegate { return _workbenchRuntime; },
                    delegate { return _documentService; },
                    delegate { return _visible; },
                    delegate(bool value) { _visible = value; },
                    delegate { PersistWorkbenchSession(); },
                    delegate { PersistWindowSettings(); },
                    delegate { FitMainWindowToScreen(); },
                    delegate(string containerId) { ActivateContainer(containerId); },
                    delegate(string containerId) { return ResolveHostLocation(containerId); },
                    delegate(string containerId) { HideContainer(containerId); },
                    delegate { OpenSettingsWindow(); },
                    delegate { OpenOnboarding(); },
                    delegate { OpenFind(); },
                    delegate(int step) { ExecuteSearchOrAdvance(step); },
                    delegate { CloseFind(); },
                    delegate(EditorCommandTarget target) { _editorSymbolInteractionService.RequestDefinition(_state, target); });
            }

            return _commandContext;
        }

        private CortexShellModuleServices GetModuleServices()
        {
            if (_moduleServices == null)
            {
                _moduleServices = new CortexShellModuleServices(
                    _state,
                    delegate { return _settingsStore; },
                    delegate { return _projectCatalog; },
                    delegate { return _loadedModCatalog; },
                    delegate { return _projectWorkspaceService; },
                    delegate { return _pathInteractionService; },
                    delegate { return _workspaceBrowserService; },
                    delegate { return _decompilerExplorerService; },
                    delegate { return _documentService; },
                    delegate { return _buildCommandResolver; },
                    delegate { return _buildExecutor; },
                    delegate { return _referenceCatalogService; },
                    delegate { return _sourcePathResolver; },
                    delegate { return _runtimeLogFeed; },
                    delegate { return _runtimeToolBridge; },
                    delegate { return _restartCoordinator; },
                    delegate { return _navigationService; },
                    delegate { return _workbenchRuntime; },
                    delegate { return _workbenchSearchService; });
            }

            return _moduleServices;
        }

        private void EnsureModuleContributionsRegistered()
        {
            if (_moduleContributionsRegistered)
            {
                return;
            }

            _moduleRegistrar.RegisterBuiltIns(_moduleContributionRegistry, GetModuleServices());
            _moduleContributionsRegistered = true;
        }

        private CortexShellModuleCompositionService GetModuleCompositionService()
        {
            EnsureModuleContributionsRegistered();
            if (_moduleCompositionService == null)
            {
                _moduleCompositionService = new CortexShellModuleCompositionService(_moduleContributionRegistry);
            }

            return _moduleCompositionService;
        }

        private CortexShellModuleActivationService GetModuleActivationService()
        {
            if (_moduleActivationService == null)
            {
                _moduleActivationService = new CortexShellModuleActivationService(
                    GetModuleCompositionService(),
                    delegate(string containerId) { return CanActivateContainer(containerId); });
            }

            return _moduleActivationService;
        }

        private CortexShellModuleRenderService GetModuleRenderService()
        {
            if (_moduleRenderService == null)
            {
                _moduleRenderService = new CortexShellModuleRenderService(
                    GetModuleCompositionService(),
                    GetModuleActivationService(),
                    delegate(string containerId) { return CanActivateContainer(containerId); },
                    delegate(string containerId) { return BuildActivationBlockedMessage(containerId); },
                    delegate { return _workbenchRuntime != null ? _workbenchRuntime.CommandRegistry : null; },
                    delegate { return _workbenchRuntime != null ? _workbenchRuntime.ContributionRegistry : null; });
            }

            return _moduleRenderService;
        }

        private void RestoreWorkbenchSession()
        {
            if (_workbenchPersistenceService == null)
            {
                return;
            }

            var persisted = _workbenchPersistenceService.Load(DefaultWorkspaceId) ?? new PersistedWorkbenchState();
            _state.Workbench.FocusedContainerId = NormalizeContainerId(persisted.FocusedContainerId, CortexWorkbenchIds.EditorContainer);
            // Default the primary side host to the file explorer on fresh sessions
            _state.Workbench.SideContainerId = NormalizeWorkspaceContainer(persisted.SideContainerId, string.Empty);
            _state.Workbench.SecondarySideContainerId = NormalizeWorkspaceContainer(persisted.SecondarySideContainerId, CortexWorkbenchIds.FileExplorerContainer);
            _state.Workbench.EditorContainerId = NormalizeContainerId(persisted.EditorContainerId, CortexWorkbenchIds.EditorContainer);
            _state.Workbench.PanelContainerId = NormalizeContainerId(persisted.PanelContainerId, CortexWorkbenchIds.LogsContainer);
            _state.Logs.ShowDetachedWindow = persisted.ShowDetachedLogWindow;
            RestoreOnboardingSessionState(persisted);
            _state.Onboarding.SelectedProfileId = _state.Onboarding.ActiveProfileId;
            _state.Onboarding.SelectedLayoutPresetId = _state.Onboarding.ActiveLayoutPresetId;
            _state.Onboarding.SelectedThemeId = _state.Onboarding.ActiveThemeId;
            var assignments = persisted.ContainerHostAssignments ?? new ContainerHostAssignment[0];
            for (var i = 0; i < assignments.Length; i++)
            {
                var assignment = assignments[i];
                if (assignment == null || string.IsNullOrEmpty(assignment.ContainerId))
                {
                    continue;
                }

                _state.Workbench.AssignHost(assignment.ContainerId, assignment.HostLocation);
            }

            var hiddenContainerIds = persisted.HiddenContainerIds ?? new string[0];
            for (var i = 0; i < hiddenContainerIds.Length; i++)
            {
                if (!string.IsNullOrEmpty(hiddenContainerIds[i]))
                {
                    _state.Workbench.HiddenContainerIds.Add(hiddenContainerIds[i]);
                }
            }

            if (_state.Workbench.IsHidden(_state.Workbench.SideContainerId))
            {
                _state.Workbench.SideContainerId = string.Empty;
            }

            if (_state.Workbench.IsHidden(_state.Workbench.SecondarySideContainerId))
            {
                _state.Workbench.SecondarySideContainerId = string.Empty;
            }

            if (_state.Workbench.IsHidden(_state.Workbench.PanelContainerId))
            {
                _state.Workbench.PanelContainerId = string.Empty;
            }

            var restoredDocuments = persisted.OpenDocumentPaths ?? new string[0];
            for (var i = 0; i < restoredDocuments.Length; i++)
            {
                var path = restoredDocuments[i];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    continue;
                }

                if (_navigationService != null)
                {
                    _navigationService.OpenDocument(_state, path, 0, string.Empty, string.Empty);
                }
            }

            if (!string.IsNullOrEmpty(persisted.ActiveDocumentPath))
            {
                var fullActivePath = Path.GetFullPath(persisted.ActiveDocumentPath);
                for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
                {
                    if (string.Equals(_state.Documents.OpenDocuments[i].FilePath, fullActivePath, StringComparison.OrdinalIgnoreCase))
                    {
                        _state.Documents.ActiveDocument = _state.Documents.OpenDocuments[i];
                        _state.Documents.ActiveDocumentPath = fullActivePath;
                        break;
                    }
                }
            }

            RestoreSelectedProject(persisted);
        }

        private void PersistWorkbenchSession()
        {
            if (_workbenchPersistenceService == null)
            {
                return;
            }

            var openPaths = new List<string>();
            for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
            {
                var filePath = _state.Documents.OpenDocuments[i] != null ? _state.Documents.OpenDocuments[i].FilePath : string.Empty;
                if (!string.IsNullOrEmpty(filePath))
                {
                    openPaths.Add(filePath);
                }
            }

            var assignments = new List<ContainerHostAssignment>();
            foreach (var pair in _state.Workbench.HostOverrides)
            {
                assignments.Add(new ContainerHostAssignment
                {
                    ContainerId = pair.Key,
                    HostLocation = pair.Value
                });
            }

            var persistedState = new PersistedWorkbenchState
            {
                FocusedContainerId = _state.Workbench.FocusedContainerId,
                SideContainerId = _state.Workbench.SideContainerId,
                SecondarySideContainerId = _state.Workbench.SecondarySideContainerId,
                EditorContainerId = _state.Workbench.EditorContainerId,
                PanelContainerId = _state.Workbench.PanelContainerId,
                ShowDetachedLogWindow = _state.Logs.ShowDetachedWindow,
                SelectedProjectModId = _state.SelectedProject != null ? (_state.SelectedProject.ModId ?? string.Empty) : string.Empty,
                SelectedProjectSourceRoot = _state.SelectedProject != null ? (_state.SelectedProject.SourceRootPath ?? string.Empty) : string.Empty,
                ActiveDocumentPath = _state.Documents.ActiveDocumentPath ?? string.Empty,
                OpenDocumentPaths = openPaths.ToArray(),
                ContainerHostAssignments = assignments.ToArray(),
                HiddenContainerIds = new List<string>(_state.Workbench.HiddenContainerIds).ToArray()
            };

            PersistOnboardingSessionState(persistedState);
            _workbenchPersistenceService.Save(DefaultWorkspaceId, persistedState);
        }

        private void RestoreSelectedProject(PersistedWorkbenchState persisted)
        {
            _state.SelectedProject = ResolvePersistedProject(persisted);
            if (_state.SelectedProject != null)
            {
                return;
            }

            var activeDocumentPath = _state.Documents.ActiveDocument != null
                ? _state.Documents.ActiveDocument.FilePath
                : _state.Documents.ActiveDocumentPath;
            _state.SelectedProject = ResolveProjectFromPath(activeDocumentPath);
            if (_state.SelectedProject != null)
            {
                return;
            }

            for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
            {
                var session = _state.Documents.OpenDocuments[i];
                var project = ResolveProjectFromPath(session != null ? session.FilePath : string.Empty);
                if (project != null)
                {
                    _state.SelectedProject = project;
                    return;
                }
            }

            var loadedMods = _loadedModCatalog != null ? _loadedModCatalog.GetLoadedMods() : null;
            if (loadedMods == null)
            {
                return;
            }

            for (var i = 0; i < loadedMods.Count; i++)
            {
                var mod = loadedMods[i];
                if (mod == null || string.IsNullOrEmpty(mod.ModId))
                {
                    continue;
                }

                var project = _projectCatalog != null ? _projectCatalog.GetProject(mod.ModId) : null;
                if (project != null)
                {
                    _state.SelectedProject = project;
                    return;
                }
            }
        }

        private CortexProjectDefinition ResolvePersistedProject(PersistedWorkbenchState persisted)
        {
            if (persisted == null || _projectCatalog == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(persisted.SelectedProjectModId))
            {
                var byId = _projectCatalog.GetProject(persisted.SelectedProjectModId);
                if (byId != null)
                {
                    return byId;
                }
            }

            if (string.IsNullOrEmpty(persisted.SelectedProjectSourceRoot))
            {
                return null;
            }

            var persistedRoot = SafeNormalizeDirectory(persisted.SelectedProjectSourceRoot);
            var projects = _projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                if (project == null)
                {
                    continue;
                }

                if (string.Equals(SafeNormalizeDirectory(project.SourceRootPath), persistedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }

            return null;
        }

        private CortexProjectDefinition ResolveProjectFromPath(string filePath)
        {
            if (_projectCatalog == null || string.IsNullOrEmpty(filePath))
            {
                return null;
            }

            string normalizedFilePath;
            try
            {
                normalizedFilePath = Path.GetFullPath(filePath);
            }
            catch
            {
                return null;
            }

            var projects = _projectCatalog.GetProjects();
            for (var i = 0; i < projects.Count; i++)
            {
                var project = projects[i];
                var normalizedRoot = SafeNormalizeDirectory(project != null ? project.SourceRootPath : string.Empty);
                if (string.IsNullOrEmpty(normalizedRoot))
                {
                    continue;
                }

                if (normalizedFilePath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                {
                    return project;
                }
            }

            return null;
        }

        private static string SafeNormalizeDirectory(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            try
            {
                return Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeContainerId(string containerId, string fallback)
        {
            return string.IsNullOrEmpty(containerId) ? fallback : containerId;
        }

        private void OpenSettingsWindow()
        {
            EnsureModuleActivated(CortexWorkbenchIds.SettingsContainer);
            _state.Workbench.EditorContainerId = CortexWorkbenchIds.SettingsContainer;
            _state.Workbench.FocusedContainerId = CortexWorkbenchIds.SettingsContainer;
            if (_workbenchRuntime != null)
            {
                _workbenchRuntime.WorkbenchState.ActiveContainerId = CortexWorkbenchIds.SettingsContainer;
                _workbenchRuntime.WorkbenchState.ActiveEditorGroupId = CortexWorkbenchIds.SettingsContainer;
                _workbenchRuntime.FocusState.FocusedRegionId = CortexWorkbenchIds.SettingsContainer;
            }

            _state.StatusMessage = "Settings opened in the editor surface.";
        }
        private static string NormalizeWorkspaceContainer(string containerId, string fallback)
        {
            if (string.IsNullOrEmpty(containerId) ||
                string.Equals(containerId, CortexWorkbenchIds.SettingsContainer, StringComparison.OrdinalIgnoreCase))
            {
                return fallback;
            }

            return containerId;
        }

        private bool CanActivateContainer(string containerId)
        {
            var contribution = GetContainerContribution(containerId);
            if (contribution == null)
            {
                return true;
            }

            switch (contribution.ActivationKind)
            {
                case ModuleActivationKind.OnWorkspaceAvailable:
                    return _state.Settings != null &&
                        !string.IsNullOrEmpty(_state.Settings.WorkspaceRootPath) &&
                        Directory.Exists(_state.Settings.WorkspaceRootPath);
                case ModuleActivationKind.OnDocumentRestore:
                    return true;
                case ModuleActivationKind.OnCommand:
                    return true;
                case ModuleActivationKind.OnContainerOpen:
                case ModuleActivationKind.Immediate:
                default:
                    return true;
            }
        }

        private ViewContainerContribution GetContainerContribution(string containerId)
        {
            if (_workbenchRuntime == null || _workbenchRuntime.ContributionRegistry == null || string.IsNullOrEmpty(containerId))
            {
                return null;
            }

            var containers = _workbenchRuntime.ContributionRegistry.GetViewContainers();
            for (var i = 0; i < containers.Count; i++)
            {
                if (string.Equals(containers[i].ContainerId, containerId, StringComparison.OrdinalIgnoreCase))
                {
                    return containers[i];
                }
            }

            return null;
        }

        private string BuildActivationBlockedMessage(string containerId)
        {
            var contribution = GetContainerContribution(containerId);
            if (contribution != null && contribution.ActivationKind == ModuleActivationKind.OnWorkspaceAvailable)
            {
                return "This module activates when a valid workspace root is configured.";
            }
            if (contribution != null && contribution.ActivationKind == ModuleActivationKind.OnCommand)
            {
                return "This module activates through its command entry point.";
            }

            return "This module is not available yet.";
        }
    }
}
