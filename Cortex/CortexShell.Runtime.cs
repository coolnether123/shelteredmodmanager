using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.Modules.Shared;

namespace Cortex
{
    public sealed partial class CortexShell
    {
        private const string DefaultWorkspaceId = "default";

        private void EnsureModuleActivated(string containerId)
        {
            if (string.IsNullOrEmpty(containerId) || _activatedContainers.Contains(containerId) || !CanActivateContainer(containerId))
            {
                return;
            }

            switch (containerId)
            {
                case CortexWorkbenchIds.LogsContainer:
                    if (_logsModule == null) _logsModule = new Modules.Logs.LogsModule();
                    break;
                case CortexWorkbenchIds.ProjectsContainer:
                    if (_projectsModule == null) _projectsModule = new Modules.Projects.ProjectsModule();
                    break;
                case CortexWorkbenchIds.EditorContainer:
                    if (_editorModule == null) _editorModule = new Modules.Editor.EditorModule();
                    break;
                case CortexWorkbenchIds.BuildContainer:
                    if (_buildModule == null) _buildModule = new Modules.Build.BuildModule();
                    break;
                case CortexWorkbenchIds.ReferenceContainer:
                    if (_referenceModule == null) _referenceModule = new Modules.Reference.ReferenceModule();
                    break;
                case CortexWorkbenchIds.RuntimeContainer:
                    if (_runtimeToolsModule == null) _runtimeToolsModule = new Modules.Runtime.RuntimeToolsModule();
                    break;
                case CortexWorkbenchIds.SettingsContainer:
                    if (_settingsModule == null) _settingsModule = new Modules.Settings.SettingsModule();
                    break;
            }

            _activatedContainers.Add(containerId);
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
            _state.Workbench.SideContainerId = NormalizeContainerId(persisted.SideContainerId, CortexWorkbenchIds.ProjectsContainer);
            _state.Workbench.EditorContainerId = NormalizeContainerId(persisted.EditorContainerId, CortexWorkbenchIds.EditorContainer);
            _state.Workbench.PanelContainerId = NormalizeContainerId(persisted.PanelContainerId, CortexWorkbenchIds.LogsContainer);
            _state.Logs.ShowDetachedWindow = persisted.ShowDetachedLogWindow;
            _state.Documents.EditorUnlocked = persisted.EditorUnlocked;

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

            _workbenchPersistenceService.Save(DefaultWorkspaceId, new PersistedWorkbenchState
            {
                FocusedContainerId = _state.Workbench.FocusedContainerId,
                SideContainerId = _state.Workbench.SideContainerId,
                EditorContainerId = _state.Workbench.EditorContainerId,
                PanelContainerId = _state.Workbench.PanelContainerId,
                ShowDetachedLogWindow = _state.Logs.ShowDetachedWindow,
                EditorUnlocked = _state.Documents.EditorUnlocked,
                ActiveDocumentPath = _state.Documents.ActiveDocumentPath ?? string.Empty,
                OpenDocumentPaths = openPaths.ToArray()
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
