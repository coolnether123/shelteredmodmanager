using HarmonyLib;

namespace ModAPI.Hooks
{
    [HarmonyPatch(typeof(SaveManager), "Update_SaveData")]
    internal static class SaveManager_UpdateSaveData_Patch
    {
        static void Prefix()
        {
            MMLog.Write("[Update_SaveData_Patch] TRIGGERED.");
        }
    }
}
