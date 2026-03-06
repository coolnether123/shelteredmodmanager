using ModAPI.Core;
using ModAPI.Actors;
using ShelteredAPI.Input;
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

        public static void Initialize()
        {
            if (_initialized) return;

            lock (Sync)
            {
                if (_initialized) return;

                ShelteredInputActions.EnsureRegistered();
                ShelteredVanillaInputActions.EnsureRegistered();
                ShelteredKeybindsProvider.Instance.EnsureLoaded();
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
            ModAPIRegistry.RegisterAPI<IGameHelper>("ShelteredAPI.GameHelper", new GameHelperImpl(), "shelteredapi");

            IActorSystem actors = ActorSystem.Instance;
            ModAPIRegistry.RegisterAPI<IActorSystem>("ShelteredAPI.Actors", actors, "shelteredapi");
            ModAPIRegistry.RegisterAPI<IActorRegistry>("ShelteredAPI.ActorRegistry", actors, "shelteredapi");
            ModAPIRegistry.RegisterAPI<IActorComponentStore>("ShelteredAPI.ActorComponents", actors, "shelteredapi");
            ModAPIRegistry.RegisterAPI<IActorSimulationScheduler>("ShelteredAPI.ActorSimulation", actors, "shelteredapi");
            ModAPIRegistry.RegisterAPI<IActorEvents>("ShelteredAPI.ActorEvents", actors, "shelteredapi");
            ModAPIRegistry.RegisterAPI<IActorSerializationService>("ShelteredAPI.ActorSerialization", actors, "shelteredapi");
        }
    }
}
