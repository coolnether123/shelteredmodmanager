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
                    resolver.Get<TriggerRuntimeAdapter>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new FamilyApplyService(resolver.Get<ScenarioCharacterAppearanceService>()); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new InventoryApplyService(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new BunkerApplyService(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new AssetApplyService(
                    resolver.Get<IScenarioSpriteSwapEngine>(),
                    resolver.Get<IScenarioSceneSpritePlacementEngine>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new TriggerRuntimeAdapter(); });
        }

        public static void AddScenarioPresentation(this ServiceCollection services)
        {
            services.AddSingleton(delegate(IServiceResolver resolver) { return new ShellChromeViewModelBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver)
            {
                return new StageNavigationViewModelBuilder(
                    resolver.Get<ScenarioStageRegistry>(),
                    resolver.Get<ScenarioStageCoordinator>());
            });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new InspectorViewModelBuilder(); });
            services.AddSingleton(delegate(IServiceResolver resolver) { return new StatusBarViewModelBuilder(); });
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
