using ShelteredAPI.Core;

namespace ShelteredAPI.Scenarios.Composition
{
    internal static class ScenarioRuntimeCompositionRoot
    {
        private static readonly object Sync = new object();
        private static ServiceProvider _provider;

        public static void EnsureInitialized()
        {
            if (_provider != null)
                return;

            lock (Sync)
            {
                if (_provider != null)
                    return;

                ServiceCollection services = new ServiceCollection();
                Configure(services);
                _provider = services.Build();
            }
        }

        public static T Resolve<T>() where T : class
        {
            EnsureInitialized();
            return _provider.Get<T>();
        }

        private static void Configure(ServiceCollection services)
        {
            services.AddScenarioDomain();
            services.AddScenarioInfrastructureModule();
            services.AddScenarioRegistrationModule();
            services.AddScenarioDefinitionModule();
            services.AddScenarioLifecycleModule();
            services.AddScenarioRuntimeModule();
        }
    }
}
