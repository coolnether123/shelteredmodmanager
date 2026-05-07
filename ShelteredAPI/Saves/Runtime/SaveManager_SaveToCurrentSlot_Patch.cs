using HarmonyLib;
using ShelteredAPI.Saves;
using System;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Core;

namespace ShelteredAPI.Saves.Runtime
{
    [PatchPolicy(PatchDomain.SaveFlow, "SaveToCurrentSlotRedirect",
        TargetBehavior = "Pending custom-slot save redirect before vanilla SaveToCurrentSlot execution",
        FailureMode = "New-game or custom-slot saves can target the wrong underlying slot.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the current-slot redirect patch.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(SaveManager), "SaveToCurrentSlot")]
    internal static class SaveManager_SaveToCurrentSlot_Patch
    {
        static bool Prefix(SaveManager __instance)
        {
            // 1. FORCE INJECTION NOW
            // If the Awake patch was missed, this line saves the day.
            SaveManager_Injection_Patch.Inject(__instance);
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("SaveToCurrentSlot.Prefix", "Entering save-to-slot while quitting");
            }

            // 2. Logging - Use reflection to get the slot since currentSlot doesn't exist
            SaveManager.SaveType slot = SaveManager.SaveType.Invalid;
            try
            {
                var field = __instance.GetType().GetField("m_slotInUse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    slot = (SaveManager.SaveType)field.GetValue(__instance);
                }
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SaveManager_SaveToCurrentSlot_Patch] Failed to read m_slotInUse: " + ex.Message);
            }

            // Must check for pending redirect first (from new game or slot selection flow)
            if (SaveRuntimeState.HasPendingSave(slot))
            {
                // We have a pending custom REDIRECT. Let the proxy handle this in PlatformSave.
                // The redirect target has already been queued by SlotSelectionPanel, MainMenuPanel, etc.
                // Just return true and let vanilla code call into the proxy.
                if (ModRuntime.IsQuitting) ModRuntime.MarkSaveExit("SaveToCurrentSlot.Prefix", "Pending NEW GAME for " + slot);
                return true;
            }

            if (SaveRuntimeState.ActiveCustomSave != null && SaveRuntimeState.ActiveCustomSave.absoluteSlot == (int)slot)
            {
                if (ModRuntime.IsQuitting) ModRuntime.MarkSaveExit("SaveToCurrentSlot.Prefix", "Active custom session for " + slot);
            }

            // 3. Return true to let vanilla logic run
            return true;
        }

        static void Postfix()
        {
            if (ModRuntime.IsQuitting)
            {
                ModRuntime.MarkSaveExit("SaveToCurrentSlot.Postfix", "SaveToCurrentSlot finished");
            }
        }
    }
}
