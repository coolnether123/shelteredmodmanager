using System;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.Actors;
using ModAPI.InputServices;
using ModAPI.Scenarios;
using ShelteredAPI.Actors;
using ShelteredAPI.Content;
using ShelteredAPI.Events;
using ShelteredAPI.Input;
using ShelteredAPI.Scenarios;
using UnityEngine;

namespace ShelteredAPI.Core
{
    /// <summary>
    /// Initializes ShelteredAPI core systems even when no mod plugins are enabled.
    /// </summary>
    internal static class ShelteredApiRuntimeBootstrap
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
                EnsureSaveProtectionPatches();

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
                UnityEngine.Object.DontDestroyOnLoad(runtimeRoot);
            }

            if (runtimeRoot.GetComponent<ShelteredKeybindPersistenceGuard>() == null)
                runtimeRoot.AddComponent<ShelteredKeybindPersistenceGuard>();

            if (runtimeRoot.GetComponent<ModAPI.UI.UIDebugInspector>() == null)
                runtimeRoot.AddComponent<ModAPI.UI.UIDebugInspector>();
        }

        private static void EnsureApiRegistrations()
        {
            var gameHelper = new GameHelperImpl();
            RegisterApi(GameRuntimeApiIds.GameHelper, gameHelper);
            RegisterApi("ShelteredAPI.GameHelper", gameHelper);

            IContentResolutionService contentResolution = new ShelteredContentResolutionService();
            RegisterApi(GameRuntimeApiIds.ContentResolution, contentResolution);
            RegisterApi("ShelteredAPI.ContentResolution", contentResolution);

            IGameLifecycleSource lifecycleSource = new ShelteredGameLifecycleSource();
            RegisterApi(GameRuntimeApiIds.GameLifecycle, lifecycleSource);
            RegisterApi("ShelteredAPI.GameLifecycle", lifecycleSource);

            ISaveRuntimeAdapter saveRuntime = new ShelteredSaveRuntimeAdapter();
            RegisterApi(GameRuntimeApiIds.SaveRuntime, saveRuntime);
            RegisterApi("ShelteredAPI.SaveRuntime", saveRuntime);
            saveRuntime.EnsureRuntimeReady();

            IUiLifecycleEventSink uiLifecycleEvents = new ShelteredUiLifecycleEventSink();
            RegisterApi(GameRuntimeApiIds.UiLifecycleEvents, uiLifecycleEvents);
            RegisterApi("ShelteredAPI.UiLifecycleEvents", uiLifecycleEvents);

            IActorSystem actors = ShelteredActors.Instance;
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

        private static void EnsureSaveProtectionPatches()
        {
            try
            {
                var harmony = new HarmonyLib.Harmony("ShelteredModManager.ShelteredAPI.SaveProtection");
                var patchOptions = new HarmonyUtil.PatchOptions
                {
                    AllowDebugPatches = HarmonyBootstrap.ReadManagerBool("EnableDebugPatches", false),
                    AllowDangerousPatches = HarmonyBootstrap.ReadManagerBool("AllowDangerousPatches", false),
                    AllowStructReturns = HarmonyBootstrap.ReadManagerBool("AllowStructReturns", false)
                };
                var registryOptions = PatchRegistry.CreateManagerOptions(
                    patchOptions,
                    "ShelteredAPI",
                    key => HarmonyBootstrap.ReadManagerString(key, null));

                PatchRegistry.ApplyManualModule(
                    harmony,
                    typeof(SaveProtectionPatches),
                    delegate { SaveProtectionPatches.ApplyPatches(harmony); },
                    registryOptions);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredApiRuntimeBootstrap.SaveProtection", "Failed to apply Sheltered save protection patches: " + ex.Message);
            }
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
