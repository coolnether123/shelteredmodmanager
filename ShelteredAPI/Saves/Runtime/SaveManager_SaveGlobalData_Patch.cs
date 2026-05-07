using HarmonyLib;
using System;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Core;

namespace ShelteredAPI.Saves.Runtime
{
    [PatchPolicy(PatchDomain.SaveFlow, "SaveGlobalDataCustomSession",
        TargetBehavior = "Global-data save propagation into the active custom session",
        FailureMode = "Custom save sessions miss global-data updates or drift from manifest state.",
        RollbackStrategy = "Disable the SaveFlow patch domain or remove the custom global-data patch.",
        StartupTiming = PatchStartupTiming.SaveFlowCritical)]
    [HarmonyPatch(typeof(SaveManager), "SaveGlobalData")]
    internal static class SaveManager_SaveGlobalData_Patch
    {
        static bool Prefix(SaveManager __instance)
        {
            MMLog.WriteDebug("[SaveGlobalData_Patch] SaveGlobalData prefix invoked.");
            // If we are currently in a custom save session
            if (SaveRuntimeState.ActiveCustomSave != null)
            {
                MMLog.WriteDebug("[SaveGlobalData_Patch] Intercepting in-game save for active custom save.");
                try
                {
                    var saveData = new SaveData();
                    var saveables = Traverse.Create(__instance).Field("m_saveables").GetValue<System.Collections.Generic.List<ISaveable>>();
                    MMLog.WriteDebug($"[SaveGlobalData_Patch] Found {saveables.Count} ISaveable objects to process.");

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
                    SaveEntry active = SaveRuntimeState.ActiveCustomSave;
                    string scenarioId = active != null
                        ? SaveStorageRouter.NormalizeScenarioId(active.scenarioId)
                        : "Standard";
                    var updatedEntry = SaveStorageRouter.Overwrite(scenarioId, active.id, null, bytes);
                    if (updatedEntry != null)
                    {
                        SaveRuntimeState.ActiveCustomSave = updatedEntry;
                        MMLog.Write("[SaveGlobalData_Patch] Successfully updated custom save file and manifest. scenario=" + scenarioId
                            + " saveId=" + updatedEntry.id + " absoluteSlot=" + updatedEntry.absoluteSlot + ".");
                    }
                    else
                    {
                        MMLog.WriteError("[SaveGlobalData_Patch] Overwrite operation failed. scenario=" + scenarioId
                            + " saveId=" + (active != null ? active.id : "<null>") + ".");
                    }

                    // Let vanilla SaveGlobalData run so global preferences (audio/language/input/etc.)
                    // are persisted as expected.
                    return true;
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
