using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ModAPI.Persistence
{
    /// <summary>
    /// Implementation of the per-mod save data persistence.
    /// Manages 'mods/{ModId}/data.json' within each save slot directory.
    /// </summary>
    internal class SaveSystemImpl : ISaveSystem
    {
        private static readonly List<SaveSystemImpl> _instances = new List<SaveSystemImpl>();

        private readonly Dictionary<string, object> _registeredData = new Dictionary<string, object>();
        private readonly Dictionary<string, string> _registeredDefaults = new Dictionary<string, string>();
        private readonly Dictionary<string, Delegate> _migrationCallbacks = new Dictionary<string, Delegate>();
        private readonly Dictionary<string, PreparedLoadState> _preparedLoadStates = new Dictionary<string, PreparedLoadState>();
        private readonly string _modId;
        private readonly ModPersistenceStore _store;

        private string _shutdownCache;
        private string _preparedLoadKey;
        private bool _afterLoadCallbacksApplied;
        private bool _reportedNoActiveLoadContext;

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
            CaptureRegisteredDefault(key, data);
            if (migrationCallback != null) _migrationCallbacks[key] = migrationCallback;
        }

        public static void PrecalculateShutdownData()
        {
            MMLog.WriteDebug("[SaveSystem] Pre-calculating mod data for safe shutdown...");
            foreach (SaveSystemImpl sys in _instances)
            {
                sys.Precalculate();
            }
        }

        private void Precalculate()
        {
            string serialized;
            HashSet<string> failedKeys;
            if (!TrySerializeRegisteredData("shutdown-buffer", out serialized, out failedKeys))
            {
                _shutdownCache = null;
                return;
            }

            _shutdownCache = serialized;
            MMLog.WriteDebug("[SaveSystem] Buffered data for " + _modId);
        }

        private void CaptureRegisteredDefault(string key, object data)
        {
            try
            {
                _registeredDefaults[key] = PersistenceFieldGraphSerializer.Serialize(data);
            }
            catch (Exception ex)
            {
                _registeredDefaults.Remove(key);
                MMLog.WriteWarning("[SaveSystem] Could not capture registered defaults for " + _modId + "/" + key + ": " + ex.Message);
            }
        }

        private void HandleBeforeSave(object gameData)
        {
            IModSaveContext saveContext = SaveRuntimeAdapters.GetCurrentSaveContext();
            string rootPath = saveContext != null ? saveContext.SlotPath : GetCurrentSlotPath();
            if (string.IsNullOrEmpty(rootPath))
            {
                ReportSaveSkippedBecauseNoContext();
                return;
            }

            if (saveContext == null)
                saveContext = new ModSaveContext(rootPath, ActiveSlotIndex, null, null, null);

            try
            {
                MMLog.WriteDebug("[SaveSystem] HandleBeforeSave for " + _modId + ". IsQuitting=" + PluginRunner.IsQuitting + ".");
                string modFilePath = _store.GetCurrentFilePath(rootPath);
                string jsonToWrite;
                HashSet<string> callbackFailures = new HashSet<string>(StringComparer.Ordinal);
                HashSet<string> preparedKeys = new HashSet<string>(StringComparer.Ordinal);
                bool usedShutdownBuffer = !string.IsNullOrEmpty(_shutdownCache) && PluginRunner.IsQuitting;

                if (usedShutdownBuffer)
                {
                    MMLog.WriteDebug("[SaveSystem] Writing buffered shutdown data for " + _modId + " to " + modFilePath);
                    jsonToWrite = _shutdownCache;
                }
                else
                {
                    MMLog.WriteDebug("[SaveSystem] Serializing live mod data for " + _modId + " to " + modFilePath);
                    InvokeBeforeSaveHooks(saveContext, preparedKeys, callbackFailures);

                    HashSet<string> failedSerializeKeys;
                    if (!TrySerializeRegisteredData("save", out jsonToWrite, out failedSerializeKeys))
                    {
                        ReportAbortedSaveDiagnostics(callbackFailures, preparedKeys, failedSerializeKeys);
                        return;
                    }
                }

                MMLog.WriteDebug("[SaveSystem] Writing " + jsonToWrite.Length + " bytes to " + modFilePath);
                _store.Write(rootPath, jsonToWrite);
                ReportSuccessfulSaveDiagnostics(callbackFailures, preparedKeys, usedShutdownBuffer);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SaveSystem] Critical error saving mod data for " + _modId + ": " + ex.Message);
            }
        }

        private void HandleBeforeLoadSceneContents(object data)
        {
            PrepareRegisteredDataForLoad();
        }

        private void HandleAfterLoad(object data)
        {
            if (PrepareRegisteredDataForLoad())
                ApplyAfterLoadCallbacks();
        }

        private bool PrepareRegisteredDataForLoad()
        {
            IModSaveContext saveContext = SaveRuntimeAdapters.GetCurrentSaveContext();
            string rootPath = saveContext != null ? saveContext.SlotPath : GetCurrentSlotPath();
            if (string.IsNullOrEmpty(rootPath))
            {
                ReportLoadSkippedBecauseNoContext();
                return false;
            }

            _reportedNoActiveLoadContext = false;

            string loadKey = BuildLoadKey(saveContext, rootPath);
            if (string.Equals(_preparedLoadKey, loadKey, StringComparison.Ordinal))
                return true;

            _preparedLoadKey = loadKey;
            _afterLoadCallbacksApplied = false;
            _preparedLoadStates.Clear();

            foreach (KeyValuePair<string, object> registration in _registeredData)
            {
                PreparedLoadState state = new PreparedLoadState();
                state.DefaultStateAvailable = RestoreRegisteredDefault(registration.Key, registration.Value, state);
                _preparedLoadStates[registration.Key] = state;
            }

            try
            {
                ModPersistenceLoadResult loadResult = _store.Load(rootPath);
                if (loadResult != null)
                {
                    if (loadResult.IsLegacy)
                    {
                        MMLog.WriteInfo("[SaveSystem] Found legacy mod data for " + _modId
                            + " in root folder. It will be moved to the nested 'mods' directory on next save.");
                    }

                    if (!string.IsNullOrEmpty(loadResult.DeserializeError))
                    {
                        foreach (PreparedLoadState state in _preparedLoadStates.Values)
                        {
                            state.FailedDeserialize = true;
                            state.Details.Add("container deserialize failed: " + loadResult.DeserializeError);
                        }
                    }

                    ModPersistenceData container = loadResult.Data;
                    if (container != null && container.entries != null)
                    {
                        foreach (ModDataEntry entry in container.entries)
                        {
                            object dataObject;
                            PreparedLoadState state;
                            if (!_registeredData.TryGetValue(entry.key, out dataObject)
                                || !_preparedLoadStates.TryGetValue(entry.key, out state))
                            {
                                continue;
                            }

                            try
                            {
                                PersistenceFieldGraphSerializer.DeserializeOverwrite(entry.json, dataObject);
                                state.Loaded = true;
                            }
                            catch (Exception ex)
                            {
                                state.FailedDeserialize = true;
                                state.Details.Add("entry deserialize failed: " + ex.Message);
                                state.DefaultStateAvailable = RestoreRegisteredDefault(entry.key, dataObject, state);
                            }
                        }

                        MMLog.WriteDebug("[SaveSystem] Prepared mod data for " + _modId + " from " + loadResult.FileName);
                    }
                }
            }
            catch (Exception ex)
            {
                foreach (PreparedLoadState state in _preparedLoadStates.Values)
                {
                    state.FailedDeserialize = true;
                    state.Details.Add("persistence read failed: " + ex.Message);
                }

                MMLog.WriteError("[SaveSystem] Failed to prepare mod data for " + _modId + ": " + ex.Message);
            }

            foreach (KeyValuePair<string, object> registration in _registeredData)
            {
                PreparedLoadState state = _preparedLoadStates[registration.Key];
                if (state.Loaded)
                    continue;

                state.Missing = true;
                Delegate callback;
                if (_migrationCallbacks.TryGetValue(registration.Key, out callback))
                {
                    try
                    {
                        callback.DynamicInvoke(registration.Value);
                        state.Migrated = true;
                        MMLog.WriteInfo("[SaveSystem] Migrated data for " + registration.Key + " in " + _modId);
                    }
                    catch (Exception ex)
                    {
                        state.CallbackFailure = true;
                        state.Details.Add("migration callback failed: " + ex.Message);
                        state.DefaultStateAvailable = RestoreRegisteredDefault(registration.Key, registration.Value, state);
                        MMLog.WriteWarning("[SaveSystem] Migration failed for " + registration.Key + ": " + ex.Message);
                    }
                }

                if (!state.Migrated && state.DefaultStateAvailable)
                    state.Defaulted = true;
            }

            return true;
        }

        private bool RestoreRegisteredDefault(string key, object dataObject, PreparedLoadState state)
        {
            string defaultJson;
            if (dataObject == null || !_registeredDefaults.TryGetValue(key, out defaultJson))
            {
                state.FailedDefaultRestore = true;
                state.Details.Add("registered default unavailable");
                return false;
            }

            try
            {
                PersistenceFieldGraphSerializer.DeserializeOverwrite(defaultJson, dataObject);
                return true;
            }
            catch (Exception ex)
            {
                state.FailedDefaultRestore = true;
                state.Details.Add("registered default restore failed: " + ex.Message);
                return false;
            }
        }

        private void InvokeBeforeSaveHooks(
            IModSaveContext saveContext,
            HashSet<string> preparedKeys,
            HashSet<string> callbackFailures)
        {
            foreach (KeyValuePair<string, object> registration in _registeredData)
            {
                IModPersistenceLifecycle lifecycle = registration.Value as IModPersistenceLifecycle;
                if (lifecycle != null)
                {
                    try
                    {
                        lifecycle.PrepareForSave(saveContext);
                        preparedKeys.Add(registration.Key);
                    }
                    catch (Exception ex)
                    {
                        callbackFailures.Add(registration.Key);
                        MMLog.WriteError("[SaveSystem] " + registration.Key + ".PrepareForSave failed: " + ex.Message);
                    }
                }

                IModPersistenceLogic legacyLogic = registration.Value as IModPersistenceLogic;
                if (legacyLogic != null)
                {
                    try
                    {
                        legacyLogic.OnSaving(saveContext);
                        preparedKeys.Add(registration.Key);
                    }
                    catch (Exception ex)
                    {
                        callbackFailures.Add(registration.Key);
                        MMLog.WriteError("[SaveSystem] " + registration.Key + ".OnSaving failed: " + ex.Message);
                    }
                }
            }
        }

        private void ApplyAfterLoadCallbacks()
        {
            if (_afterLoadCallbacksApplied) return;

            IModSaveContext saveContext = SaveRuntimeAdapters.GetCurrentSaveContext();
            if (saveContext == null)
            {
                string rootPath = GetCurrentSlotPath();
                if (!string.IsNullOrEmpty(rootPath))
                    saveContext = new ModSaveContext(rootPath, ActiveSlotIndex, null, null, null);
            }

            foreach (KeyValuePair<string, object> registration in _registeredData)
            {
                PreparedLoadState state;
                if (!_preparedLoadStates.TryGetValue(registration.Key, out state))
                    continue;

                IModPersistenceLogic legacyLogic = registration.Value as IModPersistenceLogic;
                if (state.Loaded && legacyLogic != null)
                {
                    try
                    {
                        legacyLogic.OnLoaded(saveContext);
                        state.LegacyRestoreApplied = true;
                    }
                    catch (Exception ex)
                    {
                        state.CallbackFailure = true;
                        state.Details.Add("OnLoaded failed: " + ex.Message);
                        MMLog.WriteError("[SaveSystem] " + registration.Key + ".OnLoaded failed: " + ex.Message);
                    }
                }

                bool hasRestorableData = state.Loaded || state.Migrated || state.Defaulted;
                IModPersistenceLifecycle lifecycle = registration.Value as IModPersistenceLifecycle;
                if (hasRestorableData && lifecycle != null)
                {
                    bool restoreSucceeded = false;
                    try
                    {
                        lifecycle.RestoreAfterLoad(saveContext);
                        state.RestoreApplied = true;
                        restoreSucceeded = true;
                    }
                    catch (Exception ex)
                    {
                        state.CallbackFailure = true;
                        state.Details.Add("RestoreAfterLoad failed: " + ex.Message);
                        MMLog.WriteError("[SaveSystem] " + registration.Key + ".RestoreAfterLoad failed: " + ex.Message);
                    }

                    if (restoreSucceeded)
                        ValidateAfterLoad(registration.Key, lifecycle, saveContext, state);
                }

                ReportLoadDiagnostic(registration.Key, state);
            }

            _afterLoadCallbacksApplied = true;
        }

        private void ValidateAfterLoad(
            string key,
            IModPersistenceLifecycle lifecycle,
            IModSaveContext saveContext,
            PreparedLoadState state)
        {
            try
            {
                string diagnosticMessage;
                if (lifecycle.ValidateAfterLoad(saveContext, out diagnosticMessage))
                {
                    state.ValidationPassed = true;
                    return;
                }

                state.ValidationFailed = true;
                state.Details.Add(string.IsNullOrEmpty(diagnosticMessage)
                    ? "validation failed"
                    : "validation failed: " + diagnosticMessage);
            }
            catch (Exception ex)
            {
                state.ValidationFailed = true;
                state.CallbackFailure = true;
                state.Details.Add("ValidateAfterLoad failed: " + ex.Message);
                MMLog.WriteError("[SaveSystem] " + key + ".ValidateAfterLoad failed: " + ex.Message);
            }
        }

        private bool TrySerializeRegisteredData(string phase, out string json, out HashSet<string> failedKeys)
        {
            HashSet<string> serializationFailures = new HashSet<string>(StringComparer.Ordinal);
            failedKeys = serializationFailures;

            try
            {
                json = _store.Serialize(
                    _registeredData,
                    delegate(string key, Exception ex)
                    {
                        serializationFailures.Add(key);
                        MMLog.WriteError("[SaveSystem] Save diagnostic mod=" + _modId + " key=" + key
                            + " status=failed-serialize phase=" + phase + " detail=" + ex.Message);
                    });
            }
            catch (Exception ex)
            {
                json = null;
                MMLog.WriteError("[SaveSystem] Failed to serialize persistence container for " + _modId + ": " + ex.Message);
                return false;
            }

            if (serializationFailures.Count > 0)
            {
                json = null;
                MMLog.WriteError("[SaveSystem] Serialization failed for " + serializationFailures.Count
                    + " key(s) in " + _modId + "; persistence file was not written.");
                return false;
            }

            return true;
        }

        private void ReportSaveSkippedBecauseNoContext()
        {
            foreach (string key in _registeredData.Keys)
            {
                MMLog.WriteInfo("[SaveSystem] Save diagnostic mod=" + _modId + " key=" + key
                    + " status=skipped-no-active-save-context");
            }
        }

        private void ReportLoadSkippedBecauseNoContext()
        {
            if (_reportedNoActiveLoadContext)
                return;

            foreach (string key in _registeredData.Keys)
            {
                MMLog.WriteInfo("[SaveSystem] Load diagnostic mod=" + _modId + " key=" + key
                    + " status=skipped-no-active-save-context");
            }

            _reportedNoActiveLoadContext = true;
        }

        private void ReportSuccessfulSaveDiagnostics(
            HashSet<string> callbackFailures,
            HashSet<string> preparedKeys,
            bool usedShutdownBuffer)
        {
            foreach (string key in _registeredData.Keys)
            {
                List<string> statuses = new List<string>();
                statuses.Add("saved");
                if (usedShutdownBuffer)
                    statuses.Add("shutdown-buffered");
                else if (preparedKeys.Contains(key))
                    statuses.Add("prepared");
                if (callbackFailures.Contains(key))
                    statuses.Add("callback-failure");

                WriteSaveDiagnostic(key, statuses);
            }
        }

        private void ReportAbortedSaveDiagnostics(
            HashSet<string> callbackFailures,
            HashSet<string> preparedKeys,
            HashSet<string> failedSerializeKeys)
        {
            foreach (string key in _registeredData.Keys)
            {
                List<string> statuses = new List<string>();
                statuses.Add(failedSerializeKeys.Contains(key) ? "failed-serialize" : "save-aborted");
                if (preparedKeys.Contains(key))
                    statuses.Add("prepared");
                if (callbackFailures.Contains(key))
                    statuses.Add("callback-failure");

                WriteSaveDiagnostic(key, statuses);
            }
        }

        private void WriteSaveDiagnostic(string key, List<string> statuses)
        {
            string message = "[SaveSystem] Save diagnostic mod=" + _modId + " key=" + key
                + " status=" + string.Join(",", statuses.ToArray());
            if (statuses.Contains("failed-serialize") || statuses.Contains("callback-failure"))
                MMLog.WriteWarning(message);
            else
                MMLog.WriteInfo(message);
        }

        private void ReportLoadDiagnostic(string key, PreparedLoadState state)
        {
            List<string> statuses = new List<string>();
            if (state.Loaded) statuses.Add("loaded");
            if (state.Missing) statuses.Add("missing");
            if (state.Migrated) statuses.Add("migrated");
            if (state.Defaulted) statuses.Add("defaulted");
            if (state.FailedDeserialize) statuses.Add("failed-deserialize");
            if (state.FailedDefaultRestore) statuses.Add("failed-default-restore");
            if (state.LegacyRestoreApplied) statuses.Add("legacy-restored");
            if (state.RestoreApplied) statuses.Add("restored");
            if (state.ValidationPassed) statuses.Add("validation-passed");
            if (state.ValidationFailed) statuses.Add("validation-failed");
            if (state.CallbackFailure) statuses.Add("callback-failure");
            if (statuses.Count == 0) statuses.Add("no-data-action");

            string message = "[SaveSystem] Load diagnostic mod=" + _modId + " key=" + key
                + " status=" + string.Join(",", statuses.ToArray());
            if (state.Details.Count > 0)
                message += " detail=" + string.Join("; ", state.Details.ToArray());

            if (state.FailedDeserialize || state.FailedDefaultRestore || state.ValidationFailed || state.CallbackFailure)
                MMLog.WriteWarning(message);
            else
                MMLog.WriteInfo(message);
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

        private sealed class PreparedLoadState
        {
            internal readonly List<string> Details = new List<string>();
            internal bool Loaded;
            internal bool Missing;
            internal bool Migrated;
            internal bool Defaulted;
            internal bool FailedDeserialize;
            internal bool FailedDefaultRestore;
            internal bool CallbackFailure;
            internal bool DefaultStateAvailable;
            internal bool LegacyRestoreApplied;
            internal bool RestoreApplied;
            internal bool ValidationPassed;
            internal bool ValidationFailed;
        }
    }
}
