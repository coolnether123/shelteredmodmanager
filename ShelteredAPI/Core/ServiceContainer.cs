using System;
using System.Collections.Generic;

namespace ShelteredAPI.Core
{
    internal enum ServiceLifetime
    {
        Singleton = 0,
        Transient = 1
    }

    internal interface IServiceResolver
    {
        T Get<T>() where T : class;
        object Get(Type serviceType);
    }

    internal sealed class ServiceCollection
    {
        private sealed class ServiceRegistration
        {
            public Type ServiceType;
            public Func<IServiceResolver, object> Factory;
            public ServiceLifetime Lifetime;
        }

        private readonly List<ServiceRegistration> _registrations = new List<ServiceRegistration>();

        public void AddSingleton<TService>(Func<IServiceResolver, TService> factory) where TService : class
        {
            Add(typeof(TService), delegate(IServiceResolver resolver) { return factory(resolver); }, ServiceLifetime.Singleton);
        }

        public void AddTransient<TService>(Func<IServiceResolver, TService> factory) where TService : class
        {
            Add(typeof(TService), delegate(IServiceResolver resolver) { return factory(resolver); }, ServiceLifetime.Transient);
        }

        public ServiceProvider Build()
        {
            Dictionary<Type, ServiceProvider.ServiceEntry> entries = new Dictionary<Type, ServiceProvider.ServiceEntry>();
            for (int i = 0; i < _registrations.Count; i++)
            {
                ServiceRegistration registration = _registrations[i];
                entries[registration.ServiceType] = new ServiceProvider.ServiceEntry(
                    registration.ServiceType,
                    registration.Factory,
                    registration.Lifetime);
            }

            return new ServiceProvider(entries);
        }

        private void Add(Type serviceType, Func<IServiceResolver, object> factory, ServiceLifetime lifetime)
        {
            if (serviceType == null)
                throw new ArgumentNullException("serviceType");
            if (factory == null)
                throw new ArgumentNullException("factory");

            _registrations.Add(new ServiceRegistration
            {
                ServiceType = serviceType,
                Factory = factory,
                Lifetime = lifetime
            });
        }
    }

    internal sealed class ServiceProvider : IServiceResolver
    {
        internal sealed class ServiceEntry
        {
            private readonly object _sync = new object();
            private object _singletonInstance;

            public ServiceEntry(Type serviceType, Func<IServiceResolver, object> factory, ServiceLifetime lifetime)
            {
                ServiceType = serviceType;
                Factory = factory;
                Lifetime = lifetime;
            }

            public Type ServiceType { get; private set; }
            public Func<IServiceResolver, object> Factory { get; private set; }
            public ServiceLifetime Lifetime { get; private set; }

            public object Resolve(IServiceResolver resolver)
            {
                if (Lifetime == ServiceLifetime.Transient)
                    return Factory(resolver);

                if (_singletonInstance != null)
                    return _singletonInstance;

                lock (_sync)
                {
                    if (_singletonInstance == null)
                        _singletonInstance = Factory(resolver);
                    return _singletonInstance;
                }
            }
        }

        private readonly Dictionary<Type, ServiceEntry> _entries;

        public ServiceProvider(Dictionary<Type, ServiceEntry> entries)
        {
            _entries = entries ?? new Dictionary<Type, ServiceEntry>();
        }

        public T Get<T>() where T : class
        {
            return Get(typeof(T)) as T;
        }

        public object Get(Type serviceType)
        {
            if (serviceType == null)
                return null;

            ServiceEntry entry;
            if (!_entries.TryGetValue(serviceType, out entry) || entry == null)
                throw new InvalidOperationException("Service is not registered: " + serviceType.FullName);

            return entry.Resolve(this);
        }
    }
}
