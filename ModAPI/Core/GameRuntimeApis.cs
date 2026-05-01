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
        public static T Get<T>(string apiId) where T : class
        {
            return ModAPIRegistry.GetAPI<T>(apiId);
        }

        public static bool TryGet<T>(string apiId, out T api) where T : class
        {
            return ModAPIRegistry.TryGetAPI<T>(apiId, out api);
        }

        public static bool TryGetGameHelper(out IGameHelper api)
        {
            return TryGet(GameRuntimeApiIds.GameHelper, out api);
        }

        public static bool TryGetContentResolution(out IContentResolutionService api)
        {
            return TryGet(GameRuntimeApiIds.ContentResolution, out api);
        }

        public static bool TryGetGameLifecycle(out IGameLifecycleSource api)
        {
            return TryGet(GameRuntimeApiIds.GameLifecycle, out api);
        }

        public static bool TryGetSaveRuntime(out ISaveRuntimeAdapter api)
        {
            return TryGet(GameRuntimeApiIds.SaveRuntime, out api);
        }

        public static bool TryGetUiLifecycleEvents(out IUiLifecycleEventSink api)
        {
            return TryGet(GameRuntimeApiIds.UiLifecycleEvents, out api);
        }

        public static bool TryGetActors(out IActorSystem api)
        {
            return TryGet(GameRuntimeApiIds.Actors, out api);
        }

        public static bool TryGetActorRegistry(out IActorRegistry api)
        {
            return TryGet(GameRuntimeApiIds.ActorRegistry, out api);
        }

        public static bool TryGetActorComponents(out IActorComponentStore api)
        {
            return TryGet(GameRuntimeApiIds.ActorComponents, out api);
        }

        public static bool TryGetActorBindings(out IActorBindingStore api)
        {
            return TryGet(GameRuntimeApiIds.ActorBindings, out api);
        }

        public static bool TryGetActorAdapters(out IActorAdapterRegistry api)
        {
            return TryGet(GameRuntimeApiIds.ActorAdapters, out api);
        }

        public static bool TryGetActorDiagnostics(out IActorDiagnostics api)
        {
            return TryGet(GameRuntimeApiIds.ActorDiagnostics, out api);
        }

        public static bool TryGetActorSimulation(out IActorSimulationScheduler api)
        {
            return TryGet(GameRuntimeApiIds.ActorSimulation, out api);
        }

        public static bool TryGetActorEvents(out IActorEvents api)
        {
            return TryGet(GameRuntimeApiIds.ActorEvents, out api);
        }

        public static bool TryGetActorSerialization(out IActorSerializationService api)
        {
            return TryGet(GameRuntimeApiIds.ActorSerialization, out api);
        }

        public static bool TryGetCustomScenarios(out ICustomScenarioService api)
        {
            return TryGet(GameRuntimeApiIds.CustomScenarios, out api);
        }
    }
}
