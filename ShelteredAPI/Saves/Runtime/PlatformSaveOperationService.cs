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
            SaveRuntimeState.Target operationPendingTarget = null;
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

                SaveRuntimeState.Target target;
                bool hasPendingSave = SaveRuntimeState.TryGetPendingSave(type, out target) && target != null;
                if (hasPendingSave)
                {
                    operationPendingTarget = target;
                    return SavePendingTarget(type, data, slotName, target);
                }

                SaveRuntimeState.MirroredVanillaSession mirroredSession;
                if (SaveRuntimeState.TryGetActiveMirroredVanillaSessionFor(type, out mirroredSession))
                    return SaveActiveMirroredVanillaSession(type, data, slotName, mirroredSession);

                if (SaveRuntimeState.HasActiveCustomSessionFor(type))
                    return SaveActiveCustomSession(type, data, slotName);

                return SaveVanilla(type, data, slotName);
            }
            catch (Exception ex)
            {
                MMLog.WriteException(ex, "PlatformSaveProxy.PlatformSave");
                if (!IsReservedSaveType(type) && operationPendingTarget != null)
                {
                    bool clearedPending = SaveRuntimeState.ClearPendingSaveIfMatches(type, operationPendingTarget);
                    MMLog.WriteWarning(string.Format(
                        "PlatformSaveProxy.PlatformSave: cleared pending save redirect after exception. proxySlot={0}, scenario={1}, saveId={2}, cleared={3}",
                        slotName,
                        SaveStorageRouter.NormalizeScenarioId(operationPendingTarget.ScenarioId),
                        operationPendingTarget.SaveId,
                        clearedPending));
                }

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
                if (!IsReservedSaveType(type))
                    SaveRuntimeState.ClearCurrentSaveOperation(type);
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

        private bool SavePendingTarget(SaveManager.SaveType type, byte[] data, string slotName, SaveRuntimeState.Target target)
        {
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformSave.Redirect", "saveId=" + target.SaveId);
            }

            string scenarioId = SaveStorageRouter.NormalizeScenarioId(target.ScenarioId);
            SaveEntry entry = SaveStorageRouter.Overwrite(scenarioId, target.SaveId, new SaveOverwriteOptions(), data);
            if (entry == null)
            {
                MMLog.WriteError(string.Format("PlatformSaveProxy.PlatformSave: redirected save failed. proxySlot={0}, scenario={1}, saveId={2}", slotName, scenarioId, target.SaveId));
                bool clearedPending = SaveRuntimeState.ClearPendingSaveIfMatches(type, target);
                MMLog.WriteWarning(string.Format("PlatformSaveProxy.PlatformSave: cleared failed pending save redirect. proxySlot={0}, scenario={1}, saveId={2}, cleared={3}", slotName, scenarioId, target.SaveId, clearedPending));
                return false;
            }

            SaveStorageRouter.UpdateSlotManifest(scenarioId, entry.absoluteSlot, entry.saveInfo);
            SaveRuntimeState.SetActiveCustomSession(type, entry);
            SaveRuntimeState.ClearPendingSaveIfMatches(type, target);

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
                entry.scenarioId ?? target.ScenarioId ?? "unknown"));
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

        private bool SaveActiveMirroredVanillaSession(
            SaveManager.SaveType type,
            byte[] data,
            string slotName,
            SaveRuntimeState.MirroredVanillaSession session)
        {
            SaveEntry active = session != null && session.Entry != null
                ? session.Entry
                : SaveRuntimeState.ActiveCustomSave;
            if (active == null)
            {
                MMLog.WriteError(string.Format("PlatformSaveProxy.PlatformSave: mirrored vanilla save failed because active entry was missing. proxySlot={0}", slotName));
                return false;
            }

            if (!SaveBackupService.BackupVanillaBeforeOverwrite(type))
            {
                MMLog.WriteError("Mirrored vanilla save cancelled because its recovery snapshot failed.");
                return false;
            }

            string scenarioId = SaveStorageRouter.NormalizeScenarioId(active.scenarioId);
            SaveEntry result = SaveStorageRouter.Overwrite(scenarioId, active.id, new SaveOverwriteOptions(), data);
            if (result == null)
            {
                MMLog.WriteError(string.Format("PlatformSaveProxy.PlatformSave: mirrored XML overwrite failed. proxySlot={0}, scenario={1}, saveId={2}", slotName, scenarioId, active.id));
                return false;
            }

            byte[] vanillaBytes = data != null ? (byte[])data.Clone() : null;
            bool vanillaSuccess = _inner.PlatformSave(type, vanillaBytes);
            if (!vanillaSuccess)
            {
                MMLog.WriteError(string.Format("PlatformSaveProxy.PlatformSave: mirrored vanilla write failed. proxySlot={0}, scenario={1}, saveId={2}", slotName, scenarioId, active.id));
                SaveRuntimeState.SetActiveMirroredVanillaSession(type, result, session.Route);
                return false;
            }

            SaveRegistryCore.TryWriteStandardVanillaMirrorManifestFromSave(
                session.Route.VanillaSlotNumber,
                result.saveInfo,
                data);
            SaveRuntimeState.SetActiveMirroredVanillaSession(type, result, session.Route);

            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformSave.MirroredVanilla.Done", "entry=" + result.id);
                SaveRuntimeStatus.MarkQuitSaveCompleted();
            }

            MMLog.WriteInfo(string.Format(
                "Save finished {0} (mirrored vanilla slot: {1}, scenario: {2}, absoluteSlot: {3}, vanillaSlot: {4})",
                slotName,
                result.id,
                result.scenarioId,
                result.absoluteSlot,
                session.Route.VanillaSlotNumber));
            return true;
        }

        private bool SaveVanilla(SaveManager.SaveType type, byte[] data, string slotName)
        {
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("PlatformSave.FallbackVanilla", "type=" + type);
            }

            if (!SaveBackupService.BackupVanillaBeforeOverwrite(type))
            {
                MMLog.WriteError("Vanilla save cancelled because its recovery snapshot failed.");
                return false;
            }
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
