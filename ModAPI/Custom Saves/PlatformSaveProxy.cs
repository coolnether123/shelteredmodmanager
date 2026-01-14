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
            MMLog.WriteDebug($"[PlatformSaveProxy] PlatformSave called for {type}");

            // 1. CHECK FOR NEW GAME OR SLOT SWAP
            lock (_nextSaveLock)
            {
                if (NextSave.TryGetValue(type, out var target))
                {
                    MMLog.Write($"[Proxy] Intercepting Vanilla Save ({type}) -> Redirecting to Custom ID: {target.saveId}");
                    
                    // This writes the file to ModAPI/Saves/Standard/... AND parses metadata
                    var entry = ExpandedVanillaSaves.Instance.Overwrite(target.saveId, null, data);
                    
                    // Set this as the active save for the rest of the session
                    ActiveCustomSave = entry;
                    
                    // Clear the "Next" target so we don't get stuck
                    NextSave.Remove(type); 
                    
                    return true; // We handled it, don't let vanilla run
                }
            }

            // 2. CHECK FOR EXISTING LOADED CUSTOM GAME
            if (ActiveCustomSave != null)
            {
                MMLog.Write($"[Proxy] Saving Active Custom Game: {ActiveCustomSave.id}");
                
                // Update the file and metadata
                ActiveCustomSave = ExpandedVanillaSaves.Instance.Overwrite(ActiveCustomSave.id, null, data);
                
                return true; // We handled it
            }

            // 3. FALLBACK TO VANILLA
            // This happens if the user selected Slot 1/2/3 normally
            return _inner.PlatformSave(type, data);
        }

        public override bool PlatformLoad(SaveManager.SaveType type)
        {
            MMLog.WriteDebug($"[PlatformSaveProxy] Intercepted PlatformLoad call for SaveType: {type}.");

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
                        MMLog.Write($"[PlatformSaveProxy] Set active custom save to ID: {entry?.id} after loading.");
                        NextLoad.Remove(type);
                        return true;
                    }
                    catch (Exception ex)
                    {
                        MMLog.WriteError("PlatformSaveProxy custom load error: " + ex);
                        _customLoadedXml = null;
                        ActiveCustomSave = null;
                        return false;
                    }
                }
            }

            MMLog.WriteDebug($"[PlatformSaveProxy] No custom load target. Passing load for slot={type} to vanilla handler.");
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
            MMLog.WriteDebug($"[PlatformSaveProxy] SetNextLoad: type={type}, scenarioId={scenarioId}, saveId={saveId}");
            lock (_nextLoadLock)
            {
                NextLoad[type] = new Target { scenarioId = scenarioId, saveId = saveId };
            }
        }

        public static void SetNextSave(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            MMLog.WriteDebug($"[PlatformSaveProxy] SetNextSave: type={type}, scenarioId={scenarioId}, saveId={saveId}");
            lock (_nextSaveLock)
            {
                NextSave[type] = new Target { scenarioId = scenarioId, saveId = saveId };
            }
        }
    }
}