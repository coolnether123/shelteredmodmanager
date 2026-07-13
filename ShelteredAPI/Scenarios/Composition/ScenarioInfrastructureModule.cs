using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Bunker;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ScenarioInfrastructureModule
    {
        public static void AddScenarioInfrastructureModule(this ServiceCollection services)
        {
            services.AddSingleton<IScenarioStateManager>(delegate(IServiceResolver resolver) { return new ScenarioStateManager(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioEditorSessionStore(); });
            services.AddSingleton<IScenarioEditorSessionStore>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioEditorSessionStore>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioRuntimeDefinitionOverrideProvider(resolver.Get<IScenarioEditorSessionStore>());
            });
            services.AddSingleton<IScenarioRuntimeDefinitionOverrideProvider>(delegate(IServiceResolver resolver)
            {
                return resolver.Get<ScenarioRuntimeDefinitionOverrideProvider>();
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringPauseService(); });
            services.AddSingleton<IScenarioPauseService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioAuthoringPauseService>(); });
            services.AddSingleton<IScenarioPlaytestUiService>(delegate(IServiceResolver resolver) { return new ScenarioPlaytestVanillaUiService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringDraftRepository(resolver.Get<IScenarioSaveLibrary>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioDraftMetadataEditService(resolver.Get<ScenarioAuthoringDraftRepository>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringHistoryService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringInventoryProjectionService(resolver.Get<InventoryApplyService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringCaptureService(resolver.Get<IScenarioDraftMutationService>(), resolver.Get<ScenarioActorResolver>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioGameplayScheduleAuthoringService(resolver.Get<ScenarioActorResolver>(), resolver.Get<ScenarioAuthoringInventoryProjectionService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioStoryAuthoringService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioEventAuthoringService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteRuntimeResolver(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteAnimationMetadataService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpritePlacementPolicy(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSceneSpritePlacementCatalogService(
                    resolver.Get<IScenarioSpriteAssetResolver>(),
                    resolver.Get<ScenarioSpritePlacementPolicy>());
            });
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
