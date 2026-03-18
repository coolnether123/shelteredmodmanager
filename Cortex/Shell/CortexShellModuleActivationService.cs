using System;
using System.Collections.Generic;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleActivationService
    {
        private readonly CortexShellModuleCompositionService _compositionService;
        private readonly Func<string, bool> _canActivateContainer;
        private readonly HashSet<string> _activatedContainers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        public CortexShellModuleActivationService(
            CortexShellModuleCompositionService compositionService,
            Func<string, bool> canActivateContainer)
        {
            _compositionService = compositionService;
            _canActivateContainer = canActivateContainer;
        }

        /// <summary>
        /// Ensures that the module backing a workbench container has been composed and marked active.
        /// </summary>
        /// <param name="containerId">The target workbench container identifier.</param>
        public void EnsureActivated(string containerId)
        {
            if (string.IsNullOrEmpty(containerId) || _activatedContainers.Contains(containerId) || (_canActivateContainer != null && !_canActivateContainer(containerId)))
            {
                return;
            }

            var module = _compositionService != null ? _compositionService.GetOrCreate(containerId) : null;
            if (module == null)
            {
                return;
            }

            _activatedContainers.Add(containerId);
        }
    }
}
