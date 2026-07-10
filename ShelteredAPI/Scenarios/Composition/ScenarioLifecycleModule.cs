using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Persistence;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Lifecycle;
using ShelteredAPI.Scenarios.Registration;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ScenarioLifecycleModule
    {
        public static void AddScenarioLifecycleModule(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRuntimeBindingPersistence(); });
            services.AddSingleton<IScenarioRuntimeBindingPersistence>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioRuntimeBindingPersistence>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ShelteredScenarioRuntimeBindingManager(
                    resolver.Get<IScenarioStateManager>(),
                    resolver.Get<IScenarioRuntimeBindingPersistence>(),
                    resolver.Get<IVanillaScenarioRuntime>());
            });
            services.AddSingleton<IScenarioRuntimeBindingService>(delegate(IServiceResolver resolver) { return resolver.Get<ShelteredScenarioRuntimeBindingManager>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioLifecycleService(
                    resolver.Get<IScenarioRegistrationStore>(),
                    resolver.Get<IScenarioDependencyVerifier>(),
                    resolver.Get<IScenarioStateManager>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<ScenarioEventHub>());
            });
            services.AddSingleton<ICustomScenarioLifecycleService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioLifecycleService>(); });
        }
    }
}
