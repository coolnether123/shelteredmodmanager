using ModAPI.Scenarios;
using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Registration;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ScenarioRegistrationModule
    {
        public static void AddScenarioRegistrationModule(this ServiceCollection services)
        {
            services.AddSingleton<IScenarioRegistrationStore>(delegate(IServiceResolver resolver) { return new ScenarioRegistrationStore(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRegistrationValidator(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRecordFactory(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSaveDescriptorMirror(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioEventHub(resolver.Get<IScenarioStateManager>()); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioRegistrationService(
                    resolver.Get<ScenarioRegistrationValidator>(),
                    resolver.Get<ScenarioRecordFactory>(),
                    resolver.Get<IScenarioRegistrationStore>(),
                    resolver.Get<ScenarioSaveDescriptorMirror>(),
                    resolver.Get<ScenarioEventHub>(),
                    resolver.Get<IScenarioStateManager>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ShelteredCustomScenarioService(
                    resolver.Get<IScenarioRegistrationStore>(),
                    resolver.Get<ScenarioRegistrationService>(),
                    resolver.Get<IScenarioDefinitionFactory>(),
                    resolver.Get<IScenarioDefinitionCatalogService>(),
                    resolver.Get<IScenarioDependencyVerifier>(),
                    resolver.Get<ICustomScenarioLifecycleService>(),
                    resolver.Get<ScenarioEventHub>());
            });
            services.AddSingleton<ICustomScenarioRegistry>(delegate(IServiceResolver resolver) { return resolver.Get<ShelteredCustomScenarioService>(); });
            services.AddSingleton<IShelteredCustomScenarioService>(delegate(IServiceResolver resolver) { return resolver.Get<ShelteredCustomScenarioService>(); });
            services.AddSingleton<ICustomScenarioService>(delegate(IServiceResolver resolver) { return resolver.Get<IShelteredCustomScenarioService>(); });
        }
    }
}
