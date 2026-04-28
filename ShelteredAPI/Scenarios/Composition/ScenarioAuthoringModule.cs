using ShelteredAPI.Core;

namespace ShelteredAPI.Scenarios
{
    internal static class ScenarioAuthoringModule
    {
        public static void AddScenarioAuthoringModule(this ServiceCollection services)
        {
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
                    resolver.Get<PlacementGhostSessionService>());
            });
            services.AddSingleton<IScenarioAuthoringSectionHub>(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringSectionHub(
                    resolver.Get<ScenarioSpriteSwapAuthoringService>(),
                    resolver.Get<ScenarioSceneSpritePlacementAuthoringService>(),
                    resolver.Get<ScenarioBuildPlacementAuthoringService>(),
                    resolver.Get<ScenarioGameplayScheduleAuthoringService>());
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
                    resolver.Get<IScenarioAuthoringSectionHub>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringLayoutService>(),
                    resolver.Get<ScenarioStageCoordinator>(),
                    resolver.Get<ScenarioTimelineBuilder>(),
                    resolver.Get<ScenarioTimelineNavigationService>(),
                    resolver.Get<ScenarioSelectionScopeService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioEditorController(
                    resolver.Get<IScenarioEditorSessionStore>(),
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
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringBackendService(
                    resolver.Get<ScenarioAuthoringSelectionService>(),
                    resolver.Get<IScenarioEditorSessionStore>(),
                    resolver.Get<ScenarioAuthoringPresentationBuilder>(),
                    resolver.Get<ScenarioAuthoringContextMenuService>(),
                    resolver.Get<ScenarioAuthoringCommandService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>(),
                    resolver.Get<IScenarioAuthoringSectionHub>(),
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringLayoutService>(),
                    resolver.Get<ScenarioStageCoordinator>(),
                    resolver.Get<ScenarioSelectionScopeService>());
            });
            services.AddSingleton<IScenarioAuthoringBackend>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioAuthoringBackendService>(); });
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
