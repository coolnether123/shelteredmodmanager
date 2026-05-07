using System;
using System.Collections.Generic;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Persistence
{
    /// <summary>
    /// Implementation of the per-mod save data persistence.
    /// Manages 'mods/{ModId}/data.json' within each save slot directory.
    /// </summary>
    internal class SaveSystemImpl : ISaveSystem
    {
        private static readonly List<SaveSystemImpl> _instances = new List<SaveSystemImpl>();
        private string _shutdownCache = null;
        private string _preparedLoadKey = null;
        private bool _afterLoadCallbacksApplied;
        private readonly HashSet<string> _loadedKeysForPreparedData = new HashSet<string>(StringComparer.Ordinal);

        private readonly Dictionary<string, object> _registeredData = new Dictionary<string, object>();
        private readonly Dictionary<string, Delegate> _migrationCallbacks = new Dictionary<string, Delegate>();
        private readonly string _modId;
        private readonly ModPersistenceStore _store;

        public SaveSystemImpl(string modId)
        {
            _modId = modId;
            _store = new ModPersistenceStore(modId);
            // Save blobs need to be available before scene saveables start deserializing.
            GameLifecycleSources.AddBeforeSave(HandleBeforeSave);
            GameLifecycleSources.AddBeforeLoadSceneContents(HandleBeforeLoadSceneContents);
            GameLifecycleSources.AddAfterLoad(HandleAfterLoad);
            _instances.Add(this);
        }

        public string GetCurrentSlotPath()
        {
            return SaveRuntimeAdapters.GetCurrentSlotPath();
        }

        public int ActiveSlotIndex
        {
            get { return SaveRuntimeAdapters.GetActiveSlotIndex(); }
        }

        public void RegisterModData<T>(string key, T data, Action<T> migrationCallback = null) where T : class
        {
            if (string.IsNullOrEmpty(key)) return;
            _registeredData[key] = data;
            if (migrationCallback != null) _migrationCallbacks[key] = migrationCallback;
        }

        public static void PrecalculateShutdownData()
        {
            MMLog.WriteDebug("[SaveSystem] Pre-calculating mod data for safe shutdown...");
            foreach (var sys in _instances)
            {
                sys.Precalculate();
            }
        }

        private void Precalculate()
        {
            try
            {
                _shutdownCache = _store.Serialize(_registeredData);
                MMLog.WriteDebug($"[SaveSystem] Buffered data for {_modId}");
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[SaveSystem] Failed to buffer data for {_modId}: {ex.Message}");
            }
        }

        private void HandleBeforeSave(object gameData)
        {
            try
            {
                MMLog.WriteDebug($"[SaveSystem] HandleBeforeSave for {_modId}. IsQuitting={PluginRunner.IsQuitting}.");

                IModSaveContext saveContext = SaveRuntimeAdapters.GetCurrentSaveContext();
                var rootPath = saveContext != null ? saveContext.SlotPath : GetCurrentSlotPath();
                if (string.IsNullOrEmpty(rootPath)) 
                {
                    MMLog.WriteDebug($"[SaveSystem] No active slot for {_modId}, skipping save.");
                    return;
                }

                if (saveContext == null)
                    saveContext = new ModSaveContext(rootPath, ActiveSlotIndex, null, null, null);

                var modFilePath = _store.GetCurrentFilePath(rootPath);
                string jsonToWrite;

                // CHECK FOR PRE-CALCULATED CACHE (Safety for Shutdown)
                if (!string.IsNullOrEmpty(_shutdownCache) && PluginRunner.IsQuitting)
                {
                    MMLog.WriteDebug($"[SaveSystem] Writing buffered shutdown data for {_modId} to {modFilePath}");
                    jsonToWrite = _shutdownCache;
                }
                else
                {
                    MMLog.WriteDebug($"[SaveSystem] Serializing live mod data for {_modId} to {modFilePath}");
                    
                    InvokeBeforeSaveHooks(saveContext);
                    jsonToWrite = _store.Serialize(_registeredData);
                }

                MMLog.WriteDebug($"[SaveSystem] Writing {jsonToWrite.Length} bytes to {modFilePath}");
                _store.Write(rootPath, jsonToWrite);

                MMLog.WriteDebug($"[SaveSystem] Successfully saved mod data for {_modId}");
            }
            catch (Exception ex)
            {
                MMLog.WriteError($"[SaveSystem] Critical error saving mod data for {_modId}: {ex.Message}");
            }
        }

        private void HandleBeforeLoadSceneContents(object data)
        {
            PrepareRegisteredDataForLoad();
        }

        private void HandleAfterLoad(object data)
        {
            PrepareRegisteredDataForLoad();
            ApplyAfterLoadCallbacks();
        }

        private void PrepareRegisteredDataForLoad()
        {
            IModSaveContext saveContext = SaveRuntimeAdapters.GetCurrentSaveContext();
            var rootPath = saveContext != null ? saveContext.SlotPath : GetCurrentSlotPath();
            if (string.IsNullOrEmpty(rootPath)) return;

            string loadKey = BuildLoadKey(saveContext, rootPath);
            if (string.Equals(_preparedLoadKey, loadKey, StringComparison.Ordinal)) return;

            _preparedLoadKey = loadKey;
            _afterLoadCallbacksApplied = false;
            _loadedKeysForPreparedData.Clear();

            var loadedKeys = new HashSet<string>();

            try
            {
                ModPersistenceLoadResult loadResult = _store.Load(rootPath);
                if (loadResult != null)
                {
                    if (loadResult.IsLegacy)
                        MMLog.WriteInfo($"[SaveSystem] Found legacy mod data for {_modId} in root folder. It will be moved to the nested 'mods' directory on next save.");

                    var container = loadResult.Data;
                    if (container != null && container.entries != null)
                    {
                        foreach (var entry in container.entries)
                        {
                            if (_registeredData.TryGetValue(entry.key, out var dataObj))
                            {
                                JsonUtility.FromJsonOverwrite(entry.json, dataObj);
                                loadedKeys.Add(entry.key);
                                _loadedKeysForPreparedData.Add(entry.key);
                            }
                        }
                        MMLog.WriteDebug(string.Format("[SaveSystem] Prepared mod data for {0} from {1}", _modId, loadResult.FileName));
                    }
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError(string.Format("[SaveSystem] Failed to prepare mod data for {0}: {1}", _modId, ex.Message));
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

        private void InvokeBeforeSaveHooks(IModSaveContext saveContext)
        {
            foreach (var kv in _registeredData)
            {
                if (kv.Value is IModPersistenceLogic)
                {
                    try
                    {
                        MMLog.WriteDebug($"[SaveSystem] Invoking OnSaving hook for {kv.Key} in {_modId}");
                        (kv.Value as IModPersistenceLogic).OnSaving(saveContext);
                    }
                    catch (Exception logicEx) { MMLog.WriteError($"[SaveSystem] {kv.Key}.OnSaving failed: {logicEx.Message}"); }
                }
            }
        }

        private void ApplyAfterLoadCallbacks()
        {
            if (_afterLoadCallbacksApplied) return;

            foreach (var key in _loadedKeysForPreparedData)
            {
                if (!_registeredData.TryGetValue(key, out var dataObj)) continue;

                var persistenceLogic = dataObj as IModPersistenceLogic;
                if (persistenceLogic == null) continue;

                try
                {
                    IModSaveContext saveContext = SaveRuntimeAdapters.GetCurrentSaveContext();
                    if (saveContext == null)
                    {
                        string rootPath = GetCurrentSlotPath();
                        if (!string.IsNullOrEmpty(rootPath))
                            saveContext = new ModSaveContext(rootPath, ActiveSlotIndex, null, null, null);
                    }

                    persistenceLogic.OnLoaded(saveContext);
                }
                catch (Exception logicEx)
                {
                    MMLog.WriteError($"[SaveSystem] {key}.OnLoaded failed: {logicEx.Message}");
                }
            }

            _afterLoadCallbacksApplied = true;
        }

        private static string BuildLoadKey(IModSaveContext saveContext, string rootPath)
        {
            if (saveContext == null)
                return rootPath ?? string.Empty;

            return string.Format(
                "{0}|{1}|{2}|{3}",
                saveContext.SaveScopeId ?? string.Empty,
                saveContext.SaveId ?? string.Empty,
                saveContext.SlotIndex,
                rootPath ?? string.Empty);
        }
    }
}

