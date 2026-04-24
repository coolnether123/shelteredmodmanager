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
            services.AddScenarioDomain();

            services.AddSingleton<IScenarioStateManager>(delegate(IServiceResolver resolver) { return new ScenarioStateManager(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringPauseService(); });
            services.AddSingleton<IScenarioPauseService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioAuthoringPauseService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringDraftRepository(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringHistoryService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringCaptureService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioGameplayScheduleAuthoringService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteRuntimeResolver(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteCatalogService(resolver.Get<ScenarioSpriteRuntimeResolver>()); });

            services.AddScenarioInfrastructure();
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
            services.AddScenarioApplication();

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
                    resolver.Get<ScenarioAuthoringDraftRepository>(),
                    resolver.Get<IScenarioStateManager>(),
                    resolver.Get<IScenarioRuntimeBindingService>());
            });
            services.AddSingleton<IShelteredCustomScenarioService>(delegate(IServiceResolver resolver) { return resolver.Get<ShelteredCustomScenarioService>(); });
            services.AddSingleton<ICustomScenarioService>(delegate(IServiceResolver resolver) { return resolver.Get<IShelteredCustomScenarioService>(); });

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioCharacterAppearanceService(resolver.Get<IScenarioSpriteAssetResolver>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSceneSpritePlacementAuthoringService(
                    resolver.Get<ScenarioSpriteCatalogService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>(),
                    resolver.Get<IScenarioSceneSpritePlacementEngine>(),
                    resolver.Get<IScenarioEditorService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpriteSwapAuthoringService(
                    resolver.Get<ScenarioSpriteCatalogService>(),
                    resolver.Get<ScenarioCharacterAppearanceService>(),
                    resolver.Get<ScenarioSpriteRuntimeResolver>(),
                    resolver.Get<SpritePatchBuilder>(),
                    resolver.Get<ScenarioAuthoringHistoryService>(),
                    resolver.Get<IScenarioSpriteSwapEngine>(),
                    resolver.Get<IScenarioSceneSpritePlacementEngine>(),
                    resolver.Get<IScenarioEditorService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioBuildPlacementAuthoringService(
                    resolver.Get<StructurePlacementService>(),
                    resolver.Get<ObjectPlacementService>(),
                    resolver.Get<WallWiringEditService>(),
                    resolver.Get<PlacementPaletteService>(),
                    resolver.Get<PlacementGhostSessionService>(),
                    resolver.Get<IScenarioEditorService>());
            });
            services.AddSingleton<IScenarioApplier>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioApplyCoordinator>(); });

            services.AddSingleton<IScenarioPlaytestOrchestrator>(delegate(IServiceResolver resolver)
            {
                return new ScenarioPlaytestOrchestrator(
                    resolver.Get<IScenarioApplier>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<IScenarioPauseService>());
            });

            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringWindowRegistry(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringSettingsService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTargetClassifier(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSelectionScopeService(resolver.Get<ScenarioTargetClassifier>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringLayoutService(
                    resolver.Get<ScenarioAuthoringWindowRegistry>(),
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioStageCoordinator>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringSelectionService(
                    resolver.Get<ScenarioCharacterAppearanceService>(),
                    resolver.Get<ScenarioSelectionScopeService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringScrollFocusService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringInputCaptureService(resolver.Get<ScenarioAuthoringScrollFocusService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringCameraGuardService(resolver.Get<ScenarioAuthoringInputCaptureService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringContextMenuService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringMenuService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringCommandService(
                    resolver.Get<ScenarioAuthoringCaptureService>(),
                    resolver.Get<ScenarioSpriteSwapAuthoringService>(),
                    resolver.Get<ScenarioSceneSpritePlacementAuthoringService>(),
                    resolver.Get<ScenarioBuildPlacementAuthoringService>(),
                    resolver.Get<ScenarioGameplayScheduleAuthoringService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringLayoutService>(),
                    resolver.Get<ScenarioStageCoordinator>(),
                    resolver.Get<ScenarioTimelineBuilder>(),
                    resolver.Get<ScenarioTimelineNavigationService>(),
                    resolver.Get<ScenarioSelectionScopeService>());
            });
            services.AddScenarioPresentation();
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringShellImguiRenderModule(resolver.Get<ScenarioSpriteSwapAuthoringService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringImguiRenderModule(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringNguiRenderModule(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringPresentationBuilder(
                    resolver.Get<ScenarioAuthoringCaptureService>(),
                    resolver.Get<ScenarioSpriteSwapAuthoringService>(),
                    resolver.Get<ScenarioSceneSpritePlacementAuthoringService>(),
                    resolver.Get<ScenarioBuildPlacementAuthoringService>(),
                    resolver.Get<ScenarioAuthoringWindowRegistry>(),
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringLayoutService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioSpriteRuntimeResolver>(),
                    resolver.Get<ShellChromeViewModelBuilder>(),
                    resolver.Get<StageNavigationViewModelBuilder>(),
                    resolver.Get<InspectorViewModelBuilder>(),
                    resolver.Get<StatusBarViewModelBuilder>(),
                    resolver.Get<ScenarioTimelineBuilder>(),
                    resolver.Get<ScenarioTimelineViewModelBuilder>(),
                    resolver.Get<ScenarioModDependencyDetector>(),
                    resolver.Get<ScenarioModCompatibilityViewModelBuilder>(),
                    resolver.Get<ScenarioSelectionScopeService>(),
                    resolver.Get<ScenarioTargetClassifier>());
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
                    resolver.Get<IScenarioSceneSpritePlacementEngine>(),
                    resolver.Get<ScenarioObjectIdentityAssignmentService>());
            });
            services.AddSingleton<IScenarioEditorService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioEditorController>(); });
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

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringBackendService(
                    resolver.Get<ScenarioAuthoringSelectionService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringPresentationBuilder>(),
                    resolver.Get<ScenarioAuthoringContextMenuService>(),
                    resolver.Get<ScenarioAuthoringCommandService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>(),
                    resolver.Get<ScenarioBuildPlacementAuthoringService>(),
                    resolver.Get<ScenarioSpriteSwapAuthoringService>(),
                    resolver.Get<ScenarioSceneSpritePlacementAuthoringService>(),
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringLayoutService>(),
                    resolver.Get<ScenarioStageCoordinator>(),
                    resolver.Get<ScenarioSelectionScopeService>());
            });
            services.AddSingleton<IScenarioAuthoringBackend>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioAuthoringBackendService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringPresentationService(
                    resolver.Get<IScenarioAuthoringBackend>(),
                    new IScenarioAuthoringRenderModule[]
                    {
                        resolver.Get<ScenarioAuthoringShellImguiRenderModule>(),
                        resolver.Get<ScenarioAuthoringImguiRenderModule>(),
                        resolver.Get<ScenarioAuthoringNguiRenderModule>()
                    });
            });

            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringBootstrapService(
                    resolver.Get<ScenarioAuthoringBackendService>(),
                    resolver.Get<ScenarioAuthoringDraftRepository>(),
                    resolver.Get<ScenarioAuthoringMenuService>(),
                    resolver.Get<ScenarioAuthoringPresentationService>(),
                    resolver.Get<IScenarioEditorService>());
            });
        }
    }
}
