using System;
using System.Collections.Generic;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleActivationService
    {
        private readonly CortexShellModuleDescriptorCatalog _descriptorCatalog;
        private readonly CortexShellModuleCompositionService _compositionService;
        private readonly HashSet<string> _activatedContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public CortexShellModuleActivationService(
            CortexShellModuleDescriptorCatalog descriptorCatalog,
            CortexShellModuleCompositionService compositionService)
        {
            _descriptorCatalog = descriptorCatalog;
            _compositionService = compositionService;
        }

        /// <summary>
        /// Ensures that the module backing a workbench container has been composed and marked active.
        /// </summary>
        /// <param name="context">The activation context that decides whether a container may activate.</param>
        /// <param name="containerId">The target workbench container identifier.</param>
        public void EnsureActivated(CortexShellModuleActivationContext context, string containerId)
        {
            if (context == null || string.IsNullOrEmpty(containerId) || _activatedContainers.Contains(containerId) || !context.CanActivateContainer(containerId))
            {
                return;
            }

            var descriptor = _descriptorCatalog != null ? _descriptorCatalog.FindDescriptor(containerId) : null;
            if (descriptor == null)
            {
                return;
            }

            if (descriptor.EnsureComposed != null)
            {
                descriptor.EnsureComposed(_compositionService);
            }

            _activatedContainers.Add(containerId);
        }
    }
}
