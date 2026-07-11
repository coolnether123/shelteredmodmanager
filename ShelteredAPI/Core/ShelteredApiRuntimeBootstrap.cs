using System;
using System.Diagnostics;
using ModAPI.Core;
using ShelteredAPI.UI.Compatibility;
using ModAPI.Harmony;
using ModAPI.Actors;
using ModAPI.InputServices;
using ModAPI.Scenarios;
using ShelteredAPI.Actors;
using ShelteredAPI.Content;
using ShelteredAPI.Debugging;
using ShelteredAPI.Events;
using ShelteredAPI.Input;
using ShelteredAPI.Storage;
using ShelteredAPI.Scenarios;
using UnityEngine;


using ShelteredAPI.Harmony;
using ShelteredAPI.Saves;
using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Application.Runtime;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Shared;
using ShelteredAPI.Scenarios.Infrastructure.Harmony;
namespace ShelteredAPI.Core
{
    /// <summary>
    /// Initializes ShelteredAPI core systems even when no mod plugins are enabled.
    /// </summary>
    internal static class ShelteredApiRuntimeBootstrap
    {
        private static bool _initialized;
        private static bool _saveProtectionPatched;
        private static readonly object Sync = new object();
        private const string ProviderId = "shelteredapi";

        public static void Initialize()
        {
            if (_initialized) return;

            lock (Sync)
            {
                if (_initialized) return;

                MeasureStartupPhase("ShelteredAPI ShelteredUnityLogNormalizers.Register", ShelteredUnityLogNormalizers.Register);
                MeasureStartupPhase("ShelteredAPI ScenarioCompositionRoot.EnsureRuntimeInitialized", ScenarioCompositionRoot.EnsureRuntimeInitialized);
                MeasureStartupPhase("ShelteredAPI ScenarioRngPatches.Install", ScenarioRngPatches.Install);
                MeasureStartupPhase("ShelteredAPI ScenarioFeatureToggles.RegisterCustomScenarioEditorToggle", ScenarioFeatureToggles.RegisterCustomScenarioEditorToggle);
                if (ScenarioFeatureToggles.IsCustomScenarioEditorEnabled())
                {
                    MMLog.WriteInfo("[ShelteredApiRuntimeBootstrap] Custom scenario editor runtime hooks are deferred until authoring opens.");
                }
                else
                {
                    MMLog.WriteInfo("[ShelteredApiRuntimeBootstrap] Custom scenario editor runtime hooks are disabled by manager option.");
                }
                ShelteredAPI.Harmony.ShelteredDeferredPatchTriggers.ApplyDebugDeferred("ShelteredAPI runtime diagnostics enabled");
                MeasureStartupPhase("ShelteredAPI ShelteredVanillaInputActions.EnsureRegistered", ShelteredVanillaInputActions.EnsureRegistered);
                MeasureStartupPhase("ShelteredAPI ShelteredKeybindsProvider.EnsureLoaded", ShelteredKeybindsProvider.Instance.EnsureLoaded);
                MeasureStartupPhase("ShelteredAPI ShelteredStores.EnsurePersistenceRegistered", ShelteredStores.EnsurePersistenceRegistered);
                MeasureStartupPhase("ShelteredAPI ScrollInputService.RegisterSource", delegate
                {
                    ScrollInputService.RegisterSource(UnityScrollInputSource.Instance);
                });
                MeasureStartupPhase("ShelteredAPI EnsurePersistenceGuard", EnsurePersistenceGuard);
                MeasureStartupPhase("ShelteredAPI EnsureApiRegistrations", EnsureApiRegistrations);

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

            LoadingTransitionRecoveryService.EnsureInstalled(runtimeRoot);

            ShelteredFeedbackBootstrap.EnsureInstalled(runtimeRoot);

            if (runtimeRoot.GetComponent<ShelteredAPI.UI.Compatibility.UIDebugInspector>() == null)
                runtimeRoot.AddComponent<ShelteredAPI.UI.Compatibility.UIDebugInspector>();
        }

        private static void EnsureApiRegistrations()
        {
            var gameHelper = new GameHelperImpl();
            RegisterApi(GameRuntimeApiIds.GameHelper, gameHelper);
            RegisterApi(ShelteredApiAliasIds.GameHelper, gameHelper);

            IContentResolutionService contentResolution = new ShelteredContentResolutionService();
            RegisterApi(GameRuntimeApiIds.ContentResolution, contentResolution);
            RegisterApi(ShelteredApiAliasIds.ContentResolution, contentResolution);

            IShelteredContentService content = ShelteredContent.Service;
            RegisterApi(GameRuntimeApiIds.ShelteredContent, content);
            RegisterApi(ShelteredApiAliasIds.ShelteredContent, content);
            RegisterApi(GameRuntimeApiIds.ShelteredContentRegistration, content.Registration);
            RegisterApi(ShelteredApiAliasIds.ShelteredContentRegistration, content.Registration);
            RegisterApi(GameRuntimeApiIds.ShelteredInventory, content.Inventory);
            RegisterApi(ShelteredApiAliasIds.ShelteredInventory, content.Inventory);
            RegisterApi(GameRuntimeApiIds.ShelteredAssetLoading, content.Assets);
            RegisterApi(ShelteredApiAliasIds.ShelteredAssetLoading, content.Assets);
            RegisterApi(GameRuntimeApiIds.ShelteredLocalization, content.Localization);
            RegisterApi(ShelteredApiAliasIds.ShelteredLocalization, content.Localization);
            RegisterApi(GameRuntimeApiIds.ShelteredRecipeLootMutation, content.RecipeLootMutation);
            RegisterApi(ShelteredApiAliasIds.ShelteredRecipeLootMutation, content.RecipeLootMutation);
            ShelteredCharacterItems.EnsureRegistered();
            RegisterApi(ShelteredApiAliasIds.CharacterItems, ShelteredCharacterItems.Service);

            IGameLifecycleSource lifecycleSource = new ShelteredGameLifecycleSource();
            RegisterApi(GameRuntimeApiIds.GameLifecycle, lifecycleSource);
            RegisterApi(ShelteredApiAliasIds.GameLifecycle, lifecycleSource);

            ISaveRuntimeAdapter saveRuntime = new ShelteredSaveRuntimeAdapter();
            RegisterApi(GameRuntimeApiIds.SaveRuntime, saveRuntime);
            RegisterApi(ShelteredApiAliasIds.SaveRuntime, saveRuntime);
            saveRuntime.EnsureRuntimeReady();

            IUiLifecycleEventSink uiLifecycleEvents = new ShelteredUiLifecycleEventSink();
            RegisterApi(GameRuntimeApiIds.UiLifecycleEvents, uiLifecycleEvents);
            RegisterApi(ShelteredApiAliasIds.UiLifecycleEvents, uiLifecycleEvents);

            IActorSystem actors = ShelteredActors.Instance;
            RegisterApi(GameRuntimeApiIds.Actors, actors);
            RegisterApi(ShelteredApiAliasIds.Actors, actors);
            RegisterApi(GameRuntimeApiIds.ActorRegistry, (IActorRegistry)actors);
            RegisterApi(ShelteredApiAliasIds.ActorRegistry, (IActorRegistry)actors);
            RegisterApi(GameRuntimeApiIds.ActorComponents, (IActorComponentStore)actors);
            RegisterApi(ShelteredApiAliasIds.ActorComponents, (IActorComponentStore)actors);
            RegisterApi(GameRuntimeApiIds.ActorBindings, (IActorBindingStore)actors);
            RegisterApi(ShelteredApiAliasIds.ActorBindings, (IActorBindingStore)actors);
            RegisterApi(GameRuntimeApiIds.ActorAdapters, (IActorAdapterRegistry)actors);
            RegisterApi(ShelteredApiAliasIds.ActorAdapters, (IActorAdapterRegistry)actors);
            RegisterApi(GameRuntimeApiIds.ActorDiagnostics, (IActorDiagnostics)actors);
            RegisterApi(ShelteredApiAliasIds.ActorDiagnostics, (IActorDiagnostics)actors);
            RegisterApi(GameRuntimeApiIds.ActorSimulation, (IActorSimulationScheduler)actors);
            RegisterApi(ShelteredApiAliasIds.ActorSimulation, (IActorSimulationScheduler)actors);
            RegisterApi(GameRuntimeApiIds.ActorEvents, (IActorEvents)actors);
            RegisterApi(ShelteredApiAliasIds.ActorEvents, (IActorEvents)actors);
            RegisterApi(GameRuntimeApiIds.ActorSerialization, (IActorSerializationService)actors);
            RegisterApi(ShelteredApiAliasIds.ActorSerialization, (IActorSerializationService)actors);
            IActorAuthoringCapabilityRegistry actorAuthoringCapabilities = new ScenarioActorAuthoringCapabilityRegistry();
            if (ScenarioFeatureToggles.IsDevActorAuthoringProviderEnabled())
                actorAuthoringCapabilities.RegisterProvider(new ScenarioDevActorAuthoringCapabilityProvider());
            RegisterApi(GameRuntimeApiIds.ActorAuthoringCapabilities, actorAuthoringCapabilities);
            RegisterApi(ShelteredApiAliasIds.ActorAuthoringCapabilities, actorAuthoringCapabilities);

            ICustomScenarioService customScenarios = ScenarioCompositionRoot.ResolveRuntime<ICustomScenarioService>();
            ScenarioCompositionRoot.ResolveRuntime<IScenarioRuntimeBindingService>().EnsureHooked();
            RegisterApi(GameRuntimeApiIds.CustomScenarios, customScenarios);
            RegisterApi(ShelteredApiAliasIds.CustomScenarios, customScenarios);

        }

        internal static void EnsureAuthoringApiRegistered()
        {
            if (!ScenarioFeatureToggles.IsCustomScenarioEditorEnabled())
                return;

            IScenarioAuthoringBackend scenarioAuthoring = ScenarioCompositionRoot.Resolve<IScenarioAuthoringBackend>();
            RegisterApi(GameRuntimeApiIds.ScenarioAuthoring, scenarioAuthoring);
            RegisterApi(ShelteredApiAliasIds.ScenarioAuthoring, scenarioAuthoring);
        }

        internal static void EnsureSaveProtectionPatches()
        {
            lock (Sync)
            {
                if (_saveProtectionPatched)
                    return;
                _saveProtectionPatched = true;
            }

            MeasureStartupPhase("ShelteredAPI deferred SaveProtection SaveFlowCritical", delegate
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
            });
        }

        private static void RegisterApi<T>(string apiId, T implementation) where T : class
        {
            if (implementation == null || string.IsNullOrEmpty(apiId))
                return;

            if (ModAPIRegistry.IsAPIRegistered(apiId))
                return;

            ModAPIRegistry.RegisterAPI<T>(apiId, implementation, ProviderId);
        }

        private static void MeasureStartupPhase(string phaseName, Action action)
        {
            if (action == null)
                return;

            var timer = Stopwatch.StartNew();
            try
            {
                action();
            }
            finally
            {
                timer.Stop();
                MMLog.WriteWithSource(
                    MMLog.LogLevel.Info,
                    MMLog.LogCategory.General,
                    "StartupTiming",
                    phaseName + " took " + timer.ElapsedMilliseconds + "ms.");
            }
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
