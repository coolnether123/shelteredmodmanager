using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.UI;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Ensures the legacy KEYBINDS button is not present in the Mod Manager panel.
    /// Keybinds should only be opened from Settings -> Controls.
    /// </summary>
    [HarmonyPatch(typeof(ModManagerPanel), "Initialise")]
    internal static class ModManagerKeybindsPatches
    {
        private const string ButtonName = "KeybindsButton";

        [HarmonyPostfix]
        private static void Postfix(ModManagerPanel __instance)
        {
            try
            {
                if (__instance == null || __instance.transform == null) return;
                var existing = __instance.transform.Find(ButtonName);
                if (existing == null) return;

                UnityEngine.Object.Destroy(existing.gameObject);
                MMLog.WriteInfo("[ModManagerKeybindsPatches] Removed legacy Keybinds button from Mod Manager panel.");
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[ModManagerKeybindsPatches] Failed to remove legacy Keybinds button: " + ex.Message);
            }
        }
    }
}
