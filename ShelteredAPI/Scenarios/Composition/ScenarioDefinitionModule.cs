using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Registration;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ScenarioDefinitionModule
    {
        public static void AddScenarioDefinitionModule(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionReader(
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionCatalog>(),
                    resolver.Get<IScenarioDefinitionValidator>());
            });
            services.AddSingleton<IScenarioDefinitionReader>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioDefinitionReader>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioDependencyService(
                    resolver.Get<IScenarioDefinitionReader>());
            });
            services.AddSingleton<IScenarioDependencyVerifier>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioDependencyService>(); });
            services.AddSingleton<IScenarioDefinitionDependencyReader>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioDependencyService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionService(
                    resolver.Get<IScenarioRegistrationStore>(),
                    resolver.Get<IScenarioStateManager>(),
                    resolver.Get<IScenarioDefinitionReader>());
            });
            services.AddSingleton<IScenarioDefinitionFactory>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioDefinitionService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionRegistrationSync(
                    resolver.Get<IScenarioDefinitionCatalog>(),
                    resolver.Get<IScenarioDefinitionReader>(),
                    resolver.Get<IScenarioRegistrationStore>(),
                    resolver.Get<ScenarioRecordFactory>(),
                    resolver.Get<ScenarioSaveDescriptorMirror>(),
                    resolver.Get<IScenarioDefinitionDependencyReader>(),
                    resolver.Get<IScenarioDefinitionFactory>());
            });
            services.AddSingleton<IScenarioDefinitionCatalogService>(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionCatalogRefreshCoordinator(
                    resolver.Get<ScenarioDefinitionRegistrationSync>(),
                    delegate { return resolver.Get<IScenarioRuntimeOrchestrator>(); });
            });
        }
    }
}
