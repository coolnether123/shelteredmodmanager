using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Util
{
    /// <summary>
    /// Provides a single save-group for mods to store JSON blobs keyed by mod id.
    /// </summary>
    public static class PersistentDataAPI
    {
        public static void SaveData<T>(this IPluginContext ctx, string key, T data)
        {
            if (ctx == null || ctx.Mod == null || string.IsNullOrEmpty(ctx.Mod.Id))
                return;

            ModSaveDataProxy.EnsureRegistered();
            ModSaveDataProxy.Save(ctx.Mod.Id, key, data);
        }

        public static bool LoadData<T>(this IPluginContext ctx, string key, out T value)
        {
            value = default(T);
            if (ctx == null || ctx.Mod == null || string.IsNullOrEmpty(ctx.Mod.Id))
                return false;

            ModSaveDataProxy.EnsureRegistered();
            return ModSaveDataProxy.TryLoad(ctx.Mod.Id, key, out value);
        }
    }

    public sealed class ModSaveDataProxy : ISaveable
    {
        private const string GroupName = "ModAPI_Data";
        private const string ModsGroup = "mods";
        private const string IdKey = "id";
        private const string PayloadKey = "payload";

        private static readonly Dictionary<string, Dictionary<string, string>> DataByMod = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Sync = new object();
        private static bool _registered;
        private static readonly ModSaveDataProxy Instance = new ModSaveDataProxy();

        private ModSaveDataProxy() { }

        public static void EnsureRegistered()
        {
            if (_registered) return;
            var mgr = SaveManager.instance;
            if (mgr == null) return;
            mgr.RegisterSaveable(Instance);
            _registered = true;
        }

        public static void Save(string modId, string key, object data)
        {
            lock (Sync)
            {
                Dictionary<string, string> modData;
                if (!DataByMod.TryGetValue(modId, out modData))
                {
                    modData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    DataByMod[modId] = modData;
                }
                modData[key] = JsonUtility.ToJson(data);
            }
        }

        public static bool TryLoad<T>(string modId, string key, out T value)
        {
            value = default(T);
            lock (Sync)
            {
                if (!DataByMod.TryGetValue(modId, out var modData) || !modData.TryGetValue(key, out var raw))
                    return false;
                value = JsonUtility.FromJson<T>(raw);
                return true;
            }
        }

        public bool IsRelocationEnabled() { return true; }
        public bool IsReadyForLoad() { return true; }

        public bool SaveLoad(SaveData data)
        {
            if (data == null) return false;
            lock (Sync)
            {
                data.GroupStart(GroupName);
                // Save/Load logic...
                data.GroupEnd();
            }
            return true;
        }
    }
}
