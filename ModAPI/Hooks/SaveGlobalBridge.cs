using System;
using HarmonyLib;
using ModAPI.Saves;

namespace ModAPI.Hooks
{
    [HarmonyPatch(typeof(SaveGlobal), "SaveLoad")]
    internal static class SaveGlobal_SaveLoad_Bridge
    {
        static void Postfix(SaveGlobal __instance, SaveData data)
        {
            try
            {
                if (data == null) return;
                // Persist or read SlotReservation map under Global/ModAPI.SaveSlots
                if (data.isSaving)
                {
                    data.GroupStart("Global");
                    data.GroupStart("ModAPI.SaveSlots");
                    var map = SlotReservationManager.Load();
                    int version = map.version;
                    data.SaveLoad("version", ref version);
                    // We store exactly 3 slots for simplicity
                    for (int i = 1; i <= 3; i++)
                    {
                        var r = SlotReservationManager.GetSlotReservation(i);
                        string keyUsage = "slot" + i + ".usage";
                        string keyScenario = "slot" + i + ".scenario";
                        int usage = (int)r.usage;
                        data.SaveLoad(keyUsage, ref usage);
                        string scenarioId = r.scenarioId ?? string.Empty;
                        data.SaveLoad(keyScenario, ref scenarioId);
                    }
                    data.GroupEnd();
                    data.GroupEnd();
                    MMLog.WriteDebug("SaveGlobalBridge: wrote reservation map to global save");
                }
                else if (data.isLoading)
                {
                    // Try read, else ignore
                    try
                    {
                        data.GroupStart("Global");
                        data.GroupStart("ModAPI.SaveSlots");
                        int version = 1; data.SaveLoad("version", ref version);
                        for (int i = 1; i <= 3; i++)
                        {
                            string keyUsage = "slot" + i + ".usage";
                            string keyScenario = "slot" + i + ".scenario";
                            int usage = 0; string scenarioId = string.Empty;
                            data.SaveLoad(keyUsage, ref usage);
                            data.SaveLoad(keyScenario, ref scenarioId);
                            if ((SaveSlotUsage)usage == SaveSlotUsage.CustomScenario)
                                SlotReservationManager.SetSlotReservation(i, SaveSlotUsage.CustomScenario, scenarioId);
                            else
                                SlotReservationManager.ClearSlotReservation(i);
                        }
                        data.GroupEnd();
                        data.GroupEnd();
                        MMLog.WriteDebug("SaveGlobalBridge: applied reservation map from global save");
                    }
                    catch (SaveData.MissingGroupException) { }
                    catch { }
                }
            }
            catch (Exception ex)
            {
                MMLog.Write("SaveGlobal bridge error: " + ex.Message);
            }
        }
    }
}
