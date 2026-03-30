using System.Collections.Generic;
using Cortex.Plugins.Abstractions;

namespace Cortex
{
    internal sealed class CortexShellModuleCompositionService
    {
        private readonly CortexShellModuleContributionRegistry _contributionRegistry;
        private readonly Shell.WorkbenchModuleRuntimeFactory _runtimeFactory;
        private readonly Dictionary<string, ModuleRegistration> _instances = new Dictionary<string, ModuleRegistration>(System.StringComparer.OrdinalIgnoreCase);

        public CortexShellModuleCompositionService(
            CortexShellModuleContributionRegistry contributionRegistry,
            Shell.WorkbenchModuleRuntimeFactory runtimeFactory)
        {
            _contributionRegistry = contributionRegistry;
            _runtimeFactory = runtimeFactory;
        }

        /// <summary>
        /// Returns a cached module instance for the specified container, creating it on first access.
        /// </summary>
        /// <param name="containerId">The owning workbench container identifier.</param>
        /// <returns>The cached or newly created module instance, or <c>null</c> when no contribution is registered.</returns>
        public IWorkbenchModule GetOrCreate(string containerId)
        {
            ModuleRegistration registration;
            return TryGetOrCreate(containerId, out registration) ? registration.Module : null;
        }

        public IWorkbenchModuleRuntime GetRuntime(string containerId)
        {
            ModuleRegistration registration;
            return TryGetOrCreate(containerId, out registration) ? registration.Runtime : null;
        }

        public IWorkbenchModuleRuntime GetRuntimeByModuleId(string moduleId)
        {
            if (string.IsNullOrEmpty(moduleId) || _contributionRegistry == null)
            {
                return null;
            }

            var contribution = _contributionRegistry.FindContributionByModuleId(moduleId);
            var descriptor = contribution != null ? contribution.Descriptor : null;
            return descriptor != null ? GetRuntime(descriptor.ContainerId) : null;
        }

        private bool TryGetOrCreate(string containerId, out ModuleRegistration registration)
        {
            registration = null;
            if (string.IsNullOrEmpty(containerId))
            {
                return false;
            }

            if (_instances.TryGetValue(containerId, out registration))
            {
                return registration != null && registration.Module != null;
            }

            var contribution = _contributionRegistry != null ? _contributionRegistry.FindContribution(containerId) : null;
            var runtime = contribution != null && _runtimeFactory != null
                ? _runtimeFactory.Create(contribution.Descriptor)
                : null;
            var created = contribution != null ? contribution.CreateModule(runtime) : null;
            if (created != null)
            {
                registration = new ModuleRegistration
                {
                    Module = created,
                    Runtime = runtime
                };
                _instances[containerId] = registration;
                return true;
            }

            return false;
        }

        private sealed class ModuleRegistration
        {
            public IWorkbenchModule Module;
            public IWorkbenchModuleRuntime Runtime;
        }
    }
}
