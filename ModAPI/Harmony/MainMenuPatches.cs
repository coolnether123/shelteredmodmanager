using System;
using HarmonyLib;
using UnityEngine;


namespace ModAPI.Harmony
{
    [HarmonyPatch(typeof(MainMenuPanel), "OnShow")]
    internal static class MainMenuPanel_OnShow_ResetProxyState
    {
        static void Postfix(MainMenuPanel __instance)
        {
            try
            {

            }
            catch (Exception ex)
            {
                MMLog.Write("MainMenuPanel_OnShow_ResetProxyState patch error: " + ex.Message);
            }
        }
    }
}
