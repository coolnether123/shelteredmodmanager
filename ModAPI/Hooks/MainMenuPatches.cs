using System;
using HarmonyLib;
using UnityEngine;
using ModAPI.Saves;

namespace ModAPI.Hooks
{
    [HarmonyPatch(typeof(MainMenuPanel), "OnShow")]
    internal static class MainMenuPanel_OnShow_ResetProxyState
    {
        static void Postfix(MainMenuPanel __instance)
        {
            try
            {
                var saveManager = SaveManager.instance;
                if (saveManager == null) return;

                var platformSaveProxy = saveManager.platformSave as PlatformSaveProxy;
                if (platformSaveProxy == null) return;

                platformSaveProxy.ResetState();
                MMLog.WriteDebug("MainMenuPanel: PlatformSaveProxy state reset.");
            }
            catch (Exception ex)
            {
                MMLog.Write("MainMenuPanel_OnShow_ResetProxyState patch error: " + ex.Message);
            }
        }
    }
}
