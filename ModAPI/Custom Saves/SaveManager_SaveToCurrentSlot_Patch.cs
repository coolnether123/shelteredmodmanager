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
            var slot = (SaveManager.SaveType)Traverse.Create(__instance).Field("m_slotInUse").GetValue<int>();
            
            if (PlatformSaveProxy.NextSave.ContainsKey(slot))
            {
                MMLog.WriteDebug($"Pending NEW GAME detected for {slot}. Proxy is injected and waiting.");
            }
            else if (PlatformSaveProxy.ActiveCustomSave != null)
            {
                MMLog.WriteDebug($"Active CUSTOM SESSION detected for {slot}. Proxy is injected and waiting.");
            }

            // 3. Return true to let vanilla logic run (which calls our Proxy)
            return true; 
        }
    }
}
