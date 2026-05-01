using ModAPI.Core;
using ShelteredAPI.Content;

namespace ShelteredAPI.Core
{
    /// <summary>
    /// Legacy ShelteredAPI registry aliases. Prefer GameRuntimeApiIds for new code.
    /// </summary>
    public static class ShelteredApiAliasIds
    {
        public const string GameHelper = "ShelteredAPI.GameHelper";
        public const string ContentResolution = "ShelteredAPI.ContentResolution";
        public const string ShelteredContent = "ShelteredAPI.Content";
        public const string ShelteredContentRegistration = "ShelteredAPI.ContentRegistration";
        public const string ShelteredInventory = "ShelteredAPI.Inventory";
        public const string ShelteredAssetLoading = "ShelteredAPI.AssetLoading";
        public const string ShelteredLocalization = "ShelteredAPI.Localization";
        public const string ShelteredRecipeLootMutation = "ShelteredAPI.RecipeLootMutation";
        public const string GameLifecycle = "ShelteredAPI.GameLifecycle";
        public const string SaveRuntime = "ShelteredAPI.SaveRuntime";
        public const string UiLifecycleEvents = "ShelteredAPI.UiLifecycleEvents";
        public const string Actors = "ShelteredAPI.Actors";
        public const string ActorRegistry = "ShelteredAPI.ActorRegistry";
        public const string ActorComponents = "ShelteredAPI.ActorComponents";
        public const string ActorBindings = "ShelteredAPI.ActorBindings";
        public const string ActorAdapters = "ShelteredAPI.ActorAdapters";
        public const string ActorDiagnostics = "ShelteredAPI.ActorDiagnostics";
        public const string ActorSimulation = "ShelteredAPI.ActorSimulation";
        public const string ActorEvents = "ShelteredAPI.ActorEvents";
        public const string ActorSerialization = "ShelteredAPI.ActorSerialization";
        public const string CustomScenarios = "ShelteredAPI.CustomScenarios";
        public const string ScenarioAuthoring = "ShelteredAPI.ScenarioAuthoring";
    }

    /// <summary>
    /// Sheltered-specific typed accessors over the compatibility registry.
    /// </summary>
    public static class ShelteredApiServices
    {
        public static bool TryGetContent(out IShelteredContentService service)
        {
            if (GameRuntimeApis.TryGet(GameRuntimeApiIds.ShelteredContent, out service))
                return true;

            return ModAPIRegistry.TryGetAPI(ShelteredApiAliasIds.ShelteredContent, out service);
        }

        public static bool TryGetContentRegistration(out IShelteredContentRegistrationService service)
        {
            if (GameRuntimeApis.TryGet(GameRuntimeApiIds.ShelteredContentRegistration, out service))
                return true;

            return ModAPIRegistry.TryGetAPI(ShelteredApiAliasIds.ShelteredContentRegistration, out service);
        }

        public static bool TryGetInventory(out IShelteredInventoryService service)
        {
            if (GameRuntimeApis.TryGet(GameRuntimeApiIds.ShelteredInventory, out service))
                return true;

            return ModAPIRegistry.TryGetAPI(ShelteredApiAliasIds.ShelteredInventory, out service);
        }

        public static bool TryGetAssets(out IShelteredAssetLoadingService service)
        {
            if (GameRuntimeApis.TryGet(GameRuntimeApiIds.ShelteredAssetLoading, out service))
                return true;

            return ModAPIRegistry.TryGetAPI(ShelteredApiAliasIds.ShelteredAssetLoading, out service);
        }

        public static bool TryGetLocalization(out IShelteredLocalizationService service)
        {
            if (GameRuntimeApis.TryGet(GameRuntimeApiIds.ShelteredLocalization, out service))
                return true;

            return ModAPIRegistry.TryGetAPI(ShelteredApiAliasIds.ShelteredLocalization, out service);
        }

        public static bool TryGetRecipeLootMutation(out IShelteredRecipeLootMutationService service)
        {
            if (GameRuntimeApis.TryGet(GameRuntimeApiIds.ShelteredRecipeLootMutation, out service))
                return true;

            return ModAPIRegistry.TryGetAPI(ShelteredApiAliasIds.ShelteredRecipeLootMutation, out service);
        }
    }
}
