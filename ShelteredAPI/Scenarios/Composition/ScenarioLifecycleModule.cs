using ShelteredAPI.Core;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioLifecycleModule
    {
        public static void AddScenarioLifecycleModule(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ShelteredScenarioRuntimeBindingManager(resolver.Get<IScenarioStateManager>());
            });
            services.AddSingleton<IScenarioRuntimeBindingService>(delegate(IServiceResolver resolver) { return resolver.Get<ShelteredScenarioRuntimeBindingManager>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioLifecycleService(
                    resolver.Get<IScenarioRegistrationStore>(),
                    resolver.Get<ScenarioDependencyService>(),
                    resolver.Get<IScenarioStateManager>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<ScenarioEventHub>());
            });
        }
    }
}
