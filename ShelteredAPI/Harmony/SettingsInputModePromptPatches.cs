using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.UI;

namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Adds a temporary confirmation countdown when switching from keyboard/mouse to controller mode.
    /// </summary>
    [PatchPolicy(PatchDomain.Input, "ShelteredSettingsInputModePrompt",
        TargetBehavior = "Temporary controller-mode confirmation with automatic revert",
        FailureMode = "Keyboard/mouse to controller switches remain permanent immediately, which can strand the player when a controller is unavailable.",
        RollbackStrategy = "Disable the Input patch domain or remove the Sheltered settings input-mode prompt patch.")]
    internal static class SettingsInputModePromptPatches
    {
        [HarmonyPatch(typeof(SettingsPCPanel), "OnControlMethodButtonPressed_PAD")]
        [HarmonyPrefix]
        private static bool ControlMethodPadPrefix()
        {
            PlatformInput.InputType previousMode = PlatformInput.InputMethod;
            if (previousMode == PlatformInput.InputType.Gamepad)
            {
                ShelteredControllerModePromptDialog.Dismiss();
                return false;
            }

            PlatformInput.SetInputMethod(PlatformInput.InputType.Gamepad);
            if (!ShelteredControllerModePromptDialog.Show(previousMode))
            {
                MMLog.WriteWarning("[SettingsInputModePromptPatches] Failed to show controller confirmation prompt. Restoring previous input mode.");
                PlatformInput.SetInputMethod(previousMode);
            }

            return false;
        }

        [HarmonyPatch(typeof(SettingsPCPanel), "OnControlMethodButtonPressed_KB")]
        [HarmonyPrefix]
        private static bool ControlMethodKeyboardPrefix()
        {
            ShelteredControllerModePromptDialog.Dismiss();
            PlatformInput.SetInputMethod(PlatformInput.InputType.KeyboardMouse);
            return false;
        }
    }
}
