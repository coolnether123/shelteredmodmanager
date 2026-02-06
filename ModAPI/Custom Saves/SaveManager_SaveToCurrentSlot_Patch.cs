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

            // 2. Logging
            var slot = (SaveManager.SaveType)__instance.GetType().GetField("m_slotInUse", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetValue(__instance);
            
            if (PlatformSaveProxy.NextSave.ContainsKey(slot))
            {
                MMLog.WriteDebug($"Pending NEW GAME detected for {slot}. Proxy is injected and waiting.");
            }
            else if (PlatformSaveProxy.ActiveCustomSave != null)
            {
                MMLog.WriteDebug($"Active CUSTOM SESSION detected for {slot}. Proxy is injected and waiting.");
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
        }
    }
}
