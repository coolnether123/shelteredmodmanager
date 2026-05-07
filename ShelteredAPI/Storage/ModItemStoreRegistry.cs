using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Content;
using ShelteredAPI.Persistence;
using ShelteredAPI.UI.Runtime;
using UnityEngine;
using GameItemDefinition = global::ItemDefinition;

namespace ShelteredAPI.Storage
{
    internal static class ModItemStoreRegistry
    {
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, ModItemStoreState> Stores = new Dictionary<string, ModItemStoreState>(StringComparer.OrdinalIgnoreCase);

        public static IItemStore Get(string ownerId, string storeId, string displayName, int capacity)
        {
            if (string.IsNullOrEmpty(ownerId))
                ownerId = "anonymous";
            if (string.IsNullOrEmpty(storeId))
                storeId = "default";

            ModItemStorePersistence.EnsureRegistered();
            string key = BuildKey(ownerId, storeId);
            lock (Sync)
            {
                ModItemStoreState state;
                if (!Stores.TryGetValue(key, out state))
                {
                    state = new ModItemStoreState
                    {
                        OwnerId = ownerId,
                        StoreId = key,
                        DisplayName = string.IsNullOrEmpty(displayName) ? storeId : displayName,
                        Capacity = Math.Max(0, capacity)
                    };
                    Stores[key] = state;
                }
                else
                {
                    if (!string.IsNullOrEmpty(displayName))
                        state.DisplayName = displayName;
                    if (capacity >= 0)
                        state.Capacity = capacity;
                }

                return new ModItemStore(state);
            }
        }

        internal static List<ModItemStoreState> Snapshot()
        {
            lock (Sync)
                return new List<ModItemStoreState>(Stores.Values);
        }

        internal static void ReplaceAll(List<ModItemStoreState> states)
        {
            lock (Sync)
            {
                Stores.Clear();
                for (int i = 0; states != null && i < states.Count; i++)
                {
                    ModItemStoreState state = states[i];
                    if (state != null && !string.IsNullOrEmpty(state.StoreId))
                        Stores[state.StoreId] = state;
                }
            }
        }

        internal static bool TryGet(string storeId, out IItemStore store)
        {
            store = null;
            if (string.IsNullOrEmpty(storeId))
                return false;

            lock (Sync)
            {
                ModItemStoreState state;
                if (!Stores.TryGetValue(storeId, out state))
                    return false;

                store = new ModItemStore(state);
                return true;
            }
        }

        private static string BuildKey(string ownerId, string storeId)
        {
            return ownerId + "." + storeId;
        }
    }
}
