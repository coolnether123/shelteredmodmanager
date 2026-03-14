using HarmonyLib;
using ModAPI.Saves;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Hooks
{
    public class PlatformSaveProxy : PlatformSave_Base
    {
        public class Target { public string scenarioId; public string saveId; }

        public static readonly object _nextLoadLock = new object();
        public static readonly object _nextSaveLock = new object();
        
        public static readonly Dictionary<SaveManager.SaveType, Target> NextLoad = new Dictionary<SaveManager.SaveType, Target>();
        public static readonly Dictionary<SaveManager.SaveType, Target> NextSave = new Dictionary<SaveManager.SaveType, Target>();
        public static SaveEntry ActiveCustomSave;
        private static bool _quitSaveCompleted = false; // Tracks if we've already saved during quit sequence

        private readonly PlatformSave_Base _inner;
        private string _customLoadedXml;

        public PlatformSaveProxy(PlatformSave_Base inner)
        {
            _inner = inner;
        }

        public static bool NextSaveTargetExists()
        {
            return SaveRuntimeState.HasAnyPendingSave();
        }

        public static KeyValuePair<SaveManager.SaveType, Target> GetNextSaveTargetAndClear()
        {
            return SaveRuntimeState.GetNextSaveTargetAndClear();
        }

        public override bool IsSaving() => _inner.IsSaving();
        public override bool IsLoading() => _inner.IsLoading();
        public override bool IsDeleting() => _inner.IsDeleting();
        public override bool WasSaveError() => _inner.WasSaveError();
        public override void DoesSaveExist(SaveManager.SaveType type, out bool exists, out bool corrupted) => _inner.DoesSaveExist(type, out exists, out corrupted);
        public override void PlatformInit() => _inner.PlatformInit();
        public override void PlatformUpdate() => _inner.PlatformUpdate();
        public override bool PlatformDelete(SaveManager.SaveType type)
        {
            bool routedResult;
            if (SaveDeleteRouter.TryDeleteBySaveType(type, out routedResult))
            {
                return routedResult;
            }

            return _inner.PlatformDelete(type);
        }


        public override bool PlatformSave(SaveManager.SaveType type, byte[] data)
        {
            // Regular log: High-level save notification
            string slotName = type.ToString();
            MMLog.WriteInfo($"Saving triggered! Saving to {slotName}");
            
            if (PluginRunner.IsQuitting)
            {
                SaveExitTracker.Mark("PlatformSave.Enter", "type=" + type + ", bytes=" + (data != null ? data.Length.ToString() : "null"));
            }
            try
            {
                // CRASH FIX: During quit, the save system can be triggered multiple times.
                // If we've already completed a save during this quit sequence, skip redundant saves
                // to avoid accessing destroyed objects in the vanilla code that runs after.
                if (PluginRunner.IsQuitting && _quitSaveCompleted)
                {
                    if (PluginRunner.IsQuitting)
                    {
                        SaveExitTracker.Mark("PlatformSave.Skip", "quit save already completed");
                    }
                    MMLog.WriteInfo($"Save finished {slotName} (skipped - already completed)");
                    return true; // Tell vanilla "save succeeded" without doing anything
                }

                // 1. CHECK FOR NEW GAME OR SLOT SWAP
                Target target;
                if (SaveRuntimeState.TryGetPendingSave(type, out target) && target != null)
                {
                    if (PluginRunner.IsQuitting)
                    {
                        SaveExitTracker.Mark("PlatformSave.Redirect", "saveId=" + target.saveId);
                    }

                    // FORCE SYNC: ExpandedVanillaSaves.Instance.Overwrite uses File.WriteAllBytes (blocking).
                    // This ensures the file is flushed to disk before we return.
                    var entry = ExpandedVanillaSaves.Instance.Overwrite(target.saveId, new SaveOverwriteOptions(), data);

                    // Create Manifest immediately for new saves
                    if (entry != null)
                    {
                        var registry = (SaveRegistryCore)ExpandedVanillaSaves.Instance;
                        registry.UpdateSlotManifest(entry.absoluteSlot, entry.saveInfo);
                    }

                    // Set this as the active save for the rest of the session
                    SaveRuntimeState.SetActiveCustomSession(type, entry);

                    // Clear the "Next" target so we don't get stuck
                    SaveRuntimeState.ClearPendingSave(type);

                    if (entry != null)
                        MMLog.WriteDebug($"Saved custom slot: {entry.id}");
                    if (PluginRunner.IsQuitting)
                    {
                        SaveExitTracker.Mark("PlatformSave.Redirect.Done", entry != null ? ("entry=" + entry.id) : "entry=null");
                    }

                    if (PluginRunner.IsQuitting) _quitSaveCompleted = true;

                    // Regular log: Save complete
                    MMLog.WriteInfo($"Save finished {slotName} (custom slot: {entry?.id ?? "unknown"})");
                    return true; // We handled it
                }

                // 2. CHECK FOR EXISTING LOADED CUSTOM GAME
                if (SaveRuntimeState.HasActiveCustomSave)
                {
                    // Update the file and metadata
                    // FORCE SYNC: Uses File.WriteAllBytes
                    var active = SaveRuntimeState.ActiveCustomSave;
                    var result = ExpandedVanillaSaves.Instance.Overwrite(active.id, new SaveOverwriteOptions(), data);
                    
                    if (result != null)
                    {
                        SaveRuntimeState.SetActiveCustomSession(type, result);
                    }
                    if (PluginRunner.IsQuitting)
                    {
                        SaveExitTracker.Mark("PlatformSave.ActiveCustom.Done", result != null ? ("entry=" + result.id) : "result=null");
                    }

                    if (PluginRunner.IsQuitting) _quitSaveCompleted = true;
                    
                    // Regular log: Save complete
                    MMLog.WriteInfo($"Save finished {slotName} (custom slot: {SaveRuntimeState.ActiveCustomSave.id})");
                    return true; // We handled it
                }

                // 3. FALLBACK TO VANILLA
                // This happens if the user selected Slot 1/2/3 normally
                if (PluginRunner.IsQuitting)
                {
                    SaveExitTracker.Mark("PlatformSave.FallbackVanilla", "type=" + type);
                }
                bool success = _inner.PlatformSave(type, data);
                if (success)
                {
                    MMLog.WriteInfo($"Save finished {slotName} (vanilla)");
                }
                return success;
            }
            catch (Exception ex)
            {
                MMLog.WriteException(ex, "PlatformSaveProxy.PlatformSave");
                if (PluginRunner.IsQuitting)
                {
                    SaveExitTracker.Mark("PlatformSave.Exception", ex.GetType().Name + ": " + ex.Message);
                }
                MMLog.Flush();
                throw;
            }
        }

        public override bool PlatformLoad(SaveManager.SaveType type)
        {
            Target nextLoadTarget;
            if (SaveRuntimeState.TryGetPendingLoad(type, out nextLoadTarget) && nextLoadTarget != null)
            {
                try
                {
                    var scenarioId = nextLoadTarget.scenarioId;
                    var saveId = nextLoadTarget.saveId;

                    ISaveApi saveApi = ExpandedVanillaSaves.IsStandardScenario(scenarioId)
                        ? ExpandedVanillaSaves.Instance
                        : ScenarioSaves.GetRegistry(scenarioId);

                    var entry = saveApi.Get(saveId);
                    if (entry == null)
                    {
                        MMLog.WriteWarning(string.Format("[PlatformLoad] Pending custom target missing: scenario={0}, saveId={1}. Clearing redirect for {2}.", scenarioId, saveId, type));
                        SaveRuntimeState.ClearPendingLoad(type);
                        _customLoadedXml = null;
                        SaveRuntimeState.ClearActiveCustomSession();
                        return false;
                    }

                    var path = DirectoryProvider.EntryPath(scenarioId, entry.absoluteSlot);
                    if (!File.Exists(path))
                    {
                        MMLog.WriteWarning(string.Format("[PlatformLoad] Pending custom save file missing: {0}. Clearing redirect for {1}.", path, type));
                        SaveRuntimeState.ClearPendingLoad(type);
                        _customLoadedXml = null;
                        SaveRuntimeState.ClearActiveCustomSession();
                        return false;
                    }

                    _customLoadedXml = File.ReadAllText(path);
                    SaveRuntimeState.SetActiveCustomSession(type, entry);
                    SaveRuntimeState.ClearPendingLoad(type);
                    return true;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("custom load error: " + ex);
                    SaveRuntimeState.ClearPendingLoad(type);
                    _customLoadedXml = null;
                    SaveRuntimeState.ClearActiveCustomSession();
                    return false;
                }
            }

            MMLog.WriteDebug($"No custom load target. Passing load for slot={type} to vanilla handler.");
            if (PluginRunner.IsQuitting)
            {
                SaveExitTracker.Mark("PlatformLoad.FallbackVanilla", "type=" + type);
            }
            SaveRuntimeState.ClearActiveCustomSession();
            return _inner.PlatformLoad(type);
        }

        public override bool PlatformGetLoadedData(out byte[] data)
        {
            if (!string.IsNullOrEmpty(_customLoadedXml))
            {
                data = Encoding.UTF8.GetBytes(_customLoadedXml);
                _customLoadedXml = null;
                return true;
            }
            return _inner.PlatformGetLoadedData(out data);
        }

        public static void SetNextLoad(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            
            // Safety: Ensure proxy is injected before we register a pending load
            try { SaveManager_Injection_Patch.Inject(SaveManager.instance); } catch { }
            SaveRuntimeState.SetPendingLoad(type, scenarioId, saveId);
        }

        public static void SetNextSave(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            SaveRuntimeState.SetPendingSave(type, scenarioId, saveId);
        }
        public static void ResetStatus()
        {
            _quitSaveCompleted = false;
        }
    }
}
