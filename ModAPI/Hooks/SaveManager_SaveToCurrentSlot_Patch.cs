using HarmonyLib;
using ModAPI.Saves;
using System;
using System.Linq;

namespace ModAPI.Hooks
{
    [HarmonyPatch(typeof(SaveManager), "SaveToCurrentSlot")]
    internal static class SaveManager_SaveToCurrentSlot_Patch
    {
        static bool Prefix(SaveManager __instance, bool alsoSaveGlobalDataOnPs4)
        {
            // Check if we are in a custom save session
            if (PlatformSaveProxy.ActiveCustomSave != null)
            {
                MMLog.Write("[SaveToCurrentSlot_Patch] Intercepting custom save for active session.");
                try
                {
                    // Gather save data (mimic SaveManager's internal process)
                    var saveData = new SaveData();
                    var saveables = Traverse.Create(__instance).Field("m_saveables").GetValue<System.Collections.Generic.List<ISaveable>>();
                    MMLog.Write($"[SaveToCurrentSlot_Patch] Found {saveables.Count} ISaveable objects to process.");

                    foreach (var saveable in saveables)
                    {
                        if (saveable != null)
                        {
                            saveData.StartSaveable();
                            saveable.SaveLoad(saveData);
                        }
                    }
                    saveData.Finished();
                    byte[] bytes = saveData.GetBytes();

                    // Call our centralized PlatformSaveProxy.PlatformSave to handle the actual file writing and manifest update
                    // We pass the SaveType from the current slot, as the game expects it.
                    bool success = ((PlatformSaveProxy)__instance.platformSave).PlatformSave((SaveManager.SaveType)Traverse.Create(__instance).Field("m_slotInUse").GetValue<int>(), bytes);

                    MMLog.Write($"[SaveToCurrentSlot_Patch] Custom save operation complete. Success: {success}.");

                    // Prevent the original SaveToCurrentSlot from running
                    return false;
                }
                catch (Exception ex)
                {
                    MMLog.WriteError("[SaveToCurrentSlot_Patch] CRITICAL error during custom save interception: " + ex);
                    return true; // Allow original to run on error
                }
            }

            // If no custom save is active, allow the original method to run for vanilla saves
            MMLog.Write("[SaveToCurrentSlot_Patch] No custom save active. Allowing vanilla save.");
            return true;
        }
    }
}
