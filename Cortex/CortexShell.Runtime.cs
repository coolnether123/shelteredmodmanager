using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.Modules.Shared;
using Cortex.Presentation.Models;
using UnityEngine;

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
                    _logsModule.Draw(_runtimeLogFeed, _sourcePathResolver, _navigationService, _state, detachedWindow);
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
                CortexWorkbenchIds.FileExplorerContainer,
                delegate
                {
                    if (_fileExplorerModule == null) _fileExplorerModule = new Modules.FileExplorer.FileExplorerModule();
                },
                delegate(WorkbenchPresentationSnapshot snapshot, bool detachedWindow)
                {
                    if (_fileExplorerModule == null) return;
                    _fileExplorerModule.Draw(_workspaceBrowserService, _decompilerExplorerService, _navigationService, _state);
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
                    _editorModule.Draw(_documentService, _state);
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
                    _buildModule.Draw(_buildCommandResolver, _buildExecutor, _restartCoordinator, _sourcePathResolver, _navigationService, _state);
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
                    _referenceModule.Draw(_referenceCatalogService, _navigationService, _state);
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
                    _settingsModule.Draw(_settingsStore, _projectCatalog, _projectWorkspaceService, _loadedModCatalog, snapshot, _workbenchRuntime != null ? _workbenchRuntime.ThemeState : null, _state);
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

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.file.saveAll",
                delegate(CommandExecutionContext context)
                {
                    if (_state.Settings == null || !_state.Settings.EnableFileSaving)
                    {
                        _state.StatusMessage = "Enable file saving in Settings before saving source files.";
                        return;
                    }

                    var saved = 0;
                    var blocked = 0;
                    for (var i = 0; i < _state.Documents.OpenDocuments.Count; i++)
                    {
                        var doc = _state.Documents.OpenDocuments[i];
                        if (doc == null || !doc.IsDirty || _documentService == null)
                        {
                            continue;
                        }

                        if (_documentService.Save(doc))
                        {
                            saved++;
                        }
                        else
                        {
                            blocked++;
                        }
                    }

                    _state.StatusMessage = blocked > 0
                        ? "Saved " + saved + " file(s); " + blocked + " blocked by snapshot conflicts."
                        : "Saved " + saved + " file(s).";
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.file.closeActive",
                delegate(CommandExecutionContext context)
                {
                    if (_state.Documents.ActiveDocument != null)
                    {
                        var path = _state.Documents.ActiveDocument.FilePath;
                        Modules.Shared.CortexModuleUtil.CloseDocument(_state, path);
                        _state.StatusMessage = "Closed " + System.IO.Path.GetFileName(path);
                    }
                },
                delegate(CommandExecutionContext context) { return _visible && _state.Documents.ActiveDocument != null; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.file.settings",
                delegate(CommandExecutionContext context)
                {
                    OpenSettingsWindow();
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.view.fileExplorer",
                delegate(CommandExecutionContext context)
                {
                    var isVisible = !_state.Workbench.IsHidden(CortexWorkbenchIds.FileExplorerContainer) &&
                        ResolveHostLocation(CortexWorkbenchIds.FileExplorerContainer) == WorkbenchHostLocation.SecondarySideHost;
                    if (isVisible)
                    {
                        HideContainer(CortexWorkbenchIds.FileExplorerContainer);
                        _state.StatusMessage = "File Explorer hidden.";
                    }
                    else
                    {
                        ActivateContainer(CortexWorkbenchIds.FileExplorerContainer);
                        _state.StatusMessage = "File Explorer shown.";
                    }
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.window.explorer",
                delegate(CommandExecutionContext context)
                {
                    ActivateContainer(CortexWorkbenchIds.FileExplorerContainer);
                    _state.StatusMessage = "Explorer window shown.";
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.window.projects",
                delegate(CommandExecutionContext context)
                {
                    ActivateContainer(CortexWorkbenchIds.ProjectsContainer);
                    _state.StatusMessage = "Projects window shown.";
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.window.references",
                delegate(CommandExecutionContext context)
                {
                    ActivateContainer(CortexWorkbenchIds.ReferenceContainer);
                    _state.StatusMessage = "References window shown.";
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.window.logs",
                delegate(CommandExecutionContext context)
                {
                    ActivateContainer(CortexWorkbenchIds.LogsContainer);
                    _state.StatusMessage = "Logs window shown.";
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.window.build",
                delegate(CommandExecutionContext context)
                {
                    ActivateContainer(CortexWorkbenchIds.BuildContainer);
                    _state.StatusMessage = "Build window shown.";
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.window.runtime",
                delegate(CommandExecutionContext context)
                {
                    ActivateContainer(CortexWorkbenchIds.RuntimeContainer);
                    _state.StatusMessage = "Runtime window shown.";
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.window.settings",
                delegate(CommandExecutionContext context)
                {
                    OpenSettingsWindow();
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.view.zoomIn",
                delegate(CommandExecutionContext context) { _state.StatusMessage = "Font size increase (apply via Settings).";
                },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.view.zoomOut",
                delegate(CommandExecutionContext context) { _state.StatusMessage = "Font size decrease (apply via Settings)."; },
                delegate(CommandExecutionContext context) { return _visible; });

            _workbenchRuntime.CommandRegistry.RegisterHandler(
                "cortex.win.theme",
                delegate(CommandExecutionContext context)
                {
                    if (_workbenchRuntime == null) return;
                    var themes = _workbenchRuntime.ContributionRegistry.GetThemes();
                    if (themes == null || themes.Count == 0) return;
                    var current = _workbenchRuntime.ThemeState.ThemeId;
                    var nextIndex = 0;
                    for (var i = 0; i < themes.Count; i++)
                    {
                        if (string.Equals(themes[i].ThemeId, current, System.StringComparison.OrdinalIgnoreCase))
                        {
                            nextIndex = (i + 1) % themes.Count;
                            break;
                        }
                    }

                    _workbenchRuntime.ThemeState.ThemeId = themes[nextIndex].ThemeId;
                    if (_state.Settings != null)
                    {
                        _state.Settings.ThemeId = _workbenchRuntime.ThemeState.ThemeId;
                    }

                    _state.StatusMessage = "Theme: " + themes[nextIndex].DisplayName;
                },
                delegate(CommandExecutionContext context) { return _visible; });
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
            // Default the primary side host to the file explorer on fresh sessions
            _state.Workbench.SideContainerId = NormalizeWorkspaceContainer(persisted.SideContainerId, string.Empty);
            _state.Workbench.SecondarySideContainerId = NormalizeWorkspaceContainer(persisted.SecondarySideContainerId, CortexWorkbenchIds.FileExplorerContainer);
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

            _workbenchPersistenceService.Save(DefaultWorkspaceId, new PersistedWorkbenchState
            {
                FocusedContainerId = _state.Workbench.FocusedContainerId,
                SideContainerId = _state.Workbench.SideContainerId,
                SecondarySideContainerId = _state.Workbench.SecondarySideContainerId,
                EditorContainerId = _state.Workbench.EditorContainerId,
                PanelContainerId = _state.Workbench.PanelContainerId,
                ShowDetachedLogWindow = _state.Logs.ShowDetachedWindow,
                EditorUnlocked = _state.Documents.EditorUnlocked,
                SelectedProjectModId = _state.SelectedProject != null ? (_state.SelectedProject.ModId ?? string.Empty) : string.Empty,
                SelectedProjectSourceRoot = _state.SelectedProject != null ? (_state.SelectedProject.SourceRootPath ?? string.Empty) : string.Empty,
                ActiveDocumentPath = _state.Documents.ActiveDocumentPath ?? string.Empty,
                OpenDocumentPaths = openPaths.ToArray(),
                ContainerHostAssignments = assignments.ToArray(),
                HiddenContainerIds = new List<string>(_state.Workbench.HiddenContainerIds).ToArray()
            });
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
            _showSettingsWindow = true;
            _settingsWindowRect.width = Mathf.Max(1180f, _windowRect.width - 40f);
            _settingsWindowRect.height = Mathf.Max(760f, _windowRect.height - 20f);
            _settingsWindowRect.x = Mathf.Clamp(_windowRect.x + 20f, 0f, Mathf.Max(0f, Screen.width - _settingsWindowRect.width));
            _settingsWindowRect.y = Mathf.Clamp(_windowRect.y + 20f, 0f, Mathf.Max(0f, Screen.height - _settingsWindowRect.height));
            _state.StatusMessage = "Settings window opened.";
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
