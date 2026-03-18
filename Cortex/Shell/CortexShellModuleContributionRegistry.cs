using System;
using System.Collections.Generic;
using Cortex.Plugins.Abstractions;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleContributionRegistry : IWorkbenchModuleRegistry
    {
        private readonly Dictionary<string, IWorkbenchModuleContribution> _contributions = new Dictionary<string, IWorkbenchModuleContribution>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a module contribution for a workbench container.
        /// </summary>
        /// <param name="contribution">The contribution to register.</param>
        public void Register(IWorkbenchModuleContribution contribution)
        {
            if (contribution == null || contribution.Descriptor == null || string.IsNullOrEmpty(contribution.Descriptor.ContainerId))
            {
                return;
            }

            _contributions[contribution.Descriptor.ContainerId] = contribution;
        }

        /// <summary>
        /// Resolves the contribution registered for a workbench container.
        /// </summary>
        /// <param name="containerId">The target workbench container identifier.</param>
        /// <returns>The registered contribution when one exists; otherwise <c>null</c>.</returns>
        public IWorkbenchModuleContribution FindContribution(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return null;
            }

            IWorkbenchModuleContribution contribution;
            return _contributions.TryGetValue(containerId, out contribution) ? contribution : null;
        }

        /// <summary>
        /// Resolves the descriptor registered for a workbench container.
        /// </summary>
        /// <param name="containerId">The target workbench container identifier.</param>
        /// <returns>The matching descriptor when one exists; otherwise <c>null</c>.</returns>
        public WorkbenchModuleDescriptor FindDescriptor(string containerId)
        {
            var contribution = FindContribution(containerId);
            return contribution != null ? contribution.Descriptor : null;
        }
    }
}
