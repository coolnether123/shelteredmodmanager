using ShelteredScenarioEditor.Core;

using ShelteredScenarioEditor.Application.Assets;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Application.Authoring.Tutorial;
using ShelteredScenarioEditor.Application.Commands;
using ShelteredScenarioEditor.Application.Bunker;
using ShelteredScenarioEditor.Application.Compatibility;
using ShelteredScenarioEditor.Application.Map;
using ShelteredScenarioEditor.Application.Objects;
using ShelteredScenarioEditor.Application.Runtime;
using ShelteredScenarioEditor.Application.Selection;
using ShelteredScenarioEditor.Application.Stages;
using ShelteredScenarioEditor.Application.Timeline;
using ShelteredScenarioEditor.Domain.Stages;
using ShelteredScenarioEditor.Infrastructure.Assets;
using ShelteredScenarioEditor.Infrastructure.Persistence;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Presentation.Authoring.Shell;
using ShelteredScenarioEditor.Presentation.Authoring.Windows;
namespace ShelteredScenarioEditor.Composition{
    internal static class ScenarioAuthoringModule
    {
        public static void AddScenarioAuthoringModule(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioPreviewSessionHost(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioHoverVisualService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioEditorActorReferenceService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioObjectIdentityAssignmentService(); });
            services.AddSingleton<IScenarioDefinitionSerializer>(delegate(IServiceResolver resolver) { return new ScenarioEditorDefinitionSerializer(); });
            services.AddSingleton<IScenarioDefinitionValidator>(delegate(IServiceResolver resolver) { return new ScenarioEditorDefinitionValidator(); });
            services.AddSingleton<IScenarioDefinitionCatalogService>(delegate(IServiceResolver resolver) { return new ScenarioEditorDefinitionCatalogService(); });
            services.AddSingleton<IScenarioEditorSessionStore>(delegate(IServiceResolver resolver) { return new ScenarioEditorSessionStore(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringRendererInteractionState(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioStageRegistry(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioStageCoordinator(
                    resolver.Get<ScenarioStageRegistry>(),
                    new IScenarioStageModule[0]);
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTimelineBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioModDependencyDetector(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringHistoryService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioDraftMutationService(resolver.Get<IScenarioEditorSessionStore>());
            });
            services.AddSingleton<IScenarioDraftMutationService>(delegate(IServiceResolver resolver)
            {
                return resolver.Get<ScenarioDraftMutationService>();
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new StructurePlacementService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ObjectPlacementService(
                    resolver.Get<IScenarioDraftMutationService>(),
                    resolver.Get<ScenarioPreviewSessionHost>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new WallWiringEditService(resolver.Get<IScenarioDraftMutationService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioBuildDeletionAuthoringService(
                    resolver.Get<ObjectPlacementService>(),
                    resolver.Get<WallWiringEditService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new PlacementPaletteService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new RoomVisualPaletteService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new PlacementGhostSessionService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new SpritePatchBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpritePatchAuthoringService(resolver.Get<SpritePatchBuilder>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioPngImportService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAssetInventoryService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAssetInventoryMutationService(resolver.Get<ScenarioAuthoringHistoryService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringInventoryProjectionService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringCaptureService(
                    resolver.Get<IScenarioDraftMutationService>(),
                    resolver.Get<ScenarioEditorActorReferenceService>(),
                    resolver.Get<ScenarioObjectIdentityAssignmentService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>(),
                    resolver.Get<ScenarioPreviewSessionHost>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioGameplayScheduleAuthoringService(
                    resolver.Get<ScenarioEditorActorReferenceService>(),
                    resolver.Get<ScenarioAuthoringInventoryProjectionService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioStoryAuthoringService(
                    resolver.Get<ScenarioPreviewSessionHost>(),
                    resolver.Get<ScenarioAuthoringHistoryService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioEventAuthoringService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioEditorSpriteRuntimeResolver(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioEditorSpriteAssetService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteAnimationMetadataService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpritePlacementPolicy(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSceneSpritePlacementCatalogService(
                    resolver.Get<ScenarioEditorSpriteAssetService>(),
                    resolver.Get<ScenarioSpritePlacementPolicy>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpriteCatalogService(
                    resolver.Get<ScenarioEditorSpriteRuntimeResolver>(),
                    resolver.Get<ScenarioEditorSpriteAssetService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioWeatherEffectSpriteCatalogService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringDraftRepository(
                    resolver.Get<ScenarioAuthoringSidecarStore>(),
                    resolver.Get<IScenarioDefinitionCatalogService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringPauseService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringStatusPort(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioOpeningCutsceneAuthoringService(
                    resolver.Get<ScenarioAuthoringPauseService>(),
                    resolver.Get<ScenarioAuthoringStatusPort>());
            });
            services.AddSingleton<IScenarioPauseService>(delegate(IServiceResolver resolver)
            {
                return resolver.Get<ScenarioAuthoringPauseService>();
            });
            services.AddSingleton<IScenarioPlaytestUiService>(delegate(IServiceResolver resolver)
            {
                return new ScenarioPlaytestVanillaUiService();
            });
            services.AddSingleton<IScenarioPlaytestOrchestrator>(delegate(IServiceResolver resolver)
            {
                return new ScenarioPlaytestOrchestrator(
                    resolver.Get<IScenarioPauseService>(),
                    resolver.Get<ScenarioAuthorTestChecklistService>(),
                    resolver.Get<IScenarioPlaytestUiService>(),
                    resolver.Get<ScenarioPreviewSessionHost>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTestTimeAdvanceService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioTestConsoleService(
                    resolver.Get<ScenarioTestTimeAdvanceService>(),
                    resolver.Get<ScenarioPreviewSessionHost>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioEditorCharacterAppearanceService();
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioEditorSceneAssetPreviewService(resolver.Get<ScenarioPreviewSessionHost>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioCharacterEditorAuthoringService(
                    resolver.Get<ScenarioEditorCharacterAppearanceService>(),
                    resolver.Get<ScenarioEditorActorReferenceService>(),
                    resolver.Get<ScenarioAuthoringRendererInteractionState>(),
                    resolver.Get<ScenarioAuthoringHistoryService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSceneSpritePlacementAuthoringService(
                    resolver.Get<ScenarioSceneSpritePlacementCatalogService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>(),
                    resolver.Get<ScenarioEditorSceneAssetPreviewService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioObjectIdentityAssignmentService>(),
                    resolver.Get<IScenarioEditorSessionStore>(),
                    resolver.Get<ScenarioAuthoringInputCaptureService>(),
                    resolver.Get<ScenarioAuthoringEditorCameraService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpriteSwapAuthoringService(
                    resolver.Get<ScenarioSpriteCatalogService>(),
                    resolver.Get<ScenarioEditorCharacterAppearanceService>(),
                    resolver.Get<ScenarioEditorSpriteRuntimeResolver>(),
                    resolver.Get<ScenarioSpriteAnimationMetadataService>(),
                    resolver.Get<ScenarioSpritePatchAuthoringService>(),
                    resolver.Get<ScenarioPngImportService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>(),
                    resolver.Get<ScenarioEditorSceneAssetPreviewService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<IScenarioEditorSessionStore>(),
                    resolver.Get<ScenarioAuthoringEditorCameraService>(),
                    resolver.Get<ScenarioPreviewSessionHost>(),
                    resolver.Get<ScenarioHoverVisualService>());
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
                    resolver.Get<ScenarioBuildDeletionAuthoringService>(),
                    resolver.Get<ScenarioAuthoringInputCaptureService>(),
                    resolver.Get<ScenarioAuthoringEditorCameraService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringHistoryService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioMapDraftService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioMapAuthoringRuntimeService(
                    resolver.Get<ScenarioMapDraftService>(),
                    resolver.Get<ScenarioPreviewSessionHost>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioStorageAuthoringRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioVanillaInteractionRuntimeService(); });
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
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringSidecarStore(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioEditorStateSessionService(
                    resolver.Get<IScenarioEditorSessionStore>(),
                    resolver.Get<ScenarioAuthoringSidecarStore>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioDraftSnapshotService(
                    resolver.Get<IScenarioEditorSessionStore>(),
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<ScenarioAuthoringSidecarStore>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthorTestChecklistService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioPackageInstaller(
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionCatalogService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringTutorialService(
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioEditorStateSessionService>(),
                    resolver.Get<IScenarioEditorSessionStore>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioPublishExportService(
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionValidator>(),
                    resolver.Get<ScenarioPackageInstaller>(),
                    resolver.Get<ScenarioAuthorTestChecklistService>(),
                    resolver.Get<IScenarioEditorSessionStore>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTargetClassifier(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringUiDebugService(resolver.Get<ScenarioTargetClassifier>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioBackdropTargetCatalogService(resolver.Get<ScenarioAuthoringSelectionService>());
            });
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
                    resolver.Get<ScenarioEditorCharacterAppearanceService>(),
                    resolver.Get<ScenarioSelectionScopeService>(),
                    resolver.Get<ScenarioAuthoringInputCaptureService>(),
                    resolver.Get<ScenarioAuthoringEditorCameraService>(),
                    resolver.Get<ScenarioVanillaInteractionRuntimeService>(),
                    resolver.Get<ScenarioBuildPlacementAuthoringService>(),
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringSelectionMenuService>(),
                    resolver.Get<ScenarioHoverVisualService>());
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
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringSelectionMenuService(
                    resolver.Get<ScenarioAuthoringContextMenuService>(),
                    resolver.Get<ScenarioSelectionScopeService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioAuthoringMenuService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringSessionLifecycleService(
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringDraftRepository>(),
                    resolver.Get<ScenarioAuthoringInventoryProjectionService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringBaseModeReloadService(
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<ScenarioAuthoringDraftRepository>(),
                    resolver.Get<ScenarioAuthoringCaptureService>(),
                    resolver.Get<ScenarioAuthoringSessionLifecycleService>(),
                    resolver.Get<ScenarioPreviewSessionHost>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAuthoringEntryFlowService(
                    resolver.Get<IScenarioEditorService>(),
                    resolver.Get<IScenarioDefinitionCatalogService>(),
                    resolver.Get<ScenarioAuthoringBaseModeReloadService>(),
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringSessionLifecycleService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                IScenarioAuthoringSectionHub sections = resolver.Get<IScenarioAuthoringSectionHub>();
                IScenarioEditorService editor = resolver.Get<IScenarioEditorService>();
                ScenarioAuthoringLayoutService layout = resolver.Get<ScenarioAuthoringLayoutService>();
                ScenarioSelectionScopeService selectionScope = resolver.Get<ScenarioSelectionScopeService>();
                ScenarioAuthoringRendererInteractionState rendererInteraction = resolver.Get<ScenarioAuthoringRendererInteractionState>();
                ScenarioAuthoringHistoryService history = resolver.Get<ScenarioAuthoringHistoryService>();
                IScenarioEditorSessionStore sessions = resolver.Get<IScenarioEditorSessionStore>();
                ScenarioDraftSnapshotService snapshots = resolver.Get<ScenarioDraftSnapshotService>();
                ScenarioVanillaInteractionRuntimeService vanillaInteraction = resolver.Get<ScenarioVanillaInteractionRuntimeService>();
                ScenarioCommandDispatcher dispatcher = new ScenarioCommandDispatcher();

                dispatcher.Register(new TypedRendererInteractionCommandHandler(
                    rendererInteraction,
                    resolver.Get<ScenarioAuthoringSettingsService>()));
                dispatcher.Register(new ScenarioMapAuthoringCommandHandler(
                    resolver.Get<ScenarioMapAuthoringRuntimeService>(), vanillaInteraction,
                    resolver.Get<ScenarioMapDraftService>(), layout, editor, history, rendererInteraction));
                dispatcher.Register(new ScenarioStoryFocusedEditorCommandHandler(editor, layout, rendererInteraction));
                dispatcher.Register(new TypedStoryAuthoringCommandHandler(
                    resolver.Get<ScenarioStoryAuthoringService>(), editor, rendererInteraction));
                dispatcher.Register(new TypedGameplayScheduleCommandHandler(
                    sections.GameplaySchedule, editor, rendererInteraction));
                dispatcher.Register(new ShellUxCommandHandler(
                    layout,
                    resolver.Get<ScenarioAuthoringSettingsService>(),
                    resolver.Get<ScenarioAuthoringTutorialService>(),
                    resolver.Get<ScenarioEditorStateSessionService>(),
                    editor,
                    snapshots,
                    resolver.Get<ScenarioAuthorTestChecklistService>(),
                    vanillaInteraction,
                    history));
                dispatcher.Register(new ScenarioStorageAuthoringCommandHandler(
                    resolver.Get<ScenarioStorageAuthoringRuntimeService>(), layout, vanillaInteraction));
                dispatcher.Register(new ScenarioWinLossCommandHandler(editor));
                dispatcher.Register(new ScenarioTestConsoleCommandHandler(editor, resolver.Get<ScenarioTestConsoleService>()));
                dispatcher.Register(new ScenarioPublishCommandHandler(resolver.Get<ScenarioPublishExportService>(), sessions));
                dispatcher.Register(new ScenarioDraftHistoryCommandHandler(snapshots));
                dispatcher.Register(new PlacementOverlayCommandHandler(sections.BuildPlacement, sections.SceneSpritePlacement, layout));
                dispatcher.Register(new AssetBrowserCommandHandler(
                    sections.BuildPlacement, sections.SceneSpritePlacement, sections.SpriteSwap, layout,
                    resolver.Get<ScenarioWeatherEffectSpriteCatalogService>(), editor,
                    resolver.Get<ScenarioAssetInventoryMutationService>(), sessions, selectionScope,
                    resolver.Get<ScenarioAuthoringSettingsService>()));
                dispatcher.Register(new SpriteCommandHandler(sections.SpriteSwap, selectionScope, layout, sections.BuildPlacement));
                dispatcher.Register(new SceneSpriteCommandHandler(sections.SceneSpritePlacement, sections.BuildPlacement, selectionScope));
                dispatcher.Register(new BuildCommandHandler(sections.BuildPlacement, sections.SceneSpritePlacement));
                dispatcher.Register(new EditHistoryCommandHandler(sections.SpriteSwap));
                dispatcher.Register(new SelectionCommandHandler(
                    resolver.Get<ScenarioWeatherEffectSpriteCatalogService>(),
                    resolver.Get<ScenarioAuthoringSelectionService>()));
                dispatcher.Register(new CharacterEditorCommandHandler(
                    resolver.Get<ScenarioCharacterEditorAuthoringService>(), editor));
                dispatcher.Register(new TimelineNavigationCommandHandler(
                    editor, resolver.Get<ScenarioTimelineBuilder>(), resolver.Get<ScenarioTimelineNavigationService>()));
                dispatcher.Register(new CaptureAuthoringCommandHandler(
                    resolver.Get<ScenarioAuthoringCaptureService>(), editor, selectionScope));
                dispatcher.Register(new StationUpgradeCommandHandler(
                    editor, selectionScope, resolver.Get<ScenarioObjectIdentityAssignmentService>(), history,
                    resolver.Get<ScenarioPreviewSessionHost>()));
                dispatcher.Register(new EditorLifecycleCommandHandler(
                    editor,
                    sections.BuildPlacement,
                    sections.SceneSpritePlacement,
                    resolver.Get<ScenarioAuthoringBaseModeReloadService>(),
                    resolver.Get<ScenarioOpeningCutsceneAuthoringService>(),
                    resolver.Get<ScenarioAuthoringPauseService>(),
                    resolver.Get<ScenarioAuthoringSessionLifecycleService>(),
                    sessions));
                dispatcher.Register(new ToolCommandHandler(layout, sections.BuildPlacement));
                dispatcher.Register(new EventAuthoringCommandHandler(resolver.Get<ScenarioEventAuthoringService>(), editor));

                return new ScenarioAuthoringCommandService(dispatcher, snapshots);
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioEditorController(
                    resolver.Get<IScenarioEditorSessionStore>(),
                    resolver.Get<IScenarioDefinitionSerializer>(),
                    resolver.Get<IScenarioDefinitionValidator>(),
                    resolver.Get<IScenarioPlaytestOrchestrator>(),
                    resolver.Get<IScenarioPauseService>(),
                    resolver.Get<ScenarioObjectIdentityAssignmentService>(),
                    resolver.Get<ScenarioEditorActorReferenceService>(),
                    resolver.Get<ScenarioDraftSnapshotService>(),
                    resolver.Get<ScenarioAuthoringSidecarStore>(),
                    resolver.Get<ScenarioPreviewSessionHost>());
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
                    resolver.Get<ScenarioAuthoringInputCaptureService>(),
                    resolver.Get<ScenarioDraftSnapshotService>(),
                    resolver.Get<ScenarioAuthoringSessionLifecycleService>(),
                    resolver.Get<ScenarioAuthoringRendererInteractionState>(),
                    resolver.Get<ScenarioMapAuthoringRuntimeService>(),
                    resolver.Get<ScenarioStorageAuthoringRuntimeService>(),
                    resolver.Get<ScenarioVanillaInteractionRuntimeService>(),
                    resolver.Get<ScenarioAuthoringSelectionMenuService>(),
                    resolver.Get<ScenarioAuthoringUiDebugService>(),
                    resolver.Get<ScenarioAuthoringStatusPort>(),
                    resolver.Get<ScenarioHoverVisualService>());
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
                    resolver.Get<ScenarioAuthoringCaptureService>(),
                    resolver.Get<ScenarioAuthoringInventoryProjectionService>(),
                    resolver.Get<ScenarioAuthoringEntryFlowService>(),
                    resolver.Get<ScenarioAuthoringBaseModeReloadService>(),
                    resolver.Get<ScenarioAuthoringSessionLifecycleService>(),
                    resolver.Get<ScenarioPreviewSessionHost>(),
                    resolver.Get<ScenarioAuthoringPauseService>());
            });
        }
    }
}
