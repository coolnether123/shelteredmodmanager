using HarmonyLib;
using ModAPI.Harmony;
using ShelteredAPI.Input;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Prevents Sheltered's NGUI input loop from receiving events owned by an overlay.
    /// IMGUI overlays continue receiving the same Unity input events directly.
    /// </summary>
    [PatchPolicy(PatchDomain.Input, "ShelteredOverlayInputSuppression",
        TargetBehavior = "Sheltered UI input is suspended while an overlay captures pointer or keyboard focus.",
        FailureMode = "Clicks and text input leak through active editor/debug overlays into the game UI.",
        RollbackStrategy = "Disable the Input patch domain or remove the overlay input suppression patches.",
        StartupTiming = PatchStartupTiming.MenuCritical)]
    internal static class OverlayInputCapturePatches
    {
        private static readonly System.Reflection.FieldInfo HoverField = AccessTools.Field(typeof(UICamera), "mHover");

        [HarmonyPatch(typeof(UICamera), "ProcessMouse")]
        [HarmonyPrefix]
        private static bool ProcessMousePrefix(UICamera __instance)
        {
            if (!OverlayInputCaptureRuntime.ShouldSuppressMouseInput())
                return true;

            ClearMouseState(__instance);
            return false;
        }

        [HarmonyPatch(typeof(UICamera), "ProcessFakeTouches")]
        [HarmonyPrefix]
        private static bool ProcessFakeTouchesPrefix(UICamera __instance)
        {
            if (!OverlayInputCaptureRuntime.ShouldSuppressMouseInput())
                return true;

            ClearMouseState(__instance);
            return false;
        }

        [HarmonyPatch(typeof(UICamera), "ProcessOthers")]
        [HarmonyPrefix]
        private static bool ProcessOthersPrefix()
        {
            return !OverlayInputCaptureRuntime.ShouldSuppressKeyboardInput();
        }

        private static void ClearMouseState(UICamera camera)
        {
            if (camera == null)
                return;

            camera.ShowTooltip(false);
            ClearHoverObject();
            for (int button = 0; button < 3; button++)
            {
                var mouse = UICamera.GetMouse(button);
                if (mouse == null)
                    continue;

                if (mouse.dragStarted && mouse.last != null && mouse.dragged != null)
                    UICamera.Notify(mouse.last, "OnDragOut", mouse.dragged);
                if (mouse.dragStarted && mouse.dragged != null)
                    UICamera.Notify(mouse.dragged, "OnDragEnd", null);
                if (mouse.pressed != null)
                    UICamera.Notify(mouse.pressed, "OnPress", false);

                mouse.current = null;
                mouse.last = null;
                mouse.pressed = null;
                mouse.dragged = null;
                mouse.delta = Vector2.zero;
                mouse.totalDelta = Vector2.zero;
                mouse.pressStarted = false;
                mouse.dragStarted = false;
                mouse.clickNotification = UICamera.ClickNotification.None;
            }
        }

        private static void ClearHoverObject()
        {
            GameObject hover = HoverField != null ? HoverField.GetValue(null) as GameObject : null;
            if (hover != null)
            {
                UICamera.Notify(hover, "OnHover", false);
                HoverField.SetValue(null, null);
            }

            UICamera.hoveredObject = null;
        }
    }
}
