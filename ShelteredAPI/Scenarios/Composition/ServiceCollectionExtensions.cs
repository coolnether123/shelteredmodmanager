using ModAPI.Scenarios;
using ModAPI.Core;
using ShelteredAPI.Core;
using ShelteredAPI.Scenarios.Application.Conditions;
using ShelteredAPI.Scenarios.Application.Effects;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Application.Scheduling;
using ShelteredAPI.Scenarios.Application.Selection;
using ShelteredAPI.Scenarios.Definitions;
using ShelteredAPI.Scenarios.Diagnostics;
using ShelteredAPI.Scenarios.Infrastructure.Assets;
using ShelteredAPI.Scenarios.Infrastructure.Persistence;
using ShelteredAPI.Scenarios.Infrastructure.Runtime;
using ShelteredAPI.Scenarios.Infrastructure.Serialization;

namespace ShelteredAPI.Scenarios.Composition
{
    internal static class ServiceCollectionExtensions
    {
        public static void AddScenarioDomain(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioDefinitionSerializer(); });
            services.AddSingleton<IScenarioDefinitionSerializer>(delegate(IServiceResolver resolver)
            {
                return resolver.Get<ScenarioDefinitionSerializer>();
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioCatalog(
                    new ModRegistryScenarioModFolderSource(),
                    resolver.Get<ScenarioDefinitionSerializer>(),
                    ModApiPaths.ScenarioPackagesRoot);
            });
            services.AddSingleton<IScenarioDefinitionCatalog>(delegate(IServiceResolver resolver)
            {
                return resolver.Get<ScenarioCatalog>();
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
                    resolver.Get<IScenarioSaveLibrary>());
            });
            services.AddSingleton<IScenarioSelectionCatalogService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioSelectionCatalogService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioLaunchCoordinator(
                    resolver.Get<IScenarioSaveLibrary>(),
                    resolver.Get<IScenarioSelectionCatalogService>(),
                    resolver.Get<ICustomScenarioLifecycleService>(),
                    resolver.Get<IScenarioDefinitionCatalogService>(),
                    resolver.Get<IScenarioWinLossOutcomeService>());
            });
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
                    resolver.Get<ScenarioMapProjectionApplyService>(),
                    resolver.Get<ScenarioActorResolver>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioActorResolver(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new FamilyApplyService(resolver.Get<ScenarioCharacterAppearanceService>(), resolver.Get<ScenarioActorResolver>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new InventoryApplyService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new BunkerApplyService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRuntimeExecutionJournalRepository(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioRuntimeStateService(resolver.Get<ScenarioRuntimeExecutionJournalRepository>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioRuntimeExecutionLog(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioScoreSnapshotService(resolver.Get<ScenarioRuntimeStateService>());
            });
            services.AddSingleton<IScenarioScoreSnapshotService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioScoreSnapshotService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioRuntimeExecutionJournal(resolver.Get<ScenarioRuntimeStateService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioObjectStartStateApplyService(resolver.Get<ScenarioRuntimeStateService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioSceneSpriteStartStateApplyService(resolver.Get<ScenarioRuntimeStateService>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioMapProjectionApplyService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioTriggerRuntimeService(resolver.Get<ScenarioRuntimeStateService>(), resolver.Get<ScenarioRuntimeExecutionLog>());
            });
            services.AddSingleton<IScenarioTriggerRuntimeService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioTriggerRuntimeService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioQuestInstanceResolver(resolver.Get<IVanillaScenarioRuntime>()); });
            services.AddSingleton<IScenarioQuestInstanceResolver>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioQuestInstanceResolver>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioInstalledEndGamePresenter(); });
            services.AddSingleton<IScenarioEndGamePresenter>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioInstalledEndGamePresenter>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledInventoryRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledWeatherRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledJournalRuntimeService(resolver.Get<ScenarioActorResolver>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScheduledWorldEventRuntimeService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioFutureSurvivorRecruitBindingService(resolver.Get<ScenarioActorResolver>()); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScheduledSurvivorRuntimeService(
                    resolver.Get<ScenarioActorResolver>(),
                    resolver.Get<ScenarioFutureSurvivorRecruitBindingService>());
            });
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
                return new ScenarioConversationRuntimeService(
                    resolver.Get<ScenarioActorResolver>(),
                    resolver.Get<ScenarioRuntimeStateService>(),
                    resolver.Get<ScenarioConditionEvaluatorRegistry>(),
                    resolver.Get<ScenarioRuntimeExecutionLog>());
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
                dispatcher.Register(resolver.Get<ScheduledJournalRuntimeService>());
                dispatcher.Register(resolver.Get<ScheduledWorldEventRuntimeService>());
                dispatcher.Register(resolver.Get<ScenarioConversationRuntimeService>());
                return dispatcher;
            });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioWinLossOutcomeService(
                    resolver.Get<IScenarioQuestInstanceResolver>(),
                    resolver.Get<ScenarioConditionEvaluatorRegistry>(),
                    resolver.Get<IVanillaScenarioRuntime>(),
                    resolver.Get<ScenarioRuntimeExecutionLog>(),
                    resolver.Get<IScenarioEndGamePresenter>());
            });
            services.AddSingleton<IScenarioWinLossOutcomeService>(delegate(IServiceResolver resolver) { return resolver.Get<ScenarioWinLossOutcomeService>(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioDefinitionScheduledActionProvider(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioJournalScheduledActionProvider(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioTriggerScheduledActionProvider(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioConversationScheduledActionProvider(); });
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
                        resolver.Get<ScenarioJournalScheduledActionProvider>(),
                        resolver.Get<ScenarioTriggerScheduledActionProvider>(),
                        resolver.Get<ScenarioConversationScheduledActionProvider>()
                    },
                    resolver.Get<ScenarioRuntimeExecutionLog>());
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
                    resolver.Get<IScenarioRuntimeBindingService>(),
                    resolver.Get<ScenarioConversationRuntimeService>());
            });
        }

        public static void AddScenarioInfrastructure(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new SpritePatchValidator(); });
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
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ScenarioSpriteRuntimeResolver(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioCharacterAppearanceService(resolver.Get<IScenarioSpriteAssetResolver>());
            });
        }

        public static void AddScenarioRuntime(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new ScenarioValidatorImpl(resolver.Get<ScenarioValidator>());
            });
            services.AddSingleton<IScenarioDefinitionValidator>(delegate(IServiceResolver resolver)
            {
                return resolver.Get<ScenarioValidatorImpl>();
            });
        }
    }
}
