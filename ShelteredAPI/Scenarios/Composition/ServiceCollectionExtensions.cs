using System.Collections.Generic;
using ModAPI.Scenarios;
using ShelteredAPI.Core;

namespace ShelteredAPI.Scenarios
{
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
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioStageCoordinator(resolver.Get<ScenarioStageRegistry>(), new IScenarioStageModule[0]); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new PublishValidationSummaryBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTimelineBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioScheduleTimelineBuilder(resolver.Get<ScenarioTimelineBuilder>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioModDependencyDetector(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioObjectIdentityAssignmentService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioBunkerSupportResolver(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioBunkerGridCaptureService(resolver.Get<ScenarioBunkerSupportResolver>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new StructurePlacementService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ObjectPlacementService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new WallWiringEditService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new PlacementPaletteService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new PlacementGhostSessionService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioApplyCoordinator(
                    resolver.Get<FamilyApplyService>(),
                    resolver.Get<InventoryApplyService>(),
                    resolver.Get<BunkerApplyService>(),
                    resolver.Get<AssetApplyService>(),
                    resolver.Get<TriggerRuntimeAdapter>(),
                    resolver.Get<ScenarioObjectStartStateApplyService>(),
                    resolver.Get<ScenarioSceneSpriteStartStateApplyService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new FamilyApplyService(resolver.Get<ScenarioCharacterAppearanceService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new InventoryApplyService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new BunkerApplyService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRuntimeExecutionJournalRepository(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRuntimeStateService(resolver.Get<ScenarioRuntimeExecutionJournalRepository>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRuntimeExecutionJournal(resolver.Get<ScenarioRuntimeStateService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioObjectStartStateApplyService(resolver.Get<ScenarioRuntimeStateService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSceneSpriteStartStateApplyService(resolver.Get<ScenarioRuntimeStateService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledInventoryRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledWeatherRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledSurvivorRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledQuestRuntimeService(); });
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
                return dispatcher;
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioDefinitionScheduledActionProvider(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioLegacyScheduleActionProvider(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioScheduleRuntimeCoordinator(
                    resolver.Get<ScenarioRuntimeStateService>(),
                    resolver.Get<ScenarioRuntimeExecutionJournal>(),
                    resolver.Get<ScenarioConditionEvaluatorRegistry>(),
                    resolver.Get<ScenarioEffectDispatcher>(),
                    new IScenarioScheduledActionProvider[]
                    {
                        resolver.Get<ScenarioDefinitionScheduledActionProvider>(),
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
        }

        public static void AddScenarioInfrastructure(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new SpritePatchValidator(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new SpritePatchBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new SpritePatchRuntimeRenderer(); });
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
