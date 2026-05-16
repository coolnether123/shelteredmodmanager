using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
using ModAPI.Util;
using UnityEngine;

namespace ModAPI.Persistence
{
    internal sealed class ModPersistenceStore
    {
        private readonly string _modId;

        internal ModPersistenceStore(string modId)
        {
            _modId = modId;
        }

        internal string Serialize(Dictionary<string, object> registeredData)
        {
            var containerObj = new ModPersistenceData();
            foreach (var kv in registeredData)
            {
                containerObj.entries.Add(new ModDataEntry { key = kv.Key, json = JsonUtility.ToJson(kv.Value) });
            }

            return SerializeContainer(containerObj);
        }

        internal void Write(string rootPath, string json)
        {
            string modFilePath = GetCurrentFilePath(rootPath);
            string modDataFolder = Path.GetDirectoryName(modFilePath);
            if (!Directory.Exists(modDataFolder)) Directory.CreateDirectory(modDataFolder);

            File.WriteAllText(modFilePath, json);
            CleanupLegacyFile(rootPath);
        }

        internal ModPersistenceLoadResult Load(string rootPath)
        {
            string modFilePath = ResolveReadableFilePath(rootPath);
            if (modFilePath == null) return null;

            string json = File.ReadAllText(modFilePath);
            var container = DeserializeContainer(json);
            return new ModPersistenceLoadResult(container, Path.GetFileName(modFilePath), IsLegacyFilePath(rootPath, modFilePath));
        }

        internal string GetCurrentFilePath(string rootPath)
        {
            return Path.Combine(Path.Combine(Path.Combine(rootPath, "mods"), _modId), "data.json");
        }

        private string ResolveReadableFilePath(string rootPath)
        {
            string currentFilePath = GetCurrentFilePath(rootPath);
            if (File.Exists(currentFilePath)) return currentFilePath;

            string legacyFilePath = GetLegacyFilePath(rootPath);
            return File.Exists(legacyFilePath) ? legacyFilePath : null;
        }

        private void CleanupLegacyFile(string rootPath)
        {
            string legacyFilePath = GetLegacyFilePath(rootPath);
            if (!File.Exists(legacyFilePath)) return;

            try
            {
                File.Delete(legacyFilePath);
                MMLog.WriteInfo("[SaveSystem] Migration complete for " + _modId + ": Cleaned up legacy file " + Path.GetFileName(legacyFilePath));
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[SaveSystem] Failed to clean up legacy file for " + _modId + ": " + ex.Message);
            }
        }

        private bool IsLegacyFilePath(string rootPath, string filePath)
        {
            return string.Equals(GetLegacyFilePath(rootPath), filePath, StringComparison.OrdinalIgnoreCase);
        }

        private string GetLegacyFilePath(string rootPath)
        {
            return Path.Combine(rootPath, "mod_" + _modId.Replace('.', '_') + "_data.json");
        }

        private static string SerializeContainer(ModPersistenceData data)
        {
            if (data == null)
            {
                data = new ModPersistenceData();
            }

            ManualJsonObject root = new ManualJsonObject();
            ManualJsonArray entries = new ManualJsonArray();
            if (data.entries != null)
            {
                for (int i = 0; i < data.entries.Count; i++)
                {
                    ModDataEntry entry = data.entries[i];
                    if (entry == null || string.IsNullOrEmpty(entry.key))
                    {
                        continue;
                    }

                    ManualJsonObject item = new ManualJsonObject();
                    item.Set("key", ManualJsonValue.String(entry.key));
                    item.Set("json", ManualJsonValue.String(entry.json ?? string.Empty));
                    entries.Add(ManualJsonValue.Object(item));
                }
            }

            root.Set("entries", ManualJsonValue.Array(entries));
            return ManualJson.Serialize(root, true);
        }

        private static ModPersistenceData DeserializeContainer(string json)
        {
            ModPersistenceData data = new ModPersistenceData();
            if (string.IsNullOrEmpty(json))
            {
                return data;
            }

            ManualJsonObject root;
            string error;
            if (!ManualJson.TryParseObject(json, out root, out error))
            {
                MMLog.WriteWarning("[SaveSystem] Could not parse persistence JSON for a mod entry: " + error);
                return data;
            }

            ManualJsonArray entries = root.GetArray("entries");
            if (entries == null)
            {
                return data;
            }

            for (int i = 0; i < entries.Items.Count; i++)
            {
                ManualJsonValue value = entries.Items[i];
                ManualJsonObject item = value != null && value.Type == ManualJsonValueType.Object ? value.ObjectValue : null;
                if (item == null)
                {
                    continue;
                }

                string key = item.GetString("key", string.Empty);
                if (string.IsNullOrEmpty(key))
                {
                    continue;
                }

                data.entries.Add(new ModDataEntry
                {
                    key = key,
                    json = item.GetString("json", string.Empty)
                });
            }

            return data;
        }
    }

    internal sealed class ModPersistenceLoadResult
    {
        internal ModPersistenceLoadResult(ModPersistenceData data, string fileName, bool isLegacy)
        {
            Data = data;
            FileName = fileName;
            IsLegacy = isLegacy;
        }

        internal ModPersistenceData Data { get; private set; }
        internal string FileName { get; private set; }
        internal bool IsLegacy { get; private set; }
    }
}
