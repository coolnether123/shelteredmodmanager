using System;
using System.Collections.Generic;
using System.IO;
using ModAPI.Saves;
using ModAPI.Events;
using UnityEngine;

namespace ModAPI.Core
{
    /// <summary>
    /// Implementation of the per-mod save data persistence.
    /// Manages 'mods_data.json' within each save slot directory.
    /// </summary>
    internal class SaveSystemImpl : ISaveSystem
    {
        private readonly Dictionary<string, object> _registeredData = new Dictionary<string, object>();
        private readonly Dictionary<string, Delegate> _migrationCallbacks = new Dictionary<string, Delegate>();
        private readonly string _modId;

        public SaveSystemImpl(string modId)
        {
            _modId = modId;
            // Subscribe to global life-cycle events
            GameEvents.OnBeforeSave += HandleBeforeSave;
            GameEvents.OnAfterLoad += HandleAfterLoad;
        }

        public string GetCurrentSlotPath()
        {
            var active = ModAPI.Hooks.PlatformSaveProxy.ActiveCustomSave;
            if (active == null) return null;
            
            // scenarioId is preferred. Fallback to "Standard" if not set.
            string scenario = string.IsNullOrEmpty(active.scenarioId) ? "Standard" : active.scenarioId;
            return DirectoryProvider.SlotRoot(scenario, active.absoluteSlot, false);
        }

        public int ActiveSlotIndex
        {
            get
            {
                var custom = ModAPI.Hooks.PlatformSaveProxy.ActiveCustomSave;
                return custom != null ? custom.absoluteSlot : -1;
            }
        }

        public void RegisterModData<T>(string key, T data, Action<T> migrationCallback = null) where T : class
        {
            if (string.IsNullOrEmpty(key)) return;
            _registeredData[key] = data;
            if (migrationCallback != null) _migrationCallbacks[key] = migrationCallback;
        }

        private void HandleBeforeSave(SaveData gameData)
        {
            var path = GetCurrentSlotPath();
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                // Ensure directory exists for safety
                if (!Directory.Exists(path)) Directory.CreateDirectory(path);

                // REVISION: This implementation is per-mod instance. 
                // To avoid conflicts, we'll store per-mod files: 'mod_<id>_data.json'
                var modFileName = string.Format("mod_{0}_data.json", _modId.Replace('.', '_'));
                var modFilePath = Path.Combine(path, modFileName);

                var containerObj = new ModPersistenceData();
                foreach (var kv in _registeredData)
                {
                    containerObj.entries.Add(new ModDataEntry { key = kv.Key, json = JsonUtility.ToJson(kv.Value) });
                }

                File.WriteAllText(modFilePath, JsonUtility.ToJson(containerObj, true));
                MMLog.WriteDebug(string.Format("[SaveSystem] Saved mod data for {0} to {1}", _modId, modFileName));
            }
            catch (Exception ex)
            {
                MMLog.WriteError(string.Format("[SaveSystem] Failed to save mod data for {0}: {1}", _modId, ex.Message));
            }
        }

        private void HandleAfterLoad(SaveData gameData)
        {
            var path = GetCurrentSlotPath();
            if (string.IsNullOrEmpty(path)) return;

            var modFileName = $"mod_{_modId.Replace('.', '_')}_data.json";
            var modFilePath = Path.Combine(path, modFileName);

            var loadedKeys = new HashSet<string>();

            if (File.Exists(modFilePath))
            {
                try
                {
                    var json = File.ReadAllText(modFilePath);
                    var container = JsonUtility.FromJson<ModPersistenceData>(json);
                    if (container != null && container.entries != null)
                    {
                        foreach (var entry in container.entries)
                        {
                            if (_registeredData.TryGetValue(entry.key, out var dataObj))
                            {
                                JsonUtility.FromJsonOverwrite(entry.json, dataObj);
                                loadedKeys.Add(entry.key);
                            }
                        }
                        MMLog.WriteDebug(string.Format("[SaveSystem] Loaded mod data for {0} from {1}", _modId, modFileName));
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError(string.Format("[SaveSystem] Failed to load mod data for {0}: {1}", _modId, ex.Message));
                }
            }

            // Migration check: If key registered but not loaded, try migration
            foreach (var kv in _registeredData)
            {
                if (!loadedKeys.Contains(kv.Key) && _migrationCallbacks.TryGetValue(kv.Key, out var callback))
                {
                    try
                    {
                        // Invoke using dynamic invoke or reflection
                        // Action<T> where T is unknown here? No, T was known at Register.
                        // But here we have object and Delegate.
                        // Delegate is Action<T>. Invoke(object) should work if covariant/contravariant? 
                        // Actually explicit Invoke via helper/dynamic might be needed.
                        // We can just use DynamicInvoke.
                        callback.DynamicInvoke(kv.Value);
                        MMLog.WriteInfo($"[SaveSystem] Migrated data for {kv.Key} in {_modId}");
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteWarning($"[SaveSystem] Migration failed for {kv.Key}: {ex.Message}");
                    }
                }
            }
        }
    }
}
