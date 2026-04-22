using System;
using ModAPI.Scenarios;
using ShelteredAPI.Core;

namespace ShelteredAPI.Scenarios
{
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

        public static T Resolve<T>() where T : class
        {
            EnsureInitialized();
            return _provider.Get<T>();
        }

        private static void Configure(ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioDefinitionSerializer(); });
            services.AddSingleton<IScenarioDefinitionSerializer>(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionSerializerAdapter(resolver.Get<ScenarioDefinitionSerializer>());
            });

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioCatalog(new ModRegistryScenarioModFolderSource(), resolver.Get<ScenarioDefinitionSerializer>());
            });
            services.AddSingleton<IScenarioDefinitionCatalog>(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionCatalogAdapter(resolver.Get<ScenarioCatalog>());
            });

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioValidatorImpl(new ScenarioValidator());
            });
            services.AddSingleton<IScenarioDefinitionValidator>(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionValidatorAdapter(resolver.Get<ScenarioValidatorImpl>());
            });

            services.AddSingleton<IScenarioStateManager>(delegate(IServiceResolver resolver) { return new ScenarioStateManager(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringPauseService(); });
            services.AddSingleton<IScenarioPauseService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioAuthoringPauseService>(); });

            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteAssetResolver(); });
            services.AddSingleton<IScenarioSpriteAssetResolver>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioSpriteAssetResolver>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpriteSwapPlanner(resolver.Get<IScenarioSpriteAssetResolver>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteSwapRenderer(); });
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

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ShelteredScenarioRuntimeBindingManager(resolver.Get<IScenarioStateManager>());
            });
            services.AddSingleton<IScenarioRuntimeBindingService>(delegate(IServiceResolver resolver) { return resolver.Get<ShelteredScenarioRuntimeBindingManager>(); });

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ShelteredCustomScenarioService(
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionCatalog>(),
                    resolver.Get<IScenarioDefinitionValidator>(),
                    ScenarioAuthoringDraftRepository.Instance,
                    resolver.Get<IScenarioStateManager>(),
                    resolver.Get<IScenarioRuntimeBindingService>());
            });
            services.AddSingleton<IShelteredCustomScenarioService>(delegate(IServiceResolver resolver) { return resolver.Get<ShelteredCustomScenarioService>(); });
            services.AddSingleton<ICustomScenarioService>(delegate(IServiceResolver resolver) { return resolver.Get<IShelteredCustomScenarioService>(); });

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioApplier(
                    resolver.Get<IScenarioSpriteAssetResolver>(),
                    resolver.Get<IScenarioSpriteSwapEngine>(),
                    resolver.Get<IScenarioSceneSpritePlacementEngine>());
            });
            services.AddSingleton<IScenarioApplier>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioApplier>(); });

            services.AddSingleton<IScenarioPlaytestOrchestrator>(delegate(IServiceResolver resolver)
            {
                return new ScenarioPlaytestOrchestrator(
                    resolver.Get<IScenarioApplier>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<IScenarioPauseService>());
            });

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioEditorController(
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionValidator>(),
                    resolver.Get<IScenarioPlaytestOrchestrator>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<IScenarioPauseService>(),
                    resolver.Get<IScenarioSpriteSwapEngine>(),
                    resolver.Get<IScenarioSceneSpritePlacementEngine>());
            });
            services.AddSingleton<IScenarioEditorService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioEditorController>(); });

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

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringBackendService(
                    new ScenarioAuthoringSelectionService(),
                    ScenarioAuthoringCaptureService.Instance,
                    ScenarioSpriteSwapAuthoringService.Instance,
                    ScenarioSceneSpritePlacementAuthoringService.Instance,
                    resolver.Get<IScenarioEditorService>());
            });
            services.AddSingleton<IScenarioAuthoringBackend>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioAuthoringBackendService>(); });

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringBootstrapService(
                    resolver.Get<ScenarioAuthoringBackendService>(),
                    ScenarioAuthoringDraftRepository.Instance,
                    ScenarioAuthoringMenuService.Instance,
                    ScenarioAuthoringPresentationService.Instance,
                    resolver.Get<IScenarioEditorService>());
            });
        }
    }
}
