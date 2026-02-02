using HarmonyLib;
using ModAPI.Core;
using UnityEngine;

namespace ModAPI.Hooks
{
    [HarmonyPatch(typeof(MainMenuPanel), "OnMessageBoxClosed")]
    public static class MainMenuPanel_OnMessageBoxClosed_Patch
    {
        public static void Prefix(int response)
        {
            // Response 1 is "Yes" (Save and Exit)
            if (response == 1)
            {
                MMLog.WriteDebug("[MainMenuPanel] 'Save and Exit' confirmed. Enabling FastSave mode.");
                
                // SAFETY: Pre-calculate mod data now, while the scene is healthy.
                // This ensures we don't access destroyed Unity objects during the actual quit sequence.
                SaveSystemImpl.PrecalculateShutdownData();
                
                PluginRunner.IsQuitting = true;
            }
        }
    }
}
