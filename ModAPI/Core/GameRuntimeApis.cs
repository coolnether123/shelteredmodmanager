using ModAPI.Actors;
using ModAPI.Scenarios;

namespace ModAPI.Core
{
    /// <summary>
    /// Typed accessors for game runtime APIs registered through <see cref="ModAPIRegistry"/>.
    /// String IDs remain compatibility plumbing; new callers should prefer these helpers.
    /// </summary>
    public static class GameRuntimeApis
    {
        /// <summary>
        /// Gets a registered runtime API by ID and type.
        /// Returns null when the runtime has not registered the ID or the type does not match.
        /// </summary>
        public static T Get<T>(string apiId) where T : class
        {
            return ModAPIRegistry.GetAPI<T>(apiId);
        }

        /// <summary>
        /// Attempts to get a registered runtime API by ID and type.
        /// This is the preferred form when a mod can run without an optional service.
        /// </summary>
        public static bool TryGet<T>(string apiId, out T api) where T : class
        {
            return ModAPIRegistry.TryGetAPI<T>(apiId, out api);
        }

        /// <summary>Attempts to resolve the host-neutral game helper service.</summary>
        public static bool TryGetGameHelper(out IGameHelper api)
        {
            return TryGet(GameRuntimeApiIds.GameHelper, out api);
        }

        /// <summary>Attempts to resolve mod-facing content IDs to host runtime keys.</summary>
        public static bool TryGetContentResolution(out IContentResolutionService api)
        {
            return TryGet(GameRuntimeApiIds.ContentResolution, out api);
        }

        /// <summary>Attempts to resolve the game lifecycle event source.</summary>
        public static bool TryGetGameLifecycle(out IGameLifecycleSource api)
        {
            return TryGet(GameRuntimeApiIds.GameLifecycle, out api);
        }

        /// <summary>Attempts to resolve the save runtime adapter for the active game.</summary>
        public static bool TryGetSaveRuntime(out ISaveRuntimeAdapter api)
        {
            return TryGet(GameRuntimeApiIds.SaveRuntime, out api);
        }

        /// <summary>Attempts to resolve the UI lifecycle event sink used by legacy patch hosts.</summary>
        public static bool TryGetUiLifecycleEvents(out IUiLifecycleEventSink api)
        {
            return TryGet(GameRuntimeApiIds.UiLifecycleEvents, out api);
        }

        /// <summary>Attempts to resolve the aggregate actor system.</summary>
        public static bool TryGetActors(out IActorSystem api)
        {
            return TryGet(GameRuntimeApiIds.Actors, out api);
        }

        /// <summary>Attempts to resolve the actor registry service.</summary>
        public static bool TryGetActorRegistry(out IActorRegistry api)
        {
            return TryGet(GameRuntimeApiIds.ActorRegistry, out api);
        }

        /// <summary>Attempts to resolve the actor component store.</summary>
        public static bool TryGetActorComponents(out IActorComponentStore api)
        {
            return TryGet(GameRuntimeApiIds.ActorComponents, out api);
        }

        /// <summary>Attempts to resolve the actor binding store.</summary>
        public static bool TryGetActorBindings(out IActorBindingStore api)
        {
            return TryGet(GameRuntimeApiIds.ActorBindings, out api);
        }

        /// <summary>Attempts to resolve the actor adapter registry.</summary>
        public static bool TryGetActorAdapters(out IActorAdapterRegistry api)
        {
            return TryGet(GameRuntimeApiIds.ActorAdapters, out api);
        }

        /// <summary>Attempts to resolve actor diagnostics.</summary>
        public static bool TryGetActorDiagnostics(out IActorDiagnostics api)
        {
            return TryGet(GameRuntimeApiIds.ActorDiagnostics, out api);
        }

        /// <summary>Attempts to resolve the actor simulation scheduler.</summary>
        public static bool TryGetActorSimulation(out IActorSimulationScheduler api)
        {
            return TryGet(GameRuntimeApiIds.ActorSimulation, out api);
        }

        /// <summary>Attempts to resolve the actor event stream.</summary>
        public static bool TryGetActorEvents(out IActorEvents api)
        {
            return TryGet(GameRuntimeApiIds.ActorEvents, out api);
        }

        /// <summary>Attempts to resolve the actor serialization service.</summary>
        public static bool TryGetActorSerialization(out IActorSerializationService api)
        {
            return TryGet(GameRuntimeApiIds.ActorSerialization, out api);
        }

        /// <summary>Attempts to resolve the scenario actor-authoring capability registry.</summary>
        public static bool TryGetActorAuthoringCapabilities(out IActorAuthoringCapabilityRegistry api)
        {
            return TryGet(GameRuntimeApiIds.ActorAuthoringCapabilities, out api);
        }

        /// <summary>Attempts to resolve the custom scenario service.</summary>
        public static bool TryGetCustomScenarios(out ICustomScenarioService api)
        {
            return TryGet(GameRuntimeApiIds.CustomScenarios, out api);
        }
    }
}
