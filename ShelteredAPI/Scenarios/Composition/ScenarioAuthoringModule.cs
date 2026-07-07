using ShelteredAPI.Core;

using ShelteredAPI.Scenarios.Application.Assets;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Authoring.Tutorial;
using ShelteredAPI.Scenarios.Application.Map;
using ShelteredAPI.Scenarios.Application.Objects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Application.Stages;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Authoring.Windows;
namespace ShelteredAPI.Scenarios.Composition{
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
                return new ScenarioCharacterEditorAuthoringService(
                    resolver.Get<ScenarioCharacterAppearanceService>(),
                    resolver.Get<ScenarioActorResolver>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSceneSpritePlacementAuthoringService(
                    resolver.Get<ScenarioSceneSpritePlacementCatalogService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>(),
                    resolver.Get<IScenarioSceneSpritePlacementEngine>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioObjectIdentityAssignmentService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpriteSwapAuthoringService(
                    resolver.Get<ScenarioSpriteCatalogService>(),
                    resolver.Get<ScenarioCharacterAppearanceService>(),
                    resolver.Get<ScenarioSpriteRuntimeResolver>(),
                    resolver.Get<ScenarioSpritePatchAuthoringService>(),
                    resolver.Get<ScenarioPngImportService>(),
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
                    resolver.Get<RoomVisualPaletteService>(),
                    resolver.Get<PlacementGhostSessionService>(),
                    resolver.Get<ScenarioBuildDeletionAuthoringService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioMapDraftService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioMapAuthoringRuntimeService(
                    resolver.Get<ScenarioMapDraftService>());
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
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringSetupStateService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringTutorialService(
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringSetupStateService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioPublishExportService(
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionValidator>(),
                    resolver.Get<IScenarioDefinitionCatalogService>());
            });
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
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringVanillaPanelVisibilityService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringCameraGuardService(resolver.Get<ScenarioAuthoringInputCaptureService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringEditorCameraService(
                    resolver.Get<ScenarioAuthoringInputCaptureService>(),
                    resolver.Get<ScenarioAuthoringVanillaPanelVisibilityService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringContextMenuService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringMenuService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringBaseModeReloadService(
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringDraftRepository>(),
                    resolver.Get<ScenarioLaunchCoordinator>(),
                    resolver.Get<ScenarioAuthoringCaptureService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringEntryFlowService(
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<IScenarioSelectionCatalogService>(),
                    resolver.Get<IScenarioDefinitionCatalogService>(),
                    resolver.Get<ScenarioAuthoringBaseModeReloadService>(),
                    resolver.Get<ScenarioAuthoringSettingsService>());
            });
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
                    resolver.Get<ScenarioSelectionScopeService>(),
                    resolver.Get<ScenarioCharacterEditorAuthoringService>(),
                    resolver.Get<ScenarioStoryAuthoringService>(),
                    resolver.Get<ScenarioEventAuthoringService>(),
                    resolver.Get<ScenarioPublishExportService>(),
                    resolver.Get<ScenarioAuthoringBaseModeReloadService>(),
                    resolver.Get<ScenarioAuthoringTutorialService>(),
                    resolver.Get<ScenarioAuthoringSetupStateService>(),
                    resolver.Get<ScenarioWeatherEffectSpriteCatalogService>(),
                    resolver.Get<ScenarioMapAuthoringRuntimeService>(),
                    resolver.Get<ScenarioMapDraftService>());
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
                    resolver.Get<ScenarioObjectIdentityAssignmentService>(),
                    resolver.Get<ScenarioActorResolver>());
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
                    resolver.Get<ScenarioSelectionScopeService>(),
                    resolver.Get<ScenarioAuthoringTutorialService>(),
                    resolver.Get<ScenarioAuthoringSetupStateService>(),
                    resolver.Get<ScenarioAuthoringInputCaptureService>());
            });
            services.AddSingleton<IScenarioAuthoringBackend>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioAuthoringBackendService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringBootstrapService(
                    resolver.Get<ScenarioAuthoringBackendService>(),
                    resolver.Get<ScenarioAuthoringDraftRepository>(),
                    resolver.Get<ScenarioAuthoringMenuService>(),
                    resolver.Get<ScenarioAuthoringPresentationService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<IScenarioSaveLibrary>(),
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<ScenarioAuthoringCaptureService>(),
                    resolver.Get<ScenarioAuthoringInventoryProjectionService>(),
                    resolver.Get<ScenarioAuthoringEntryFlowService>());
            });
        }
    }
}
