namespace ModAPI.Core
{
    /// <summary>
    /// Host-neutral helpers for common game-state reads that mod authors need often.
    /// Game-specific assemblies provide the implementation; callers should treat returned host objects as opaque unless using that game's typed adapter.
    /// </summary>
    public interface IGameHelper
    {
        /// <summary>
        /// Returns the total owned count for a mod-facing item ID across all shelter storage managers.
        /// Use this when gameplay logic cares about ownership, not which manager currently stores the item.
        /// </summary>
        int GetTotalOwned(string itemId);

        /// <summary>
        /// Returns the item count from the primary item inventory only.
        /// Use this when storage category matters and food, water, or entertainment managers should not be included.
        /// </summary>
        int GetInventoryCount(string itemId);

        /// <summary>
        /// Try to find a game-owned character by its mod-facing ID.
        /// The returned handle is opaque to ModAPI; game-specific APIs own typed adapters.
        /// </summary>
        object FindCharacter(string characterId);
    }

    /// <summary>
    /// Well-known <see cref="ModAPIRegistry"/> IDs used by game runtime assemblies.
    /// Prefer <see cref="GameRuntimeApis"/> when consuming these services from mod code.
    /// </summary>
    public static class GameRuntimeApiIds
    {
        /// <summary>General game-state helper service.</summary>
        public const string GameHelper = "GameRuntime.GameHelper";
        /// <summary>Aggregate actor system service.</summary>
        public const string Actors = "GameRuntime.Actors";
        /// <summary>Actor registry service.</summary>
        public const string ActorRegistry = "GameRuntime.ActorRegistry";
        /// <summary>Actor component store service.</summary>
        public const string ActorComponents = "GameRuntime.ActorComponents";
        /// <summary>Actor binding store service.</summary>
        public const string ActorBindings = "GameRuntime.ActorBindings";
        /// <summary>Actor live-sync adapter registry service.</summary>
        public const string ActorAdapters = "GameRuntime.ActorAdapters";
        /// <summary>Actor diagnostics service.</summary>
        public const string ActorDiagnostics = "GameRuntime.ActorDiagnostics";
        /// <summary>Actor simulation scheduler service.</summary>
        public const string ActorSimulation = "GameRuntime.ActorSimulation";
        /// <summary>Actor event stream service.</summary>
        public const string ActorEvents = "GameRuntime.ActorEvents";
        /// <summary>Actor serialization service.</summary>
        public const string ActorSerialization = "GameRuntime.ActorSerialization";
        /// <summary>Scenario actor-authoring capability registry.</summary>
        public const string ActorAuthoringCapabilities = "GameRuntime.ActorAuthoringCapabilities";
        /// <summary>Content ID resolution service.</summary>
        public const string ContentResolution = "GameRuntime.ContentResolution";
        /// <summary>Game lifecycle event source.</summary>
        public const string GameLifecycle = "GameRuntime.GameLifecycle";
        /// <summary>UI lifecycle event sink.</summary>
        public const string UiLifecycleEvents = "GameRuntime.UiLifecycleEvents";
        /// <summary>Save runtime adapter.</summary>
        public const string SaveRuntime = "GameRuntime.SaveRuntime";
        /// <summary>Sheltered aggregate content service.</summary>
        public const string ShelteredContent = "GameRuntime.ShelteredContent";
        /// <summary>Sheltered content registration service.</summary>
        public const string ShelteredContentRegistration = "GameRuntime.ShelteredContentRegistration";
        /// <summary>Sheltered inventory service.</summary>
        public const string ShelteredInventory = "GameRuntime.ShelteredInventory";
        /// <summary>Sheltered asset-loading service.</summary>
        public const string ShelteredAssetLoading = "GameRuntime.ShelteredAssetLoading";
        /// <summary>Sheltered localization service.</summary>
        public const string ShelteredLocalization = "GameRuntime.ShelteredLocalization";
        /// <summary>Sheltered recipe and loot mutation service.</summary>
        public const string ShelteredRecipeLootMutation = "GameRuntime.ShelteredRecipeLootMutation";
        /// <summary>Custom scenario registration and catalog service.</summary>
        public const string CustomScenarios = "GameRuntime.CustomScenarios";
        /// <summary>Scenario authoring service.</summary>
        public const string ScenarioAuthoring = "GameRuntime.ScenarioAuthoring";
    }

    /// <summary>
    /// Bootstrap contract implemented by game-specific runtime assemblies.
    /// The loader calls this once so the game integration can register services before mods consume them.
    /// </summary>
    public interface IGameRuntimeBootstrap
    {
        void Initialize();
    }
}
