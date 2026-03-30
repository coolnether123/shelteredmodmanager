using System;
using System.Collections.Generic;
using System.IO;
using Cortex.Core.Models;
using Cortex.Modules.Editor;
using Cortex.Modules.Shared;
using Cortex.Renderers.Imgui;
using Cortex.Rendering.Abstractions;
using UnityEngine;

namespace Cortex
{
    public sealed partial class CortexShellController
    {
        private const string DefaultWorkspaceId = "default";

        private void EnsureModuleActivated(string containerId)
        {
            GetModuleActivationService().EnsureActivated(containerId);
        }

        private void RegisterCommandHandlers()
        {
            _commandDispatcher.RegisterCommandHandlers();
        }

        private bool ExecuteCommand(string commandId, object parameter)
        {
            return _commandDispatcher.ExecuteCommand(commandId, parameter);
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
            // The dispatcher now handles the command context internally
            return null;
        }

        private CortexShellModuleServices GetModuleServices()
        {
            if (_moduleServices == null)
            {
                _moduleServices = new CortexShellModuleServices(
                    _state,
                    _services,
                    () => _settingsStore,
                    () => _workbenchRuntime,
                    () => GetRenderPipeline(),
                    _workbenchSearchService);
            }

            return _moduleServices;
        }

        private void ResetModuleRuntime()
        {
            DisposeRenderPipeline();
            _moduleServices = null;
            _moduleCompositionService = null;
            _moduleActivationService = null;
            _moduleRenderService = null;
            _moduleContributionsRegistered = false;
        }

        private IRenderPipeline GetRenderPipeline()
        {
            if (_renderPipeline == null)
            {
                _renderPipeline = new ImguiRenderPipeline();
            }

            return _renderPipeline;
        }

        private void DisposeRenderPipeline()
        {
            var disposable = _renderPipeline as IDisposable;
            if (disposable != null)
            {
                disposable.Dispose();
            }

            _renderPipeline = null;
        }

        private void EnsureModuleContributionsRegistered()
        {
            if (_moduleContributionsRegistered)
            {
                return;
            }

            _moduleRegistrar.RegisterBuiltIns(_moduleContributionRegistry, _extensionRegistry, _runtimeAccess, GetModuleServices());
            _moduleContributionsRegistered = true;
        }

        private CortexShellModuleCompositionService GetModuleCompositionService()
        {
            EnsureModuleContributionsRegistered();
            if (_moduleCompositionService == null)
            {
                _moduleCompositionService = new CortexShellModuleCompositionService(
                    _moduleContributionRegistry,
                    new Shell.WorkbenchModuleRuntimeFactory(_state, _services, () => _workbenchRuntime));
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
                    delegate(string containerId) { return BuildActivationBlockedMessage(containerId); });
            }

            return _moduleRenderService;
        }

        private void RestoreWorkbenchSession() => _sessionCoordinator.RestoreSession();
        private void PersistWorkbenchSession() => _sessionCoordinator.PersistSession();

        private void RestoreSelectedProject(PersistedWorkbenchState persisted)
        {
            // Handled by SessionCoordinator
        }

        private CortexProjectDefinition ResolvePersistedProject(PersistedWorkbenchState persisted)
        {
            // Handled by SessionCoordinator
            return null;
        }

        private CortexProjectDefinition ResolveProjectFromPath(string filePath)
        {
            // Handled by SessionCoordinator
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
