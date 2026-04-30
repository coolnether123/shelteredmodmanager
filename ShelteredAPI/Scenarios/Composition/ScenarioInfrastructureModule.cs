using ShelteredAPI.Core;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioInfrastructureModule
    {
        public static void AddScenarioInfrastructureModule(this ServiceCollection services)
        {
            services.AddSingleton<IScenarioStateManager>(delegate(IServiceResolver resolver) { return new ScenarioStateManager(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioEditorSessionStore(); });
            services.AddSingleton<IScenarioEditorSessionStore>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioEditorSessionStore>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringPauseService(); });
            services.AddSingleton<IScenarioPauseService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioAuthoringPauseService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringDraftRepository(resolver.Get<IScenarioSaveLibrary>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringHistoryService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringCaptureService(resolver.Get<IScenarioDraftMutationService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioGameplayScheduleAuthoringService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioEventAuthoringService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteRuntimeResolver(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpriteCatalogService(
                    resolver.Get<ScenarioSpriteRuntimeResolver>(),
                    resolver.Get<IScenarioSpriteAssetResolver>());
            });

            services.AddScenarioInfrastructure();
            services.AddScenarioApplication();
        }
    }
}
