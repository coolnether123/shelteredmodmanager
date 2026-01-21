using System;
using System.Collections;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Util
{
    /// <summary>
    /// Provides a single save-group for mods to store JSON blobs keyed by mod id.
    /// Mods interact via ctx.SaveData/LoadData helpers; the proxy serializes once per save.
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

    /// <summary>
    /// Implements ISaveable to persist mod data in a dedicated "ModAPI_Data" group.
    /// Stores one JSON blob per mod id, containing that mod's key/value pairs.
    /// </summary>
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
            if (_registered)
                return;

            var mgr = SaveManager.instance;
            if (mgr == null)
                return;

            mgr.RegisterSaveable(Instance);
            _registered = true;
        }

        public static void Save(string modId, string key, object data)
        {
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(key))
                return;

            lock (Sync)
            {
                Dictionary<string, string> modData;
                if (!DataByMod.TryGetValue(modId, out modData))
                {
                    modData = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                    DataByMod[modId] = modData;
                }

                string json = string.Empty;
                try { json = data != null ? JsonUtility.ToJson(data) : string.Empty; }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("PersistentDataAPI.Serialize." + modId + "." + key, "Failed to serialize mod data: " + ex.Message);
                }

                modData[key] = json ?? string.Empty;
            }
        }

        public static bool TryLoad<T>(string modId, string key, out T value)
        {
            value = default(T);
            if (string.IsNullOrEmpty(modId) || string.IsNullOrEmpty(key))
                return false;

            lock (Sync)
            {
                Dictionary<string, string> modData;
                if (!DataByMod.TryGetValue(modId, out modData) || modData == null)
                    return false;

                string raw;
                if (!modData.TryGetValue(key, out raw) || string.IsNullOrEmpty(raw))
                    return false;

                try
                {
                    value = JsonUtility.FromJson<T>(raw);
                    return true;
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("PersistentDataAPI.Deserialize." + modId + "." + key, "Failed to deserialize mod data: " + ex.Message);
                    return false;
                }
            }
        }

        public bool IsRelocationEnabled() { return true; }

        public bool IsReadyForLoad() { return true; }

        public bool SaveLoad(SaveData data)
        {
            if (data == null)
                return false;

            lock (Sync)
            {
                data.GroupStart(GroupName);
                try
                {
                    var mods = new List<KeyValuePair<string, string>>(DataByMod.Count);
                    foreach (var kvp in DataByMod)
                    {
                        mods.Add(new KeyValuePair<string, string>(kvp.Key, SerializeModMap(kvp.Value)));
                    }

                    data.SaveLoadList(ModsGroup, (IList)mods,
                        i =>
                        {
                            var pair = mods[i];
                            var id = pair.Key ?? string.Empty;
                            var payload = pair.Value ?? string.Empty;
                            data.SaveLoad(IdKey, ref id);
                            data.SaveLoad(PayloadKey, ref payload);
                        },
                        i =>
                        {
                            string id = string.Empty;
                            string payload = string.Empty;
                            data.SaveLoad(IdKey, ref id);
                            data.SaveLoad(PayloadKey, ref payload);
                            if (!string.IsNullOrEmpty(id))
                                DataByMod[id] = DeserializeModMap(payload);
                        });
                }
                catch (Exception ex)
                {
                    MMLog.WarnOnce("PersistentDataAPI.SaveLoad", "SaveLoad error: " + ex.Message);
                }
                finally
                {
                    data.GroupEnd();
                }
            }

            return true;
        }

        private static string SerializeModMap(Dictionary<string, string> map)
        {
            try
            {
                var envelope = new ModEnvelope();
                if (map != null)
                {
                    foreach (var kv in map)
                        envelope.entries.Add(new ModEntry { key = kv.Key, value = kv.Value });
                }
                return JsonUtility.ToJson(envelope);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PersistentDataAPI.SerializeMap", "Failed to serialize map: " + ex.Message);
                return string.Empty;
            }
        }

        private static Dictionary<string, string> DeserializeModMap(string json)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(json))
                return result;

            try
            {
                var envelope = JsonUtility.FromJson<ModEnvelope>(json);
                if (envelope != null && envelope.entries != null)
                {
                    for (int i = 0; i < envelope.entries.Count; i++)
                    {
                        var entry = envelope.entries[i];
                        if (!string.IsNullOrEmpty(entry.key))
                            result[entry.key] = entry.value ?? string.Empty;
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("PersistentDataAPI.DeserializeMap", "Failed to deserialize map: " + ex.Message);
            }

            return result;
        }

        [Serializable]
        private class ModEnvelope
        {
            public List<ModEntry> entries = new List<ModEntry>();
        }

        [Serializable]
        private class ModEntry
        {
            public string key;
            public string value;
        }
    }

    [HarmonyLib.HarmonyPatch(typeof(SaveManager), "Awake")]
    internal static class PersistentDataAPI_SaveManagerHook
    {
        private static void Postfix()
        {
            ModSaveDataProxy.EnsureRegistered();
        }
    }
}
