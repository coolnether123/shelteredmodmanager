using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Core;
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

            return JsonUtility.ToJson(containerObj, true);
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
            var container = JsonUtility.FromJson<ModPersistenceData>(json);
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
