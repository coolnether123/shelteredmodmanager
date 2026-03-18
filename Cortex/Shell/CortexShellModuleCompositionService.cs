using System;
using System.Collections.Generic;

namespace Cortex.Shell
{
    internal sealed class CortexShellModuleCompositionService
    {
        private readonly Dictionary<string, object> _instances = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Returns a cached module instance for the specified container, creating it on first access.
        /// </summary>
        /// <typeparam name="TModule">The concrete module type stored for the container.</typeparam>
        /// <param name="containerId">The owning workbench container identifier.</param>
        /// <param name="factory">The factory used to create the module when it has not been composed yet.</param>
        /// <returns>The cached or newly created module instance, or <c>null</c> when creation was not possible.</returns>
        public TModule GetOrCreate<TModule>(string containerId, Func<TModule> factory)
            where TModule : class
        {
            if (string.IsNullOrEmpty(containerId))
            {
                return null;
            }

            object existing;
            if (_instances.TryGetValue(containerId, out existing))
            {
                return existing as TModule;
            }

            var created = factory != null ? factory() : null;
            if (created != null)
            {
                _instances[containerId] = created;
            }

            return created;
        }
    }
}
