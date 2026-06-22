using System;
using System.IO;
using System.Text;
using ModAPI.Core;

namespace ShelteredAPI.Saves.Runtime
{
    internal sealed class PlatformLoadOperationService
    {
        private readonly PlatformSave_Base _inner;
        private string _customLoadedXml;

        internal PlatformLoadOperationService(PlatformSave_Base inner)
        {
            _inner = inner;
        }

        internal bool Load(SaveManager.SaveType type)
        {
            PlatformSaveProxy.Target nextLoadTarget;
            if (SaveRuntimeState.TryGetPendingLoad(type, out nextLoadTarget) && nextLoadTarget != null)
            {
                return LoadCustomTarget(type, nextLoadTarget);
            }

            MMLog.WriteDebug(string.Format("No custom load target. Passing load for slot={0} to vanilla handler.", type));
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformLoad.FallbackVanilla", "type=" + type);
            }

            SaveRuntimeState.ClearActiveCustomSession();
            return _inner.PlatformLoad(type);
        }

        internal bool GetLoadedData(out byte[] data)
        {
            if (!string.IsNullOrEmpty(_customLoadedXml))
            {
                data = Encoding.UTF8.GetBytes(_customLoadedXml);
                _customLoadedXml = null;
                return true;
            }

            return _inner.PlatformGetLoadedData(out data);
        }

        private bool LoadCustomTarget(SaveManager.SaveType type, PlatformSaveProxy.Target target)
        {
            try
            {
                string scenarioId = SaveStorageRouter.NormalizeScenarioId(target.scenarioId);
                string saveId = target.saveId;

                SaveEntry entry = SaveStorageRouter.Get(scenarioId, saveId);
                if (entry == null)
                {
                    MMLog.WriteWarning(string.Format("[PlatformLoad] Pending custom target missing: scenario={0}, saveId={1}. Clearing redirect for {2}.", scenarioId, saveId, type));
                    ClearFailedCustomLoad(type);
                    return false;
                }

                string path = DirectoryProvider.EntryPath(scenarioId, entry.absoluteSlot);
                if (!File.Exists(path))
                {
                    MMLog.WriteWarning(string.Format("[PlatformLoad] Pending custom save file missing: {0}. Clearing redirect for {1}.", path, type));
                    ClearFailedCustomLoad(type);
                    return false;
                }

                _customLoadedXml = File.ReadAllText(path);
                VanillaSaveRoute mirrorRoute;
                bool isMirroredVanilla = SaveRuntimeState.TryConsumePendingMirroredVanillaLoad(type, out mirrorRoute)
                    || SaveRegistryCore.TryGetStandardVanillaMirrorRoute(type, scenarioId, entry, out mirrorRoute);

                if (isMirroredVanilla)
                    SaveRuntimeState.SetActiveMirroredVanillaSession(type, entry, mirrorRoute);
                else
                    SaveRuntimeState.SetActiveCustomSession(type, entry);

                SaveRuntimeState.ClearPendingLoad(type);
                MMLog.WriteInfo(string.Format("[PlatformLoad] Loaded redirected save. proxySlot={0}, scenario={1}, saveId={2}, absoluteSlot={3}", type, scenarioId, saveId, entry.absoluteSlot));
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("custom load error: " + ex);
                ClearFailedCustomLoad(type);
                return false;
            }
        }

        private void ClearFailedCustomLoad(SaveManager.SaveType type)
        {
            SaveRuntimeState.ClearPendingLoad(type);
            _customLoadedXml = null;
            SaveRuntimeState.ClearActiveCustomSession();
        }
    }
}
