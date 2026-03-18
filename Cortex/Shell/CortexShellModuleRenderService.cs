using Cortex.Presentation.Models;
using UnityEngine;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleRenderService
    {
        private readonly CortexShellModuleDescriptorCatalog _descriptorCatalog;
        private readonly CortexShellModuleCompositionService _compositionService;
        private readonly CortexShellModuleActivationService _activationService;

        public CortexShellModuleRenderService(
            CortexShellModuleDescriptorCatalog descriptorCatalog,
            CortexShellModuleCompositionService compositionService,
            CortexShellModuleActivationService activationService)
        {
            _descriptorCatalog = descriptorCatalog;
            _compositionService = compositionService;
            _activationService = activationService;
        }

        /// <summary>
        /// Renders the active module for a container, including activation and blocked-state fallback handling.
        /// </summary>
        /// <param name="context">The render context containing the services and state visible to modules.</param>
        /// <param name="snapshot">The current workbench presentation snapshot.</param>
        /// <param name="containerId">The target workbench container identifier.</param>
        /// <param name="detachedWindow">Whether the module is being rendered in a detached host window.</param>
        public void DrawActiveModule(CortexShellModuleRenderContext context, WorkbenchPresentationSnapshot snapshot, string containerId, bool detachedWindow)
        {
            if (context == null)
            {
                return;
            }

            if (!context.CanActivateContainer(containerId))
            {
                DrawBlockedMessage(context.BuildActivationBlockedMessage(containerId));
                return;
            }

            if (_activationService != null)
            {
                _activationService.EnsureActivated(context.ActivationContext, containerId);
            }

            var descriptor = _descriptorCatalog != null ? _descriptorCatalog.FindDescriptor(containerId) : null;
            if (descriptor != null && descriptor.Render != null)
            {
                var missingCapabilityMessage = BuildMissingCapabilityMessage(context, descriptor);
                if (!string.IsNullOrEmpty(missingCapabilityMessage))
                {
                    DrawBlockedMessage(missingCapabilityMessage);
                    return;
                }

                descriptor.Render(_compositionService, context, snapshot, detachedWindow);
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

        private static string BuildMissingCapabilityMessage(CortexShellModuleRenderContext context, CortexShellModuleDescriptor descriptor)
        {
            if (context == null || descriptor == null || descriptor.RequiredCapabilityTypes == null || descriptor.RequiredCapabilityTypes.Length == 0)
            {
                return string.Empty;
            }

            var missing = new System.Collections.Generic.List<string>();
            for (var i = 0; i < descriptor.RequiredCapabilityTypes.Length; i++)
            {
                var capabilityType = descriptor.RequiredCapabilityTypes[i];
                if (capabilityType != null && (context.Capabilities == null || !context.Capabilities.Has(capabilityType)))
                {
                    missing.Add(capabilityType.Name);
                }
            }

            return missing.Count > 0
                ? "Module '" + descriptor.ContainerId + "' is missing required capabilities: " + string.Join(", ", missing.ToArray()) + "."
                : string.Empty;
        }
    }
}
