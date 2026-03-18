using System;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleActivationContext
    {
        private readonly Func<string, bool> _canActivateContainer;

        public CortexShellModuleActivationContext(Func<string, bool> canActivateContainer)
        {
            _canActivateContainer = canActivateContainer;
        }

        public bool CanActivateContainer(string containerId)
        {
            return _canActivateContainer != null && _canActivateContainer(containerId);
        }
    }
}
