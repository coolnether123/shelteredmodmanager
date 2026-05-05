using System;
using ShelteredAPI.Core;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ScenarioCompositionRoot
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

        public static void EnsureRuntimeInitialized()
        {
            EnsureInitialized();
        }

        public static void EnsureAuthoringInitialized()
        {
            EnsureInitialized();
        }

        public static T Resolve<T>() where T : class
        {
            EnsureAuthoringInitialized();
            return _provider.Get<T>();
        }

        public static T ResolveRuntime<T>() where T : class
        {
            EnsureRuntimeInitialized();
            return _provider.Get<T>();
        }

        private static void Configure(ServiceCollection services)
        {
            services.AddScenarioDomainModule();
            services.AddScenarioInfrastructureModule();
            services.AddScenarioRegistrationModule();
            services.AddScenarioDefinitionModule();
            services.AddScenarioLifecycleModule();
            services.AddScenarioRuntimeModule();
            services.AddScenarioAuthoringModule();
            services.AddScenarioPresentationModule();
        }
    }
}
