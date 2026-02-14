using HarmonyLib;
using ModAPI.Saves;
using System;
using System.Linq;
using ModAPI.Core;

namespace ModAPI.Hooks
{
    [HarmonyPatch(typeof(SaveManager), "SaveToCurrentSlot")]
    internal static class SaveManager_SaveToCurrentSlot_Patch
    {
        static bool Prefix(SaveManager __instance)
        {
            // 1. FORCE INJECTION NOW
            // If the Awake patch was missed, this line saves the day.
            SaveManager_Injection_Patch.Inject(__instance);
            if (PluginRunner.IsQuitting)
            {
                SaveExitTracker.Mark("SaveToCurrentSlot.Prefix", "Entering save-to-slot while quitting");
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
            lock (PlatformSaveProxy._nextSaveLock)
            {
                if (PlatformSaveProxy.NextSave.ContainsKey(slot))
                {
                    // We have a pending custom REDIRECT. Let the proxy handle this in PlatformSave.
                    // The redirect target has already been queued by SlotSelectionPanel, MainMenuPanel, etc.
                    // Just return true and let vanilla code call into the proxy.
                    if (PluginRunner.IsQuitting) SaveExitTracker.Mark("SaveToCurrentSlot.Prefix", "Pending NEW GAME for " + slot);
                    return true;
                }
            }

            if (PlatformSaveProxy.ActiveCustomSave != null && PlatformSaveProxy.ActiveCustomSave.absoluteSlot == (int)slot)
            {
                if (PluginRunner.IsQuitting) SaveExitTracker.Mark("SaveToCurrentSlot.Prefix", "Active custom session for " + slot);
            }

            // 3. Return true to let vanilla logic run
            return true;
        }

        static void Postfix()
        {
            if (PluginRunner.IsQuitting)
            {
                SaveExitTracker.Mark("SaveToCurrentSlot.Postfix", "SaveToCurrentSlot finished");
            }
        }
    }
}
