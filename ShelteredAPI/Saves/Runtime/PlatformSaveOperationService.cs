using System;
using ModAPI.Core;
using ShelteredAPI.Saves.Backups;

namespace ShelteredAPI.Saves.Runtime
{
    internal sealed class PlatformSaveOperationService
    {
        private readonly PlatformSave_Base _inner;

        internal PlatformSaveOperationService(PlatformSave_Base inner)
        {
            _inner = inner;
        }

        internal bool Save(SaveManager.SaveType type, byte[] data)
        {
            string slotName = type.ToString();
            MMLog.WriteInfo(string.Format("Saving triggered! Saving to {0}", slotName));

            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformSave.Enter", "type=" + type + ", bytes=" + (data != null ? data.Length.ToString() : "null"));
            }

            try
            {
                if (IsReservedSaveType(type))
                    return SaveReserved(type, data, slotName);

                if (ShouldSkipRedundantQuitSave(slotName))
                    return true;

                PlatformSaveProxy.Target target;
                if (SaveRuntimeState.TryGetPendingSave(type, out target) && target != null)
                    return SavePendingTarget(type, data, slotName, target);

                if (SaveRuntimeState.HasActiveCustomSessionFor(type))
                    return SaveActiveCustomSession(type, data, slotName);

                return SaveVanilla(type, data, slotName);
            }
            catch (Exception ex)
            {
                MMLog.WriteException(ex, "PlatformSaveProxy.PlatformSave");
                if (ModRuntime.IsQuitting)
                {
                    ModRuntime.MarkSaveExit("PlatformSave.Exception", ex.GetType().Name + ": " + ex.Message);
                }

                MMLog.Flush();
                throw;
            }
            finally
            {
                SaveBackupService.ClearCurrentSavePass();
            }
        }

        private bool SaveReserved(SaveManager.SaveType type, byte[] data, string slotName)
        {
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformSave.FallbackVanilla.Reserved", "type=" + type);
            }

            bool success = _inner.PlatformSave(type, data);
            if (success)
            {
                MMLog.WriteInfo(string.Format("Save finished {0} (vanilla reserved type)", slotName));
            }

            return success;
        }

        private bool SavePendingTarget(SaveManager.SaveType type, byte[] data, string slotName, PlatformSaveProxy.Target target)
        {
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformSave.Redirect", "saveId=" + target.saveId);
            }

            string scenarioId = SaveStorageRouter.NormalizeScenarioId(target.scenarioId);
            SaveEntry entry = SaveStorageRouter.Overwrite(scenarioId, target.saveId, new SaveOverwriteOptions(), data);
            if (entry == null)
            {
                MMLog.WriteError(string.Format("PlatformSaveProxy.PlatformSave: redirected save failed. proxySlot={0}, scenario={1}, saveId={2}", slotName, scenarioId, target.saveId));
                return false;
            }

            SaveStorageRouter.UpdateSlotManifest(scenarioId, entry.absoluteSlot, entry.saveInfo);
            SaveRuntimeState.SetActiveCustomSession(type, entry);
            SaveRuntimeState.ClearPendingSave(type);

            MMLog.WriteDebug(string.Format("Saved custom slot: {0} (scenario={1}, absoluteSlot={2})", entry.id, entry.scenarioId, entry.absoluteSlot));
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformSave.Redirect.Done", "entry=" + entry.id);
                SaveRuntimeStatus.MarkQuitSaveCompleted();
            }

            MMLog.WriteInfo(string.Format(
                "Save finished {0} (custom slot: {1}, scenario: {2})",
                slotName,
                entry.id ?? "unknown",
                entry.scenarioId ?? target.scenarioId ?? "unknown"));
            return true;
        }

        private bool SaveActiveCustomSession(SaveManager.SaveType type, byte[] data, string slotName)
        {
            SaveEntry active = SaveRuntimeState.ActiveCustomSave;
            SaveEntry result = SaveStorageRouter.Overwrite(active.scenarioId, active.id, new SaveOverwriteOptions(), data);
            if (result == null)
            {
                MMLog.WriteError(string.Format("PlatformSaveProxy.PlatformSave: active custom save overwrite failed. proxySlot={0}, scenario={1}, saveId={2}", slotName, active.scenarioId, active.id));
                return false;
            }

            SaveRuntimeState.SetActiveCustomSession(type, result);
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformSave.ActiveCustom.Done", "entry=" + result.id);
                SaveRuntimeStatus.MarkQuitSaveCompleted();
            }

            MMLog.WriteInfo(string.Format(
                "Save finished {0} (custom slot: {1}, scenario: {2}, absoluteSlot: {3})",
                slotName,
                result.id,
                result.scenarioId,
                result.absoluteSlot));
            return true;
        }

        private bool SaveVanilla(SaveManager.SaveType type, byte[] data, string slotName)
        {
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformSave.FallbackVanilla", "type=" + type);
            }

            SaveBackupService.BackupVanillaBeforeOverwrite(type);
            bool success = _inner.PlatformSave(type, data);
            if (success)
            {
                MMLog.WriteInfo(string.Format("Save finished {0} (vanilla)", slotName));
            }

            return success;
        }

        private bool ShouldSkipRedundantQuitSave(string slotName)
        {
            if (!ModRuntime.IsQuitting || !SaveRuntimeStatus.IsQuitSaveCompleted)
                return false;

            ModRuntime.MarkSaveExit("PlatformSave.Skip", "quit save already completed");
            MMLog.WriteInfo(string.Format("Save finished {0} (skipped - already completed)", slotName));
            return true;
        }

        private static bool IsReservedSaveType(SaveManager.SaveType type)
        {
            return type == SaveManager.SaveType.GlobalData || type == SaveManager.SaveType.Invalid;
        }
    }
}
