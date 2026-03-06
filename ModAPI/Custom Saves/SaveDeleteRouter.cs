using System;
using System.Collections.Generic;
using ModAPI.Core;
using ModAPI.Saves;

namespace ModAPI.Hooks
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
            if (active == null || active.absoluteSlot <= 0)
            {
                return false;
            }

            MMLog.WriteInfo(string.Format(
                "[SaveDeleteRouter] Redirecting delete for {0} to custom absolute slot {1} (saveId={2}).",
                requestedType,
                active.absoluteSlot,
                active.id ?? "unknown"));

            result = DeleteAbsoluteSlot(active.absoluteSlot, "PlatformDelete.RedirectFromActiveCustom");
            if (result)
            {
                ClearProxyStateAfterDelete(requestedType, active.id);
            }
            return true;
        }

        internal static bool DeleteAbsoluteSlot(int absoluteSlot, string reason)
        {
            if (absoluteSlot <= 0)
            {
                MMLog.WriteWarning(string.Format("[SaveDeleteRouter] Refusing delete for invalid absolute slot: {0}. Reason={1}", absoluteSlot, reason ?? "unknown"));
                return false;
            }

            try
            {
                bool deleted = ExpandedVanillaSaves.DeleteBySlot(absoluteSlot);
                MMLog.WriteInfo(string.Format("[SaveDeleteRouter] Delete slot {0} result={1}. Reason={2}", absoluteSlot, deleted, reason ?? "unknown"));
                return deleted;
            }
            catch (Exception ex)
            {
                MMLog.WriteError(string.Format("[SaveDeleteRouter] DeleteAbsoluteSlot failed for slot {0}. Reason={1}. Error={2}", absoluteSlot, reason ?? "unknown", ex));
                return false;
            }
        }

        private static void ClearProxyStateAfterDelete(SaveManager.SaveType requestedType, string deletedSaveId)
        {
            if (SaveRuntimeState.ActiveCustomSave != null && SaveRuntimeState.ActiveCustomSave.id == deletedSaveId)
            {
                SaveRuntimeState.ActiveCustomSave = null;
            }

            SaveRuntimeState.ClearTrackedReferences(requestedType, deletedSaveId);
        }

        private static bool IsProxyVanillaSlot(SaveManager.SaveType type)
        {
            return type == SaveManager.SaveType.Slot1 ||
                   type == SaveManager.SaveType.Slot2 ||
                   type == SaveManager.SaveType.Slot3;
        }
    }
}
