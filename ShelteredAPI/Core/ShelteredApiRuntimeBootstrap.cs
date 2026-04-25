using ModAPI.Core;
using ModAPI.Actors;
using ModAPI.InputServices;
using ModAPI.Scenarios;
using ShelteredAPI.Input;
using ShelteredAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Core
{
    /// <summary>
    /// Initializes ShelteredAPI core systems even when no mod plugins are enabled.
    /// </summary>
    public static class ShelteredApiRuntimeBootstrap
    {
        private static bool _initialized;
        private static readonly object Sync = new object();
        private const string ProviderId = "shelteredapi";

        public static void Initialize()
        {
            if (_initialized) return;

            lock (Sync)
            {
                if (_initialized) return;

                ScenarioCompositionRoot.EnsureInitialized();
                ScenarioAuthoringInputActions.EnsureRegistered();
                ScenarioAuthoringRuntimeDriver.EnsureCreated();
                ShelteredVanillaInputActions.EnsureRegistered();
                ShelteredKeybindsProvider.Instance.EnsureLoaded();
                ScrollInputService.RegisterSource(UnityScrollInputSource.Instance);
                EnsurePersistenceGuard();
                EnsureApiRegistrations();

                _initialized = true;
                MMLog.WriteInfo("[ShelteredApiRuntimeBootstrap] Core ShelteredAPI input and keybind systems initialized.");
            }
        }

        private static void EnsurePersistenceGuard()
        {
            const string runtimeObjectName = "ShelteredAPI.Runtime";

            var runtimeRoot = GameObject.Find(runtimeObjectName);
            if (runtimeRoot == null)
            {
                runtimeRoot = new GameObject(runtimeObjectName);
                Object.DontDestroyOnLoad(runtimeRoot);
            }

            if (runtimeRoot.GetComponent<ShelteredKeybindPersistenceGuard>() == null)
                runtimeRoot.AddComponent<ShelteredKeybindPersistenceGuard>();
        }

        private static void EnsureApiRegistrations()
        {
            var gameHelper = new GameHelperImpl();
            var overlayInputCapture = new ShelteredOverlayInputCaptureService();
            RegisterApi(GameRuntimeApiIds.GameHelper, gameHelper);
            RegisterApi("ShelteredAPI.GameHelper", gameHelper);
            RegisterApi(OverlayInputCaptureApi.Name, overlayInputCapture);
            RegisterApi("ShelteredAPI.OverlayInputCapture", overlayInputCapture);

            IActorSystem actors = ActorSystem.Instance;
            RegisterApi(GameRuntimeApiIds.Actors, actors);
            RegisterApi("ShelteredAPI.Actors", actors);
            RegisterApi(GameRuntimeApiIds.ActorRegistry, (IActorRegistry)actors);
            RegisterApi("ShelteredAPI.ActorRegistry", (IActorRegistry)actors);
            RegisterApi(GameRuntimeApiIds.ActorComponents, (IActorComponentStore)actors);
            RegisterApi("ShelteredAPI.ActorComponents", (IActorComponentStore)actors);
            RegisterApi(GameRuntimeApiIds.ActorBindings, (IActorBindingStore)actors);
            RegisterApi("ShelteredAPI.ActorBindings", (IActorBindingStore)actors);
            RegisterApi(GameRuntimeApiIds.ActorAdapters, (IActorAdapterRegistry)actors);
            RegisterApi("ShelteredAPI.ActorAdapters", (IActorAdapterRegistry)actors);
            RegisterApi(GameRuntimeApiIds.ActorDiagnostics, (IActorDiagnostics)actors);
            RegisterApi("ShelteredAPI.ActorDiagnostics", (IActorDiagnostics)actors);
            RegisterApi(GameRuntimeApiIds.ActorSimulation, (IActorSimulationScheduler)actors);
            RegisterApi("ShelteredAPI.ActorSimulation", (IActorSimulationScheduler)actors);
            RegisterApi(GameRuntimeApiIds.ActorEvents, (IActorEvents)actors);
            RegisterApi("ShelteredAPI.ActorEvents", (IActorEvents)actors);
            RegisterApi(GameRuntimeApiIds.ActorSerialization, (IActorSerializationService)actors);
            RegisterApi("ShelteredAPI.ActorSerialization", (IActorSerializationService)actors);

            ICustomScenarioService customScenarios = ScenarioCompositionRoot.Resolve<ICustomScenarioService>();
            IScenarioAuthoringBackend scenarioAuthoring = ScenarioCompositionRoot.Resolve<IScenarioAuthoringBackend>();
            ScenarioCompositionRoot.Resolve<IScenarioRuntimeBindingService>().EnsureHooked();
            ScenarioCompositionRoot.Resolve<IShelteredCustomScenarioService>().RefreshDefinitionCatalog();
            RegisterApi(GameRuntimeApiIds.CustomScenarios, customScenarios);
            RegisterApi("ShelteredAPI.CustomScenarios", customScenarios);
            RegisterApi(GameRuntimeApiIds.ScenarioAuthoring, scenarioAuthoring);
            RegisterApi("ShelteredAPI.ScenarioAuthoring", scenarioAuthoring);
        }

        private static void RegisterApi<T>(string apiId, T implementation) where T : class
        {
            if (implementation == null || string.IsNullOrEmpty(apiId))
                return;

            if (ModAPIRegistry.IsAPIRegistered(apiId))
                return;

            ModAPIRegistry.RegisterAPI<T>(apiId, implementation, ProviderId);
        }
    }

    internal sealed class ShelteredGameRuntimeBootstrap : IGameRuntimeBootstrap
    {
        public void Initialize()
        {
            ShelteredApiRuntimeBootstrap.Initialize();
        }
    }
}
