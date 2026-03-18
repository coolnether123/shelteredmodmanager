using System;
using System.Collections.Generic;
using Cortex.Presentation.Models;

namespace Cortex.Shell
{
    internal interface ICortexShellModule
    {
        string GetUnavailableMessage();
        void Render(WorkbenchPresentationSnapshot snapshot, bool detachedWindow);
    }

    internal interface ICortexShellModuleContribution
    {
        CortexShellModuleDescriptor Descriptor { get; }
        ICortexShellModule CreateModule();
    }

    internal sealed class CortexShellModuleContributionRegistry
    {
        private readonly Dictionary<string, ICortexShellModuleContribution> _contributions = new Dictionary<string, ICortexShellModuleContribution>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a module contribution for a workbench container.
        /// </summary>
        /// <param name="contribution">The contribution to register.</param>
        public void Register(ICortexShellModuleContribution contribution)
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
        public ICortexShellModuleContribution FindContribution(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return null;
            }

            ICortexShellModuleContribution contribution;
            return _contributions.TryGetValue(containerId, out contribution) ? contribution : null;
        }

        /// <summary>
        /// Resolves the descriptor registered for a workbench container.
        /// </summary>
        /// <param name="containerId">The target workbench container identifier.</param>
        /// <returns>The matching descriptor when one exists; otherwise <c>null</c>.</returns>
        public CortexShellModuleDescriptor FindDescriptor(string containerId)
        {
            var contribution = FindContribution(containerId);
            return contribution != null ? contribution.Descriptor : null;
        }
    }
}
