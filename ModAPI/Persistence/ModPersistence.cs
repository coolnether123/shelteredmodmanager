using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Persistence
{
    /// <summary>
    /// Manages automatic persistence for mod data.
    /// </summary>
    public static class ModPersistence
    {
        private static readonly List<ISaveable> _registeredCollections = new List<ISaveable>();

        /// <summary>
        /// Registers a collection for automatic save/load.
        /// </summary>
        internal static void Register(ISaveable collection)
        {
            if (!_registeredCollections.Contains(collection))
            {
                _registeredCollections.Add(collection);
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
    }
}
