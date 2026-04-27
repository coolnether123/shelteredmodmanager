using ShelteredAPI.Core;

namespace ShelteredAPI.Scenarios
{
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
                return new ScenarioSceneSpritePlacementService(resolver.Get<IScenarioSpriteAssetResolver>());
            });
            services.AddSingleton<IScenarioSceneSpritePlacementEngine>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioSceneSpritePlacementService>(); });
            services.AddSingleton<IScenarioApplier>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioApplyCoordinator>(); });
            services.AddSingleton<IScenarioPlaytestOrchestrator>(delegate(IServiceResolver resolver)
            {
                return new ScenarioPlaytestOrchestrator(
                    resolver.Get<IScenarioApplier>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<IScenarioPauseService>());
            });
            services.AddScenarioRuntime();
            services.AddSingleton<IScenarioRuntimeOrchestrator>(delegate(IServiceResolver resolver)
            {
                return new ScenarioRuntimeOrchestrator(
                    resolver.Get<IShelteredCustomScenarioService>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<IScenarioApplier>(),
                    resolver.Get<IScenarioSpriteSwapEngine>(),
                    resolver.Get<IScenarioSceneSpritePlacementEngine>());
            });
        }
    }
}
