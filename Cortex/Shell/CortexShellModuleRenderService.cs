using System;
using Cortex.Plugins.Abstractions;
using Cortex.Presentation.Models;
using Cortex.Rendering.RuntimeUi;
using UnityEngine;

namespace Cortex
{
    internal sealed class CortexShellModuleRenderService
    {
        private readonly CortexShellModuleCompositionService _compositionService;
        private readonly CortexShellModuleActivationService _activationService;
        private readonly Func<IWorkbenchUiSurface> _workbenchUiSurfaceProvider;
        private readonly Func<string, bool> _canActivateContainer;
        private readonly Func<string, string> _buildActivationBlockedMessage;

        public CortexShellModuleRenderService(
            CortexShellModuleCompositionService compositionService,
            CortexShellModuleActivationService activationService,
            Func<IWorkbenchUiSurface> workbenchUiSurfaceProvider,
            Func<string, bool> canActivateContainer,
            Func<string, string> buildActivationBlockedMessage)
        {
            _compositionService = compositionService;
            _activationService = activationService;
            _workbenchUiSurfaceProvider = workbenchUiSurfaceProvider;
            _canActivateContainer = canActivateContainer;
            _buildActivationBlockedMessage = buildActivationBlockedMessage;
        }

        /// <summary>
        /// Renders the active module for a container, including activation and blocked-state fallback handling.
        /// </summary>
        /// <param name="snapshot">The current workbench presentation snapshot.</param>
        /// <param name="containerId">The target workbench container identifier.</param>
        /// <param name="detachedWindow">Whether the module is being rendered in a detached host window.</param>
        public void DrawActiveModule(WorkbenchPresentationSnapshot snapshot, string containerId, bool detachedWindow)
        {
            if (_canActivateContainer != null && !_canActivateContainer(containerId))
            {
                DrawBlockedMessage(_buildActivationBlockedMessage != null ? _buildActivationBlockedMessage(containerId) : string.Empty);
                return;
            }

            if (_activationService != null)
            {
                _activationService.EnsureActivated(containerId);
            }

            var module = _compositionService != null ? _compositionService.GetOrCreate(containerId) : null;
            if (module != null)
            {
                var unavailableMessage = module.GetUnavailableMessage();
                if (!string.IsNullOrEmpty(unavailableMessage))
                {
                    DrawBlockedMessage(unavailableMessage);
                    return;
                }

                module.Render(
                    new WorkbenchModuleRenderContext(
                        containerId,
                        snapshot,
                        _workbenchUiSurfaceProvider != null ? _workbenchUiSurfaceProvider() : NullWorkbenchUiSurface.Instance,
                        _compositionService.GetRuntime(containerId)),
                    detachedWindow);
                return;
            }

            DrawBlockedMessage("No renderer is registered for " + containerId + ".");
        }

        private static void DrawBlockedMessage(string message)
        {
            GUILayout.BeginVertical(GUI.skin.box, GUILayout.ExpandHeight(true));
            GUILayout.Label(message ?? string.Empty);
            GUILayout.EndVertical();
        }
    }
}
