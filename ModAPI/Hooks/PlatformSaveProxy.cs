using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using HarmonyLib;
using ModAPI.Saves;
using UnityEngine;

namespace ModAPI.Hooks
{
    // Wraps the platform save so we can serve custom data when a reserved slot is used
    public class PlatformSaveProxy : PlatformSave_Base
    {
        private class Target { public string scenarioId; public string saveId; }
        private readonly PlatformSave_Base _inner;
        private readonly Dictionary<SaveManager.SaveType, Target> _nextLoad = new Dictionary<SaveManager.SaveType, Target>();
        private readonly Dictionary<SaveManager.SaveType, Target> _nextSave = new Dictionary<SaveManager.SaveType, Target>();
        private string _customLoadedXml;

        public PlatformSaveProxy(PlatformSave_Base inner)
        {
            _inner = inner;
        }

        public override bool IsSaving() => _inner != null && _inner.IsSaving();
        public override bool IsLoading() => _inner != null && _inner.IsLoading();
        public override bool IsDeleting() => _inner != null && _inner.IsDeleting();
        public override bool WasSaveError() => _inner != null && _inner.WasSaveError();
        public override void DoesSaveExist(SaveManager.SaveType type, out bool exists, out bool corrupted)
        { _inner.DoesSaveExist(type, out exists, out corrupted); }
        public override void PlatformInit() { if (_inner != null) _inner.PlatformInit(); }

        public override bool PlatformSave(SaveManager.SaveType type, byte[] data)
        {
            if (IsReserved(type))
            {
                Target t;
                if (_nextSave.TryGetValue(type, out t))
                {
                    try
                    {
                        var scenarioId = t.scenarioId; var saveId = t.saveId;
                        MMLog.WriteDebug($"PlatformSaveProxy.PlatformSave reserved type={type}, scenario={scenarioId}, save={saveId}");
                        string xml = CustomSaveRegistry.DecodeToXml(data);
                        var bytes = Encoding.UTF8.GetBytes(xml);
                        var before = CustomSaveRegistry.GetSave(scenarioId, saveId);
                        if (before != null) Events.RaiseBeforeSave(before);
                        var after = CustomSaveRegistry.OverwriteSave(scenarioId, saveId, null, bytes);
                        if (after != null) Events.RaiseAfterSave(after);
                        // Ensure preview auto-capture is active
                        PreviewAuto.EnsureHooked();
                        _nextSave.Remove(type);
                        MMLog.WriteDebug("PlatformSaveProxy.PlatformSave completed for custom entry");
                        return true; // simulate success; do not write vanilla slot
                    }
                    catch (Exception ex)
                    {
                        MMLog.Write("PlatformSaveProxy custom save error: " + ex.Message);
                        return false;
                    }
                }
            }
            return _inner.PlatformSave(type, data);
        }

        public override bool PlatformLoad(SaveManager.SaveType type)
        {
            if (IsReserved(type))
            {
                Target t;
                if (_nextLoad.TryGetValue(type, out t))
                {
                    try
                    {
                        var scenarioId = t.scenarioId; var saveId = t.saveId;
                        MMLog.WriteDebug($"PlatformSaveProxy.PlatformLoad reserved type={type}, scenario={scenarioId}, save={saveId}");
                        var path = DirectoryProvider.EntryPath(scenarioId, saveId);
                        var entry = CustomSaveRegistry.GetSave(scenarioId, saveId);
                        if (entry != null) Events.RaiseBeforeLoad(entry);
                        if (!File.Exists(path)) return false;
                        _customLoadedXml = File.ReadAllText(path);
                        if (entry != null) Events.RaiseAfterLoad(entry);
                        _nextLoad.Remove(type);
                        MMLog.WriteDebug("PlatformSaveProxy.PlatformLoad provided custom xml bytes");
                        return true; // loaded
                    }
                    catch (Exception ex)
                    {
                        MMLog.Write("PlatformSaveProxy custom load error: " + ex.Message);
                        _customLoadedXml = null;
                        return false;
                    }
                }
            }
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

        public override bool PlatformDelete(SaveManager.SaveType type)
        { return _inner.PlatformDelete(type); }

        public override void PlatformUpdate() { if (_inner != null) _inner.PlatformUpdate(); }

        private static bool IsReserved(SaveManager.SaveType type)
        {
            int physical = -1;
            if (type == SaveManager.SaveType.Slot1) physical = 1;
            else if (type == SaveManager.SaveType.Slot2) physical = 2;
            else if (type == SaveManager.SaveType.Slot3) physical = 3;
            else return false;
            var r = SlotReservationManager.GetSlotReservation(physical);
            return r.usage == SaveSlotUsage.CustomScenario && !string.IsNullOrEmpty(r.scenarioId);
        }

        public void SetNextLoad(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            _nextLoad[type] = new Target{ scenarioId = scenarioId, saveId = saveId };
            MMLog.WriteDebug($"PlatformSaveProxy.SetNextLoad type={type} scenario={scenarioId} id={saveId}");
        }

        public void SetNextSave(SaveManager.SaveType type, string scenarioId, string saveId)
        {
            _nextSave[type] = new Target{ scenarioId = scenarioId, saveId = saveId };
            MMLog.WriteDebug($"PlatformSaveProxy.SetNextSave type={type} scenario={scenarioId} id={saveId}");
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
                var inner = __instance.platformSave; // existing instance
                if (!(inner is PlatformSaveProxy))
                {
                    var proxy = new PlatformSaveProxy(inner);
                    traverse.Field("m_saveScript").SetValue(proxy);
                    MMLog.WriteDebug("PlatformSaveProxy installed.");
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("Failed to install PlatformSaveProxy: " + ex.Message);
            }
        }
    }
}
