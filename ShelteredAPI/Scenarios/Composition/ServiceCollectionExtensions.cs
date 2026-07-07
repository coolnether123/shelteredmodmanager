using System.Collections.Generic;
using ModAPI.Scenarios;
using ShelteredAPI.Core;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Bunker;
using ShelteredAPI.Scenarios.Application.Compatibility;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Objects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Application.Stages;
using ShelteredAPI.Scenarios.Application.Timeline;
using ShelteredAPI.Scenarios.Application.Validation;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Diagnostics;
using ShelteredAPI.Scenarios.Domain.Stages;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Persistence;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Presentation.Inspector;
using ShelteredAPI.Scenarios.Presentation.Timeline;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Composition{
    internal static class ServiceCollectionExtensions
    {
        public static void AddScenarioDomain(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioStageRegistry(); });
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
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioValidator(); });
        }

        public static void AddScenarioApplication(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSaveLibrary(); });
            services.AddSingleton<IScenarioSaveLibrary>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioSaveLibrary>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new VanillaScenarioRuntimeGateway(); });
            services.AddSingleton<IVanillaScenarioRuntime>(delegate(IServiceResolver resolver) { return resolver.Get<VanillaScenarioRuntimeGateway>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSelectionCatalogService(
                    resolver.Get<ICustomScenarioRegistry>(),
                    resolver.Get<IScenarioDefinitionCatalogService>(),
                    resolver.Get<IScenarioDependencyVerifier>(),
                    resolver.Get<IScenarioSaveLibrary>(),
                    resolver.Get<IScenarioDefinitionSerializer>());
            });
            services.AddSingleton<IScenarioSelectionCatalogService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioSelectionCatalogService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioLaunchCoordinator(
                    resolver.Get<IScenarioSaveLibrary>(),
                    resolver.Get<IScenarioSelectionCatalogService>(),
                    resolver.Get<ICustomScenarioLifecycleService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioStageCoordinator(resolver.Get<ScenarioStageRegistry>(), new IScenarioStageModule[0]); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new PublishValidationSummaryBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTimelineBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioModDependencyDetector(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioObjectIdentityAssignmentService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioBunkerSupportResolver(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioBunkerGridCaptureService(resolver.Get<ScenarioBunkerSupportResolver>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioDraftMutationService(resolver.Get<IScenarioEditorSessionStore>()); });
            services.AddSingleton<IScenarioDraftMutationService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioDraftMutationService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new StructurePlacementService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ObjectPlacementService(resolver.Get<IScenarioDraftMutationService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new WallWiringEditService(resolver.Get<IScenarioDraftMutationService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioBuildDeletionAuthoringService(resolver.Get<ObjectPlacementService>(), resolver.Get<WallWiringEditService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new PlacementPaletteService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new RoomVisualPaletteService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new PlacementGhostSessionService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpritePatchAuthoringService(resolver.Get<SpritePatchBuilder>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioPngImportService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioApplyCoordinator(
                    resolver.Get<FamilyApplyService>(),
                    resolver.Get<InventoryApplyService>(),
                    resolver.Get<BunkerApplyService>(),
                    resolver.Get<AssetApplyService>(),
                    resolver.Get<TriggerRuntimeAdapter>(),
                    resolver.Get<ScenarioObjectStartStateApplyService>(),
                    resolver.Get<ScenarioSceneSpriteStartStateApplyService>(),
                    resolver.Get<ScenarioActorResolver>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioActorResolver(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new FamilyApplyService(resolver.Get<ScenarioCharacterAppearanceService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new InventoryApplyService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new BunkerApplyService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRuntimeExecutionJournalRepository(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRuntimeStateService(resolver.Get<ScenarioRuntimeExecutionJournalRepository>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioScoreSnapshotService(resolver.Get<ScenarioRuntimeStateService>()); });
            services.AddSingleton<IScenarioScoreSnapshotService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioScoreSnapshotService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRuntimeExecutionJournal(resolver.Get<ScenarioRuntimeStateService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioObjectStartStateApplyService(resolver.Get<ScenarioRuntimeStateService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSceneSpriteStartStateApplyService(resolver.Get<ScenarioRuntimeStateService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTriggerRuntimeService(resolver.Get<ScenarioRuntimeStateService>()); });
            services.AddSingleton<IScenarioTriggerRuntimeService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioTriggerRuntimeService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioQuestInstanceResolver(resolver.Get<IVanillaScenarioRuntime>()); });
            services.AddSingleton<IScenarioQuestInstanceResolver>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioQuestInstanceResolver>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioWinLossConditionAdapter(); });
            services.AddSingleton<IScenarioWinLossConditionAdapter>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioWinLossConditionAdapter>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledInventoryRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledWeatherRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledSurvivorRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledQuestRuntimeService(resolver.Get<IVanillaScenarioRuntime>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledBunkerRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledObjectRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioFlagRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                ScenarioConditionEvaluatorRegistry registry = new ScenarioConditionEvaluatorRegistry();
                registry.Register(resolver.Get<ScheduledInventoryRuntimeService>());
                registry.Register(resolver.Get<ScheduledSurvivorRuntimeService>());
                registry.Register(resolver.Get<ScheduledQuestRuntimeService>());
                registry.Register(resolver.Get<ScheduledBunkerRuntimeService>());
                registry.Register(resolver.Get<ScenarioFlagRuntimeService>());
                registry.Register(resolver.Get<ScenarioTriggerRuntimeService>());
                return registry;
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                ScenarioEffectDispatcher dispatcher = new ScenarioEffectDispatcher();
                dispatcher.Register(resolver.Get<ScheduledInventoryRuntimeService>());
                dispatcher.Register(resolver.Get<ScheduledWeatherRuntimeService>());
                dispatcher.Register(resolver.Get<ScheduledSurvivorRuntimeService>());
                dispatcher.Register(resolver.Get<ScheduledQuestRuntimeService>());
                dispatcher.Register(resolver.Get<ScheduledBunkerRuntimeService>());
                dispatcher.Register(resolver.Get<ScheduledObjectRuntimeService>());
                dispatcher.Register(resolver.Get<ScenarioFlagRuntimeService>());
                dispatcher.Register(resolver.Get<ScenarioTriggerRuntimeService>());
                return dispatcher;
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioWinLossOutcomeService(
                    resolver.Get<IScenarioQuestInstanceResolver>(),
                    resolver.Get<IScenarioWinLossConditionAdapter>(),
                    resolver.Get<ScenarioConditionEvaluatorRegistry>(),
                    resolver.Get<IVanillaScenarioRuntime>());
            });
            services.AddSingleton<IScenarioWinLossOutcomeService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioWinLossOutcomeService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioDefinitionScheduledActionProvider(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTriggerScheduledActionProvider(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioLegacyScheduleActionProvider(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioScheduleRuntimeCoordinator(
                    resolver.Get<ScenarioRuntimeStateService>(),
                    resolver.Get<ScenarioRuntimeExecutionJournal>(),
                    resolver.Get<ScenarioConditionEvaluatorRegistry>(),
                    resolver.Get<ScenarioEffectDispatcher>(),
                    resolver.Get<IScenarioWinLossOutcomeService>(),
                    new IScenarioScheduledActionProvider[]
                    {
                        resolver.Get<ScenarioDefinitionScheduledActionProvider>(),
                        resolver.Get<ScenarioTriggerScheduledActionProvider>(),
                        resolver.Get<ScenarioLegacyScheduleActionProvider>()
                    });
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new AssetApplyService(
                    resolver.Get<IScenarioSpriteSwapEngine>(),
                    resolver.Get<IScenarioSceneSpritePlacementEngine>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new TriggerRuntimeAdapter(
                    resolver.Get<ScenarioScheduleRuntimeCoordinator>(),
                    resolver.Get<IScenarioRuntimeBindingService>());
            });
        }

        public static void AddScenarioPresentation(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ShellChromeViewModelBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTimelineViewModelBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioModCompatibilityViewModelBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new StageNavigationViewModelBuilder(
                    resolver.Get<ScenarioStageRegistry>(),
                    resolver.Get<ScenarioStageCoordinator>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new InspectorViewModelBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new StatusBarViewModelBuilder(resolver.Get<ScenarioSelectionScopeService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTimelineNavigationService(resolver.Get<ScenarioAuthoringLayoutService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioAssetAuthoringContentBuilder(
                    resolver.Get<IScenarioAuthoringSectionHub>(),
                    resolver.Get<ScenarioSelectionScopeService>(),
                    resolver.Get<ScenarioSpriteRuntimeResolver>(),
                    resolver.Get<ScenarioWeatherEffectSpriteCatalogService>());
            });
        }

        public static void AddScenarioInfrastructure(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new SpritePatchValidator(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new SpritePatchBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new SpritePatchRuntimeRenderer(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioWeatherEffectSpriteCatalogService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new SpritePatchApplyService(
                    resolver.Get<SpritePatchValidator>(),
                    resolver.Get<SpritePatchRuntimeRenderer>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSpriteAssetResolver(resolver.Get<SpritePatchApplyService>());
            });
            services.AddSingleton<IScenarioSpriteAssetResolver>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioSpriteAssetResolver>(); });
        }

        public static void AddScenarioRuntime(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioValidatorImpl(resolver.Get<ScenarioValidator>());
            });
            services.AddSingleton<IScenarioDefinitionValidator>(delegate(IServiceResolver resolver)
            {
                return new ScenarioDefinitionValidatorAdapter(resolver.Get<ScenarioValidatorImpl>());
            });
        }
    }
}
