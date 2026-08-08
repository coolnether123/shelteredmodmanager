using System;
using System.Collections.Generic;

namespace ShelteredScenarioEditor.Core
{
    internal interface IServiceResolver
    {
        T Get<T>() where T : class;
        object Get(Type serviceType);
    }

    internal sealed class ServiceCollection
    {
        private sealed class Registration
        {
            public Type ServiceType;
            public Func<IServiceResolver, object> Factory;
        }

        private readonly List<Registration> _registrations = new List<Registration>();

        public void AddSingleton<TService>(Func<IServiceResolver, TService> factory) where TService : class
        {
            _registrations.Add(new Registration
            {
                ServiceType = typeof(TService),
                Factory = delegate(IServiceResolver resolver) { return factory(resolver); }
            });
        }

        public ServiceProvider Build()
        {
            Dictionary<Type, Func<IServiceResolver, object>> factories = new Dictionary<Type, Func<IServiceResolver, object>>();
            for (int i = 0; i < _registrations.Count; i++)
                factories[_registrations[i].ServiceType] = _registrations[i].Factory;
            return new ServiceProvider(factories);
        }
    }

    internal sealed class ServiceProvider : IServiceResolver
    {
        private readonly Dictionary<Type, Func<IServiceResolver, object>> _factories;
        private readonly Dictionary<Type, object> _instances = new Dictionary<Type, object>();
        private readonly object _sync = new object();

        public ServiceProvider(Dictionary<Type, Func<IServiceResolver, object>> factories)
        {
            _factories = factories;
        }

        public T Get<T>() where T : class
        {
            return Get(typeof(T)) as T;
        }

        public object Get(Type serviceType)
        {
            lock (_sync)
            {
                object instance;
                if (_instances.TryGetValue(serviceType, out instance))
                    return instance;

                Func<IServiceResolver, object> factory;
                if (!_factories.TryGetValue(serviceType, out factory))
                    throw new InvalidOperationException("Editor service is not registered: " + serviceType.FullName);

                instance = factory(this);
                _instances[serviceType] = instance;
                return instance;
            }
        }
    }
}
