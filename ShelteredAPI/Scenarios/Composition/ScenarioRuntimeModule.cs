using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ScenarioRuntimeModule
    {
        public static void AddScenarioRuntimeModule(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpriteSwapPlanner(resolver.Get<IScenarioSpriteAssetResolver>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteSwapRenderer(resolver.Get<ScenarioSpriteRuntimeResolver>()); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpriteSwapService(resolver.Get<ScenarioSpriteSwapPlanner>(), resolver.Get<ScenarioSpriteSwapRenderer>());
            });
            services.AddSingleton<IScenarioSpriteSwapEngine>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioSpriteSwapService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSceneSpritePlacementService(
                    resolver.Get<IScenarioSpriteAssetResolver>(),
                    resolver.Get<ScenarioSceneSpritePlacementRoot>(),
                    resolver.Get<ScenarioSceneSpritePlacementRuntimeFactory>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSceneSpritePlacementRoot(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSceneSpritePlacementRuntimeFactory(); });
            services.AddSingleton<IScenarioSceneSpritePlacementEngine>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioSceneSpritePlacementService>(); });
            services.AddSingleton<IScenarioApplier>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioApplyCoordinator>(); });
            services.AddSingleton<IScenarioPlaytestOrchestrator>(delegate(IServiceResolver resolver)
            {
                return new ScenarioPlaytestOrchestrator(
                    resolver.Get<IScenarioApplier>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<IScenarioPauseService>(),
                    resolver.Get<ScenarioAuthorTestChecklistService>());
            });
            services.AddScenarioRuntime();
            services.AddSingleton<IScenarioRuntimeOrchestrator>(delegate(IServiceResolver resolver)
            {
                return new ScenarioRuntimeOrchestrator(
                    resolver.Get<ICustomScenarioLifecycleService>(),
                    resolver.Get<ICustomScenarioRegistry>(),
                    resolver.Get<IScenarioDependencyVerifier>(),
                    resolver.Get<IScenarioDefinitionFactory>(),
                    resolver.Get<IScenarioDefinitionCatalogService>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<IScenarioRuntimeDefinitionOverrideProvider>(),
                    resolver.Get<IScenarioApplier>(),
                    resolver.Get<IScenarioSpriteSwapEngine>(),
                    resolver.Get<IScenarioSceneSpritePlacementEngine>(),
                    resolver.Get<IVanillaScenarioRuntime>());
            });
        }
    }
}
