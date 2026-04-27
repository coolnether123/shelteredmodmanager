using ShelteredAPI.Core;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioDefinitionModule
    {
        public static void AddScenarioDefinitionModule(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioDependencyService(
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionCatalog>(),
                    resolver.Get<ScenarioAuthoringDraftRepository>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionService(
                    resolver.Get<IScenarioRegistrationStore>(),
                    resolver.Get<IScenarioStateManager>(),
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionCatalog>(),
                    resolver.Get<IScenarioDefinitionValidator>(),
                    resolver.Get<ScenarioAuthoringDraftRepository>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionRegistrationSync(
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionCatalog>(),
                    resolver.Get<IScenarioDefinitionValidator>(),
                    resolver.Get<ScenarioAuthoringDraftRepository>(),
                    resolver.Get<IScenarioRegistrationStore>(),
                    resolver.Get<ScenarioRecordFactory>(),
                    resolver.Get<ScenarioSaveDescriptorMirror>(),
                    resolver.Get<ScenarioDependencyService>(),
                    resolver.Get<ScenarioDefinitionService>());
            });
        }
    }
}
