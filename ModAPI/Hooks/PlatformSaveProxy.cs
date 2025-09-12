using HarmonyLib;
using ModAPI.Saves;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace ModAPI.Hooks
{
    public class PlatformSaveProxy : PlatformSave_Base
    {
        public class Target { public string scenarioId; public string saveId; }

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
            MMLog.WriteDebug($"[PlatformSaveProxy] Intercepted PlatformSave call for SaveType: {type}.");

            // Case 1: A new custom save is being created.
            Target nextSaveTarget;
            if (NextSave.TryGetValue(type, out nextSaveTarget))
            {
                MMLog.WriteDebug($"[PlatformSaveProxy] Action: Creating new custom save '{nextSaveTarget.saveId}'.");
                try
                {
                    var updatedEntry = ExpandedVanillaSaves.Instance.Overwrite(nextSaveTarget.saveId, null, data);
                    ActiveCustomSave = updatedEntry; // Set the active save for the session
                    NextSave.Remove(type); // Consume the pending save
                    MMLog.Write("[PlatformSaveProxy] New custom save created successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("[PlatformSaveProxy] CRITICAL error during new save creation: " + ex);
                    return false;
                }
            }

            // Case 2: An existing custom save is being overwritten (in-game save).
            if (ActiveCustomSave != null)
            {
                MMLog.WriteDebug($"[PlatformSaveProxy] Action: Overwriting active custom save '{ActiveCustomSave.id}'.");
                try
                {
                    var after = ExpandedVanillaSaves.Instance.Overwrite(ActiveCustomSave.id, null, data);
                    if (after != null) ActiveCustomSave = after;
                    MMLog.Write("[PlatformSaveProxy] Active custom save overwritten successfully.");
                    return true;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("PlatformSaveProxy active save error: " + ex);
                    return false;
                }
            }

            // Case 3: Not a custom save operation, pass to vanilla handler.
            MMLog.WriteDebug($"[PlatformSaveProxy] No custom context. Passing save for slot={type} to vanilla handler.");
            return _inner.PlatformSave(type, data);
        }

        public override bool PlatformLoad(SaveManager.SaveType type)
        {
            MMLog.WriteDebug($"[PlatformSaveProxy] Intercepted PlatformLoad call for SaveType: {type}.");

            Target nextLoadTarget;
            if (NextLoad.TryGetValue(type, out nextLoadTarget))
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

                    var path = DirectoryProvider.EntryPath(scenarioId, saveId);
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
            NextLoad[type] = new Target { scenarioId = scenarioId, saveId = saveId };
        }

        public static void SetNextSave(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            MMLog.WriteDebug($"[PlatformSaveProxy] SetNextSave: type={type}, scenarioId={scenarioId}, saveId={saveId}");
            NextSave[type] = new Target { scenarioId = scenarioId, saveId = saveId };
        }
    }
}