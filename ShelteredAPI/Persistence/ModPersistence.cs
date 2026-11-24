using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Persistence
{
    /// <summary>
    /// Manages automatic persistence for mod data.
    /// </summary>
    public static class ShelteredPersistence
    {
        public static ShelteredPersistentList<T> CreateList<T>(string uniqueId)
        {
            return new ShelteredPersistentList<T>(uniqueId);
        }

        public static ShelteredPersistentDictionary<TValue> CreateDictionary<TValue>(string uniqueId)
        {
            return new ShelteredPersistentDictionary<TValue>(uniqueId);
        }
    }

    internal static class ModPersistence
    {
        private static readonly List<ISaveable> _registeredCollections = new List<ISaveable>();
        private static readonly List<Action> _resetCollections = new List<Action>();

        /// <summary>
        /// Registers a collection for automatic save/load.
        /// </summary>
        internal static void Register(ISaveable collection, Action reset = null)
        {
            if (!_registeredCollections.Contains(collection))
            {
                _registeredCollections.Add(collection);
                if (reset != null) _resetCollections.Add(reset);
                // Ensure it's registered with the game's SaveManager
                if (SaveManager.instance != null)
                {
                    SaveManager.instance.RegisterSaveable(collection);
                }
            }
        }

        /// <summary>
        /// Global hook to ensure all mod collections are registered with SaveManager when it awakes.
        /// </summary>
        [PatchPolicy(PatchDomain.SaveFlow, "ModPersistenceRegistration",
            TargetBehavior = "Automatic saveable registration when SaveManager starts",
            FailureMode = "Registered mod persistence collections may not attach to the active SaveManager.",
            RollbackStrategy = "Disable the SaveFlow patch domain or remove the ModPersistence registration hook.",
            StartupTiming = PatchStartupTiming.BootCritical)]
        [HarmonyLib.HarmonyPatch(typeof(SaveManager), "Awake")]
        private static class SaveManager_Awake_Patch
        {
            private static void Postfix(SaveManager __instance)
            {
                foreach (var collection in _registeredCollections)
                {
                    __instance.RegisterSaveable(collection);
                }
            }
        }

        [PatchPolicy(PatchDomain.SaveFlow, "ModPersistenceSceneRegistration",
            TargetBehavior = "Restore collection registration after slot selection clears scene saveables",
            FailureMode = "Mod collections stop saving after returning to the menu or loading another slot.",
            RollbackStrategy = "Disable the SaveFlow patch domain or remove the scene registration hook.",
            StartupTiming = PatchStartupTiming.GameplayDeferred)]
        [HarmonyLib.HarmonyPatch(typeof(GameTime), "Awake")]
        private static class GameTime_Awake_Patch
        {
            private static void Postfix()
            {
                foreach (Action reset in _resetCollections) reset();
                SaveManager manager = SaveManager.instance;
                if (manager == null) return;
                foreach (var collection in _registeredCollections)
                    manager.RegisterSaveable(collection);
            }
        }
    }
}
