using System.Collections.Generic;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleCompositionService
    {
        private readonly CortexShellModuleContributionRegistry _contributionRegistry;
        private readonly Dictionary<string, ICortexShellModule> _instances = new Dictionary<string, ICortexShellModule>(System.StringComparer.OrdinalIgnoreCase);

        public CortexShellModuleCompositionService(CortexShellModuleContributionRegistry contributionRegistry)
        {
            _contributionRegistry = contributionRegistry;
        }

        /// <summary>
        /// Returns a cached module instance for the specified container, creating it on first access.
        /// </summary>
        /// <param name="containerId">The owning workbench container identifier.</param>
        /// <returns>The cached or newly created module instance, or <c>null</c> when no contribution is registered.</returns>
        public ICortexShellModule GetOrCreate(string containerId)
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return null;
            }

            ICortexShellModule existing;
            if (_instances.TryGetValue(containerId, out existing))
            {
                return existing;
            }

            var contribution = _contributionRegistry != null ? _contributionRegistry.FindContribution(containerId) : null;
            var created = contribution != null ? contribution.CreateModule() : null;
            if (created != null)
            {
                _instances[containerId] = created;
            }

            return created;
        }
    }
}
