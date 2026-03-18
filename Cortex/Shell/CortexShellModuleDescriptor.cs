using System;
using Cortex.Presentation.Models;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleDescriptor
    {
        public CortexShellModuleDescriptor(
            string containerId,
            Type[] requiredCapabilityTypes,
            Action<CortexShellModuleCompositionService> ensureComposed,
            Action<CortexShellModuleCompositionService, CortexShellModuleRenderContext, WorkbenchPresentationSnapshot, bool> render)
        {
            ContainerId = containerId ?? string.Empty;
            RequiredCapabilityTypes = requiredCapabilityTypes ?? new Type[0];
            EnsureComposed = ensureComposed;
            Render = render;
        }

        public string ContainerId { get; private set; }

        public Type[] RequiredCapabilityTypes { get; private set; }

        public Action<CortexShellModuleCompositionService> EnsureComposed { get; private set; }

        public Action<CortexShellModuleCompositionService, CortexShellModuleRenderContext, WorkbenchPresentationSnapshot, bool> Render { get; private set; }
    }
}
