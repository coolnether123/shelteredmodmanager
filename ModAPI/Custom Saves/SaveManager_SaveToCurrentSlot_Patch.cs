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
            // We just log for debugging. We MUST return true to let the game
            // set its internal flags (isSaving = true), otherwise the UI hangs.
            // The actual interception happens inside PlatformSaveProxy now.
            
            var slot = (SaveManager.SaveType)Traverse.Create(__instance).Field("m_slotInUse").GetValue<int>();
            
            if (PlatformSaveProxy.NextSave.ContainsKey(slot) || PlatformSaveProxy.ActiveCustomSave != null)
            {
                 MMLog.Write($"[SaveToCurrentSlot] Custom context detected for {slot}. Passing execution to Proxy.");
            }

            return true; 
        }
    }
}
