using HarmonyLib;
using ModAPI.Saves;
using System;

namespace ModAPI.Hooks
{
    [HarmonyPatch(typeof(SaveManager), "SaveGlobalData")]
    internal static class SaveManager_SaveGlobalData_Patch
    {
        static bool Prefix(SaveManager __instance)
        {
            MMLog.Write("[SaveGlobalData_Patch] TRIGGERED.");
            // If we are currently in a custom save session
            if (PlatformSaveProxy.ActiveCustomSave != null)
            {
                MMLog.Write("[SaveGlobalData_Patch] Intercepting in-game save for active custom save.");
                try
                {
                    var saveData = new SaveData();
                    var saveables = Traverse.Create(__instance).Field("m_saveables").GetValue<System.Collections.Generic.List<ISaveable>>();
                    MMLog.Write($"[SaveGlobalData_Patch] Found {saveables.Count} ISaveable objects to process.");

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

                    // Overwrite the active custom save with the new data
                    var updatedEntry = ExpandedVanillaSaves.Instance.Overwrite(PlatformSaveProxy.ActiveCustomSave.id, null, bytes);
                    if (updatedEntry != null)
                    {
                        PlatformSaveProxy.ActiveCustomSave = updatedEntry;
                        MMLog.Write("[SaveGlobalData_Patch] Successfully updated custom save file and manifest.");
                    }
                    else
                    {
                        MMLog.WriteError("[SaveGlobalData_Patch] Overwrite operation failed.");
                    }

                    // Prevent the original SaveGlobalData from running
                    return false; 
                }
                catch(Exception ex)
                {
                    MMLog.WriteError("[SaveGlobalData_Patch] CRITICAL error during manual save process: " + ex);
                    return true; // run original on error
                }
            }

            // If not in a custom save session, let the original method run
            return true;
        }
    }
}
