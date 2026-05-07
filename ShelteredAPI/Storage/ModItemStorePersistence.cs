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
    internal sealed class ModItemStorePersistence : ISaveable
    {
        private const string GroupName = "ShelteredAPI_ModItemStores";
        private const string StoresKey = "stores";
        private static readonly ModItemStorePersistence Instance = new ModItemStorePersistence();
        private static bool _registered;

        private ModItemStorePersistence()
        {
        }

        public static void EnsureRegistered()
        {
            if (_registered)
                return;

            ModPersistence.Register(Instance);
            _registered = true;
        }

        public bool IsReadyForLoad() { return true; }
        public bool IsRelocationEnabled() { return true; }

        public bool SaveLoad(SaveData data)
        {
            if (data == null)
                return false;

            data.GroupStart(GroupName);
            try
            {
                List<StoreSaveEntry> entries = data.isSaving ? BuildEntries(ModItemStoreRegistry.Snapshot()) : new List<StoreSaveEntry>();
                data.SaveLoadList(StoresKey, (IList)entries,
                    i => SaveLoadEntry(data, entries[i]),
                    i =>
                    {
                        StoreSaveEntry entry = new StoreSaveEntry();
                        SaveLoadEntry(data, entry);
                        entries.Add(entry);
                    });

                if (data.isLoading)
                    ModItemStoreRegistry.ReplaceAll(BuildStates(entries));
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ModItemStorePersistence.SaveLoad", "Store persistence failed: " + ex.Message);
            }
            finally
            {
                data.GroupEnd();
            }

            return true;
        }

        private static void SaveLoadEntry(SaveData data, StoreSaveEntry entry)
        {
            data.GroupStart("store");
            data.SaveLoad("ownerId", ref entry.OwnerId);
            data.SaveLoad("storeId", ref entry.StoreId);
            data.SaveLoad("displayName", ref entry.DisplayName);
            data.SaveLoad("capacity", ref entry.Capacity);
            data.SaveLoad("itemId", ref entry.ItemId);
            data.SaveLoad("count", ref entry.Count);
            data.GroupEnd();
        }

        private static List<StoreSaveEntry> BuildEntries(List<ModItemStoreState> states)
        {
            List<StoreSaveEntry> entries = new List<StoreSaveEntry>();
            for (int i = 0; states != null && i < states.Count; i++)
            {
                ModItemStoreState state = states[i];
                if (state == null)
                    continue;

                bool any = false;
                foreach (KeyValuePair<string, int> item in state.Items)
                {
                    any = true;
                    entries.Add(new StoreSaveEntry
                    {
                        OwnerId = state.OwnerId,
                        StoreId = state.StoreId,
                        DisplayName = state.DisplayName,
                        Capacity = state.Capacity,
                        ItemId = item.Key,
                        Count = item.Value
                    });
                }

                if (!any)
                {
                    entries.Add(new StoreSaveEntry
                    {
                        OwnerId = state.OwnerId,
                        StoreId = state.StoreId,
                        DisplayName = state.DisplayName,
                        Capacity = state.Capacity
                    });
                }
            }
            return entries;
        }

        private static List<ModItemStoreState> BuildStates(List<StoreSaveEntry> entries)
        {
            Dictionary<string, ModItemStoreState> states = new Dictionary<string, ModItemStoreState>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                StoreSaveEntry entry = entries[i];
                if (entry == null || string.IsNullOrEmpty(entry.StoreId))
                    continue;

                ModItemStoreState state;
                if (!states.TryGetValue(entry.StoreId, out state))
                {
                    state = new ModItemStoreState
                    {
                        OwnerId = entry.OwnerId,
                        StoreId = entry.StoreId,
                        DisplayName = entry.DisplayName,
                        Capacity = Math.Max(0, entry.Capacity)
                    };
                    states[entry.StoreId] = state;
                }

                if (!string.IsNullOrEmpty(entry.ItemId) && entry.Count > 0)
                    state.Items[entry.ItemId] = entry.Count;
            }

            return new List<ModItemStoreState>(states.Values);
        }

        private sealed class StoreSaveEntry
        {
            public string OwnerId = string.Empty;
            public string StoreId = string.Empty;
            public string DisplayName = string.Empty;
            public int Capacity;
            public string ItemId = string.Empty;
            public int Count;
        }
    }
}
