using System;
using System.IO;
using System.Text;
using HarmonyLib;
using ModAPI.Saves;
using UnityEngine;

namespace ModAPI.Hooks
{
    public class PlatformSaveProxy : PlatformSave_Base
    {
        private class Target { public string scenarioId; public string saveId; }
        private readonly System.Collections.Generic.Dictionary<SaveManager.SaveType, Target> _nextLoad = new System.Collections.Generic.Dictionary<SaveManager.SaveType, Target>();
        private readonly System.Collections.Generic.Dictionary<SaveManager.SaveType, Target> _nextSave = new System.Collections.Generic.Dictionary<SaveManager.SaveType, Target>();

        private readonly PlatformSave_Base _inner;
        private string _customLoadedXml;
        private SaveEntry _activeCustomSave;

        public PlatformSaveProxy(PlatformSave_Base inner)
        {
            _inner = inner;
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
            if (_activeCustomSave != null)
            {
                try
                {
                    MMLog.WriteDebug($"PlatformSaveProxy: Overwriting active custom save '{_activeCustomSave.scenarioId}/{_activeCustomSave.id}'");

                    ISaveApi saveApi = ExpandedVanillaSaves.IsStandardScenario(_activeCustomSave.scenarioId)
                        ? ExpandedVanillaSaves.Instance
                        : ScenarioSaves.GetRegistry(_activeCustomSave.scenarioId);

                    var after = saveApi.Overwrite(_activeCustomSave.id, null, data);
                    if (after != null) _activeCustomSave = after;
                    return true;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("PlatformSaveProxy active save error: " + ex);
                    return false;
                }
            }

            Target nextSaveTarget;
            if (_nextSave.TryGetValue(type, out nextSaveTarget))
            {
                try
                {
                    MMLog.WriteDebug($"PlatformSaveProxy: Saving new custom save for slot={type}, target='{nextSaveTarget.scenarioId}/{nextSaveTarget.saveId}'");

                    ISaveApi saveApi = ExpandedVanillaSaves.IsStandardScenario(nextSaveTarget.scenarioId)
                        ? ExpandedVanillaSaves.Instance
                        : ScenarioSaves.GetRegistry(nextSaveTarget.scenarioId);

                    var after = saveApi.Overwrite(nextSaveTarget.saveId, null, data);
                    if (after != null) _activeCustomSave = after;
                    _nextSave.Remove(type);
                    return true;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("PlatformSaveProxy new save error: " + ex);
                    return false;
                }
            }

            MMLog.WriteDebug($"PlatformSaveProxy: No custom context. Passing save for slot={type} to vanilla handler.");
            return _inner.PlatformSave(type, data);
        }

        public override bool PlatformLoad(SaveManager.SaveType type)
        {
            Target nextLoadTarget;
            if (_nextLoad.TryGetValue(type, out nextLoadTarget))
            {
                try
                {
                    var scenarioId = nextLoadTarget.scenarioId;
                    var saveId = nextLoadTarget.saveId;
                    MMLog.WriteDebug($"PlatformSaveProxy: Loading custom save for slot={type}, target='{scenarioId}/{saveId}'");

                    ISaveApi saveApi = ExpandedVanillaSaves.IsStandardScenario(scenarioId)
                        ? ExpandedVanillaSaves.Instance
                        : ScenarioSaves.GetRegistry(scenarioId);

                    var entry = saveApi.Get(saveId);

                    if (entry == null)
                    {
                        MMLog.WriteError($"PlatformSaveProxy: Could not find SaveEntry for '{scenarioId}/{saveId}'");
                        return false;
                    }

                    var path = DirectoryProvider.EntryPath(scenarioId, saveId);
                    if (!File.Exists(path))
                    {
                        MMLog.WriteError($"PlatformSaveProxy: Save file does not exist at '{path}'");
                        return false;
                    }

                    _customLoadedXml = File.ReadAllText(path);
                    _activeCustomSave = entry;
                    _nextLoad.Remove(type);
                    return true;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("PlatformSaveProxy custom load error: " + ex);
                    _customLoadedXml = null;
                    _activeCustomSave = null;
                    return false;
                }
            }

            MMLog.WriteDebug($"PlatformSaveProxy: No custom context. Passing load for slot={type} to vanilla handler.");
            _activeCustomSave = null;
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

        public void SetNextLoad(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            _nextLoad[type] = new Target { scenarioId = scenarioId, saveId = saveId };
        }

        public void SetNextSave(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            _nextSave[type] = new Target { scenarioId = scenarioId, saveId = saveId };
        }
    }

    [HarmonyPatch(typeof(SaveManager), "Awake")]
    internal static class SaveManager_Awake_ProxyPatch
    {
        static void Postfix(SaveManager __instance)
        {
            try
            {
                var traverse = Traverse.Create(__instance);
                var inner = __instance.platformSave;
                if (!(inner is PlatformSaveProxy))
                {
                    var proxy = new PlatformSaveProxy(inner);
                    traverse.Field("m_saveScript").SetValue(proxy);
                    MMLog.WriteDebug("PlatformSaveProxy installed.");
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("Failed to install PlatformSaveProxy: " + ex);
            }
        }
    }
}