using HarmonyLib;
using ModAPI.Saves;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using ModAPI.Core;

namespace ModAPI.Hooks
{
    public class PlatformSaveProxy : PlatformSave_Base
    {
        public class Target { public string scenarioId; public string saveId; }

        private static readonly object _nextLoadLock = new object();
        private static readonly object _nextSaveLock = new object();
        
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

        public static bool NextSaveTargetExists() => NextSave.Count > 0;
        public static KeyValuePair<SaveManager.SaveType, Target> GetNextSaveTargetAndClear()
        {
            var target = NextSave.First();
            NextSave.Clear();
            return target;
        }

        public override bool IsSaving() => _inner.IsSaving();
        public override bool IsLoading() => _inner.IsLoading();
        public override bool IsDeleting() => _inner.IsDeleting();
        public override bool WasSaveError() => _inner.WasSaveError();
        public override void DoesSaveExist(SaveManager.SaveType type, out bool exists, out bool corrupted) => _inner.DoesSaveExist(type, out exists, out corrupted);
        public override void PlatformInit() => _inner.PlatformInit();
        public override void PlatformUpdate() => _inner.PlatformUpdate();
        public override bool PlatformDelete(SaveManager.SaveType type) => _inner.PlatformDelete(type);


        public override bool PlatformSave(SaveManager.SaveType type, byte[] data)
        {
            if (PluginRunner.IsQuitting)
            {
                CrashCorridorTracer.Mark("PlatformSave.Enter", "type=" + type + ", bytes=" + (data != null ? data.Length.ToString() : "null"));
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
                        CrashCorridorTracer.Mark("PlatformSave.Skip", "quit save already completed");
                    }
                    return true; // Tell vanilla "save succeeded" without doing anything
                }

                // 1. CHECK FOR NEW GAME OR SLOT SWAP
                lock (_nextSaveLock)
                {
                    if (NextSave.TryGetValue(type, out var target))
                    {
                        MMLog.WriteDebug($"Intercepting Vanilla Save ({type}) -> Redirecting to Custom ID: {target.saveId}");
                        if (PluginRunner.IsQuitting)
                        {
                            CrashCorridorTracer.Mark("PlatformSave.Redirect", "saveId=" + target.saveId);
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
                        ActiveCustomSave = entry;
                        
                        // Clear the "Next" target so we don't get stuck
                        NextSave.Remove(type); 
                        
                        if (entry != null)
                            MMLog.WriteDebug($"Saved custom slot: {entry.id}");
                        if (PluginRunner.IsQuitting)
                        {
                            CrashCorridorTracer.Mark("PlatformSave.Redirect.Done", entry != null ? ("entry=" + entry.id) : "entry=null");
                        }

                        if (PluginRunner.IsQuitting) _quitSaveCompleted = true;
                        return true; // We handled it
                    }
                }

                // 2. CHECK FOR EXISTING LOADED CUSTOM GAME
                if (ActiveCustomSave != null)
                {
                    // Update the file and metadata
                    // FORCE SYNC: Uses File.WriteAllBytes
                    var result = ExpandedVanillaSaves.Instance.Overwrite(ActiveCustomSave.id, new SaveOverwriteOptions(), data);
                    
                    if (result != null)
                    {
                        ActiveCustomSave = result;
                        MMLog.WriteDebug($"Saved custom slot: {ActiveCustomSave.id}");
                    }
                    if (PluginRunner.IsQuitting)
                    {
                        CrashCorridorTracer.Mark("PlatformSave.ActiveCustom.Done", result != null ? ("entry=" + result.id) : "result=null");
                    }

                    if (PluginRunner.IsQuitting) _quitSaveCompleted = true;
                    return true; // We handled it
                }

                // 3. FALLBACK TO VANILLA
                // This happens if the user selected Slot 1/2/3 normally
                if (PluginRunner.IsQuitting)
                {
                    CrashCorridorTracer.Mark("PlatformSave.FallbackVanilla", "type=" + type);
                }
                return _inner.PlatformSave(type, data);
            }
            catch (Exception ex)
            {
                MMLog.WriteException(ex, "PlatformSaveProxy.PlatformSave");
                if (PluginRunner.IsQuitting)
                {
                    CrashCorridorTracer.Mark("PlatformSave.Exception", ex.GetType().Name + ": " + ex.Message);
                }
                MMLog.Flush();
                throw;
            }
        }

        public override bool PlatformLoad(SaveManager.SaveType type)
        {
            lock (_nextLoadLock)
            {
                if (NextLoad.TryGetValue(type, out var nextLoadTarget))
                {
                    try
                    {
                        var scenarioId = nextLoadTarget.scenarioId;
                        var saveId = nextLoadTarget.saveId;

                        ISaveApi saveApi = ExpandedVanillaSaves.IsStandardScenario(scenarioId)
                            ? ExpandedVanillaSaves.Instance
                            : ScenarioSaves.GetRegistry(scenarioId);

                        var entry = saveApi.Get(saveId);
                        if (entry == null) return false;

                        var path = DirectoryProvider.EntryPath(scenarioId, entry.absoluteSlot);
                        if (!File.Exists(path)) return false;

                        _customLoadedXml = File.ReadAllText(path);
                        ActiveCustomSave = entry;
                        NextLoad.Remove(type);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError("custom load error: " + ex);
                        _customLoadedXml = null;
                        ActiveCustomSave = null;
                        return false;
                    }
                }
            }

            MMLog.WriteDebug($"No custom load target. Passing load for slot={type} to vanilla handler.");
            if (PluginRunner.IsQuitting)
            {
                CrashCorridorTracer.Mark("PlatformLoad.FallbackVanilla", "type=" + type);
            }
            ActiveCustomSave = null;
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

            lock (_nextLoadLock)
            {
                NextLoad[type] = new Target { scenarioId = scenarioId, saveId = saveId };
            }
        }

        public static void SetNextSave(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            lock (_nextSaveLock)
            {
                NextSave[type] = new Target { scenarioId = scenarioId, saveId = saveId };
            }
        }
        public static void ResetStatus()
        {
            _quitSaveCompleted = false;
        }
    }
}
