using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.UI;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Ensures the legacy KEYBINDS button is not present in the Mod Manager panel.
    /// Keybinds should only be opened from Settings -> Controls.
    /// </summary>
    [PatchPolicy(PatchDomain.UI, "ShelteredModManagerKeybindCleanup",
        TargetBehavior = "Removal of legacy keybind entry points from Mod Manager UI",
        FailureMode = "Duplicate or obsolete keybind entry points remain visible in the Mod Manager panel.",
        RollbackStrategy = "Disable the UI patch domain or remove the Mod Manager cleanup patch.")]
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
