using System;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleDescriptor
    {
        public CortexShellModuleDescriptor(
            string containerId,
            Type moduleType)
        {
            ContainerId = containerId ?? string.Empty;
            ModuleType = moduleType;
        }

        public string ContainerId { get; private set; }

        public Type ModuleType { get; private set; }
    }
}
