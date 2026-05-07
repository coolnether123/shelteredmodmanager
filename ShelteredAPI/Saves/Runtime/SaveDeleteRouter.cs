using System;
using System.Collections.Generic;
using ModAPI.Core;
using ShelteredAPI.Core;
using ShelteredAPI.Saves;

namespace ShelteredAPI.Saves.Runtime
{
    /// <summary>
    /// Centralized delete routing for save slots.
    /// Ensures custom-session deletes target the active custom absolute slot, not proxy vanilla slots.
    /// </summary>
    internal static class SaveDeleteRouter
    {
        internal static bool TryDeleteBySaveType(SaveManager.SaveType requestedType, out bool result)
        {
            result = false;

            if (!IsProxyVanillaSlot(requestedType))
            {
                return false;
            }

            var active = SaveRuntimeState.ActiveCustomSave;
            if (!SaveRuntimeState.HasActiveCustomSessionFor(requestedType) ||
                active == null ||
                active.absoluteSlot <= 0 ||
                (ExpandedVanillaSaves.IsStandardScenario(active.scenarioId) && active.absoluteSlot <= 3))
            {
                return false;
            }

            MMLog.WriteInfo(string.Format(
                "[SaveDeleteRouter] Redirecting delete for {0} to custom absolute slot {1} (saveId={2}).",
                requestedType,
                active.absoluteSlot,
                active.id ?? "unknown"));

            string scenarioId = active != null && !string.IsNullOrEmpty(active.scenarioId)
                ? active.scenarioId
                : "Standard";
            result = DeleteAbsoluteSlot(scenarioId, active.absoluteSlot, "PlatformDelete.RedirectFromActiveCustom");
            if (result)
            {
                ClearProxyStateAfterDelete(requestedType, active.id);
            }
            return true;
        }

        internal static bool DeleteAbsoluteSlot(int absoluteSlot, string reason)
        {
            return DeleteAbsoluteSlot("Standard", absoluteSlot, reason);
        }

        internal static bool DeleteAbsoluteSlot(string scenarioId, int absoluteSlot, string reason)
        {
            if (absoluteSlot <= 0)
            {
                MMLog.WriteWarning(string.Format("[SaveDeleteRouter] Refusing delete for invalid absolute slot: {0}. Reason={1}", absoluteSlot, reason ?? "unknown"));
                return false;
            }

            try
            {
                string storageScenarioId = SaveStorageRouter.NormalizeScenarioId(scenarioId);
                bool deleted = SaveStorageRouter.DeleteBySlot(storageScenarioId, absoluteSlot);
                MMLog.WriteInfo(string.Format("[SaveDeleteRouter] Delete scenario={0} slot {1} result={2}. Reason={3}",
                    storageScenarioId, absoluteSlot, deleted, reason ?? "unknown"));
                return deleted;
            }
            catch (Exception ex)
            {
                MMLog.WriteError(string.Format("[SaveDeleteRouter] DeleteAbsoluteSlot failed for scenario={0} slot {1}. Reason={2}. Error={3}",
                    scenarioId ?? "Standard", absoluteSlot, reason ?? "unknown", ex));
                return false;
            }
        }

        private static void ClearProxyStateAfterDelete(SaveManager.SaveType requestedType, string deletedSaveId)
        {
            if (SaveRuntimeState.ActiveCustomSave != null && SaveRuntimeState.ActiveCustomSave.id == deletedSaveId)
            {
                SaveRuntimeState.ClearActiveCustomSession();
            }

            SaveRuntimeState.ClearTrackedReferences(requestedType, deletedSaveId);
        }

        private static bool IsProxyVanillaSlot(SaveManager.SaveType type)
        {
            return type == SaveManager.SaveType.Slot1 ||
                   type == SaveManager.SaveType.Slot2 ||
                   type == SaveManager.SaveType.Slot3 ||
                   type == SaveManager.SaveType.SlotSurrounded ||
                   type == SaveManager.SaveType.SlotStasis;
        }
    }
}
