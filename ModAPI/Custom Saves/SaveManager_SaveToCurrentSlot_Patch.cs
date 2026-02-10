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
                CrashCorridorTracer.Mark("SaveToCurrentSlot.Prefix", "Entering save-to-slot while quitting");
            }

            // 2. Logging
            SaveManager.SaveType slot = SaveManager.SaveType.Invalid;
            try
            {
                slot = (SaveManager.SaveType)__instance.GetType()
                    .GetField("m_slotInUse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                    .GetValue(__instance);
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SaveManager_SaveToCurrentSlot_Patch] Failed to read m_slotInUse: " + ex.Message);
            }
            
            if (PlatformSaveProxy.NextSave.ContainsKey(slot))
            {
                MMLog.WriteDebug($"Pending NEW GAME detected for {slot}. Proxy is injected and waiting.");
                if (PluginRunner.IsQuitting) CrashCorridorTracer.Mark("SaveToCurrentSlot.Prefix", "Pending NEW GAME for " + slot);
            }
            else if (PlatformSaveProxy.ActiveCustomSave != null)
            {
                MMLog.WriteDebug($"Active CUSTOM SESSION detected for {slot}. Proxy is injected and waiting.");
                if (PluginRunner.IsQuitting) CrashCorridorTracer.Mark("SaveToCurrentSlot.Prefix", "Active custom session for " + slot);
            }

            // 3. Note: Even if IsQuitting is true, we let vanilla run.
            // The PlatformSaveProxy._quitSaveCompleted flag will prevent duplicate saves
            // and the crash that came from vanilla UI update code after the second save.

            // 4. Return true to let vanilla logic run
            return true; 
        }

        static void Postfix()
        {
            MMLog.WriteDebug("[SaveManager] SaveToCurrentSlot finished.");
            if (PluginRunner.IsQuitting)
            {
                CrashCorridorTracer.Mark("SaveToCurrentSlot.Postfix", "SaveToCurrentSlot finished");
            }
        }
    }
}
