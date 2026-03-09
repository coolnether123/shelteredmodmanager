using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Presentation.Models;

namespace Cortex
{
    public sealed partial class CortexShell
    {
        private const string DefaultWorkspaceId = "default";

        private void EnsureModuleActivated(string containerId)
        {
            EnsureModuleBindingsInitialized();
            if (string.IsNullOrEmpty(containerId) || _activatedContainers.Contains(containerId) || !CanActivateContainer(containerId))
            {
                return;
            }

            Action activator;
            if (_moduleActivators.TryGetValue(containerId, out activator) && activator != null)
            {
                activator();
            }

            _activatedContainers.Add(containerId);
        }

        private void EnsureModuleBindingsInitialized()
        {
            if (_moduleActivators.Count > 0)
            {
                return;
            }

            RegisterModuleBinding(
                CortexWorkbenchIds.LogsContainer,
                delegate
                {
                    if (_logsModule == null) _logsModule = new Modules.Logs.LogsModule();
                },
                delegate(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    if (_logsModule == null) return;
                    _logsModule.Draw(_runtimeLogFeed, _runtimeSourceNavigationService, _sourcePathResolver, _documentService, _state, detachedWindow);
                });

            RegisterModuleBinding(
                CortexWorkbenchIds.ProjectsContainer,
                delegate
                {
                    if (_projectsModule == null) _projectsModule = new Modules.Projects.ProjectsModule();
                },
                delegate(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    if (_projectsModule == null) return;
                    _projectsModule.Draw(_projectCatalog, _projectWorkspaceService, _loadedModCatalog, _state);
                });

            RegisterModuleBinding(
                CortexWorkbenchIds.EditorContainer,
                delegate
                {
                    if (_editorModule == null) _editorModule = new Modules.Editor.EditorModule();
                },
                delegate(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    if (_editorModule == null) return;
                    _editorModule.Draw(_documentService, _workspaceBrowserService, _projectWorkspaceService, _loadedModCatalog, _state);
                });

            RegisterModuleBinding(
                CortexWorkbenchIds.BuildContainer,
                delegate
                {
                    if (_buildModule == null) _buildModule = new Modules.Build.BuildModule();
                },
                delegate(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    if (_buildModule == null) return;
                    _buildModule.Draw(_buildCommandResolver, _buildExecutor, _restartCoordinator, _sourcePathResolver, _documentService, _state);
                });

            RegisterModuleBinding(
                CortexWorkbenchIds.ReferenceContainer,
                delegate
                {
                    if (_referenceModule == null) _referenceModule = new Modules.Reference.ReferenceModule();
                },
                delegate(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    if (_referenceModule == null) return;
                    _referenceModule.Draw(_sourceReferenceService, _referenceCatalogService, _documentService, _state);
                });

            RegisterModuleBinding(
                CortexWorkbenchIds.RuntimeContainer,
                delegate
                {
                    if (_runtimeToolsModule == null) _runtimeToolsModule = new Modules.Runtime.RuntimeToolsModule();
                },
                delegate(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    if (_runtimeToolsModule == null) return;
                    _runtimeToolsModule.Draw(_runtimeToolBridge, _state);
                });

            RegisterModuleBinding(
                CortexWorkbenchIds.SettingsContainer,
                delegate
                {
                    if (_settingsModule == null) _settingsModule = new Modules.Settings.SettingsModule();
                },
                delegate(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    if (_settingsModule == null) return;
                    _settingsModule.Draw(_settingsStore, snapshot, _workbenchRuntime != null ? _workbenchRuntime.ThemeState : null, _state);
                });
        }

        private void RegisterModuleBinding(string containerId, Action activator, Action<WorkbenchPresentationSnapshot, bool> renderer)
        {
            if (string.IsNullOrEmpty(containerId) || activator == null || renderer == null)
            {
                return;
            }

            _moduleActivators[containerId] = activator;
            _moduleRenderers[containerId] = renderer;
        }

        private void RegisterCommandHandlers()
        {
            if (_workbenchRuntime == null || _workbenchRuntime.CommandRegistry == null)
            {
                return;
            }

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.shell.toggle",
                delegate(CommandExecutionContext context)
                {
                    _visible = !_visible;
                    if (!_visible)
                    {
                        PersistWorkbenchSession();
                        PersistWindowSettings();
                    }
                    _state.StatusMessage = _visible ? "Cortex opened." : "Cortex closed.";
                },
                delegate(CommandExecutionContext context) { return true; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.logs.toggleWindow",
                delegate(CommandExecutionContext context)
                {
                    _state.Logs.ShowDetachedWindow = !_state.Logs.ShowDetachedWindow;
                    _state.StatusMessage = _state.Logs.ShowDetachedWindow ? "Detached logs opened." : "Detached logs hidden.";
                },
                delegate(CommandExecutionContext context) { return true; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.shell.fitWindow",
                delegate(CommandExecutionContext context)
                {
                    FitMainWindowToScreen();
                    _state.StatusMessage = "Workbench fitted to screen.";
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.build.execute",
                delegate(CommandExecutionContext context)
                {
                    ActivateContainer(CortexWorkbenchIds.BuildContainer);
                    _state.StatusMessage = "Build panel focused.";
                },
                delegate(CommandExecutionContext context)
                {
                    return _visible && _state.SelectedProject != null;
                });
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

        private void RestoreWorkbenchSession()
        {
            if (_workbenchPersistenceService == null)
            {
                return;
            }

            var persisted = _workbenchPersistenceService.Load(DefaultWorkspaceId) ?? new PersistedWorkbenchState();
            _state.Workbench.FocusedContainerId = NormalizeContainerId(persisted.FocusedContainerId, CortexWorkbenchIds.EditorContainer);
            _state.Workbench.SideContainerId = NormalizeContainerId(persisted.SideContainerId, string.Empty);
            _state.Workbench.SecondarySideContainerId = NormalizeContainerId(persisted.SecondarySideContainerId, CortexWorkbenchIds.ProjectsContainer);
            _state.Workbench.EditorContainerId = NormalizeContainerId(persisted.EditorContainerId, CortexWorkbenchIds.EditorContainer);
            _state.Workbench.PanelContainerId = NormalizeContainerId(persisted.PanelContainerId, CortexWorkbenchIds.LogsContainer);
            _state.Logs.ShowDetachedWindow = persisted.ShowDetachedLogWindow;
            _state.Documents.EditorUnlocked = persisted.EditorUnlocked;
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

            var restoredDocuments = persisted.OpenDocumentPaths ?? new string[0];
            for (var i = 0; i < restoredDocuments.Length; i++)
            {
                var path = restoredDocuments[i];
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    continue;
                }

                CortexModuleUtil.OpenDocument(_documentService, _state, path, 0);
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

            _workbenchPersistenceService.Save(DefaultWorkspaceId, new PersistedWorkbenchState
            {
                FocusedContainerId = _state.Workbench.FocusedContainerId,
                SideContainerId = _state.Workbench.SideContainerId,
                SecondarySideContainerId = _state.Workbench.SecondarySideContainerId,
                EditorContainerId = _state.Workbench.EditorContainerId,
                PanelContainerId = _state.Workbench.PanelContainerId,
                ShowDetachedLogWindow = _state.Logs.ShowDetachedWindow,
                EditorUnlocked = _state.Documents.EditorUnlocked,
                ActiveDocumentPath = _state.Documents.ActiveDocumentPath ?? string.Empty,
                OpenDocumentPaths = openPaths.ToArray(),
                ContainerHostAssignments = assignments.ToArray()
            });
        }

        private static string NormalizeContainerId(string containerId, string fallback)
        {
            return string.IsNullOrEmpty(containerId) ? fallback : containerId;
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
