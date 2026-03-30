using System;
using System.Collections.Generic;
using Cortex.Core.Abstractions;

namespace Cortex.Shell
{
    internal sealed class ShellFeatureRegistry : ICortexPlatformFeatureRegistry
    {
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();

        public void Add<TService>(TService service) where TService : class
        {
            if (service == null)
            {
                return;
            }

            _services[typeof(TService)] = service;
        }

        public bool TryGet<TService>(out TService service) where TService : class
        {
            object value;
            if (_services.TryGetValue(typeof(TService), out value))
            {
                service = value as TService;
                return service != null;
            }

            service = null;
            return false;
        }

        public TService Get<TService>() where TService : class
        {
            TService service;
            return TryGet(out service) ? service : null;
        }
    }
}
