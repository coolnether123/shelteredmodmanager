using System;
using HarmonyLib;
using ModAPI.Core;
using ShelteredAPI.UI;

namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Takes over vanilla PC controls entry points and routes to ModAPI keybind UI.
    /// </summary>
    internal static class SettingsKeybindsButtonPatches
    {
        [HarmonyPatch(typeof(SettingsPCPanel), "OnControlsButtonPressed")]
        [HarmonyPrefix]
        private static bool ControlsPrefix()
        {
            return TryOpenKeybinds("OnControlsButtonPressed");
        }

        [HarmonyPatch(typeof(SettingsPCPanel), "OnControlsButtonPressed_PAD")]
        [HarmonyPrefix]
        private static bool ControlsPadPrefix()
        {
            return TryOpenKeybinds("OnControlsButtonPressed_PAD");
        }

        private static bool TryOpenKeybinds(string source)
        {
            try
            {
                ShelteredKeybindsUI.Show();
                return false;
            }
            catch (Exception ex)
            {
                MMLog.WriteError("[SettingsKeybindsButtonPatches] Failed to open keybinds from " + source + ": " + ex.Message);
                return true;
            }
        }
    }
}
