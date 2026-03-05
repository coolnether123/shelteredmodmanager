using HarmonyLib;
using ModAPI.InputActions;
using ModAPI.Core;
using ModAPI.UI;
using ShelteredAPI.Input;
using UnityEngine;

namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Routes vanilla PC input polling through ModAPI keybindings (primary + secondary).
    /// </summary>
    internal static class PlatformInputKeybindPatches
    {
        private const float AxisEpsilon = 0.001f;
        private const float FullUiMinX = -10000f;
        private const float FullUiMaxX = 10000f;
        private static bool _loggedInputHook;
        private static bool _loggedMenuHook;
        private static bool _loggedTouchMapHook;

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonDown", new System.Type[] { typeof(PlatformInput.InputButton) })]
        [HarmonyPrefix]
        private static bool InputButtonDownPrefix(PlatformInput.InputButton button, ref bool __result)
        {
            return !TryResolveInputButton(button, KeyState.Down, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonUp", new System.Type[] { typeof(PlatformInput.InputButton) })]
        [HarmonyPrefix]
        private static bool InputButtonUpPrefix(PlatformInput.InputButton button, ref bool __result)
        {
            return !TryResolveInputButton(button, KeyState.Up, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonHeld", new System.Type[] { typeof(PlatformInput.InputButton) })]
        [HarmonyPrefix]
        private static bool InputButtonHeldPrefix(PlatformInput.InputButton button, ref bool __result)
        {
            return !TryResolveInputButton(button, KeyState.Held, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonDown", new System.Type[] { typeof(PlatformInput.MenuInputButton) })]
        [HarmonyPrefix]
        private static bool MenuButtonDownPrefix(PlatformInput.MenuInputButton button, ref bool __result)
        {
            return !TryResolveMenuButton(button, KeyState.Down, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonUp", new System.Type[] { typeof(PlatformInput.MenuInputButton) })]
        [HarmonyPrefix]
        private static bool MenuButtonUpPrefix(PlatformInput.MenuInputButton button, ref bool __result)
        {
            return !TryResolveMenuButton(button, KeyState.Up, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonHeld", new System.Type[] { typeof(PlatformInput.MenuInputButton) })]
        [HarmonyPrefix]
        private static bool MenuButtonHeldPrefix(PlatformInput.MenuInputButton button, ref bool __result)
        {
            return !TryResolveMenuButton(button, KeyState.Held, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetAnyInput")]
        [HarmonyPostfix]
        private static void GetAnyInputPostfix(ref bool __result)
        {
            if (__result) return;
            if (ShelteredVanillaInputActions.IsAnyMappedKeyDown())
            {
                __result = true;
                return;
            }
            if (TouchInputBridge.IsTouchDragHeld(FullUiMinX, FullUiMaxX))
            {
                __result = true;
                return;
            }

            __result =
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_CursorHorizontal")) > AxisEpsilon ||
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_CursorVertical")) > AxisEpsilon ||
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_CameraHorizontal")) > AxisEpsilon ||
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_CameraVertical")) > AxisEpsilon ||
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_InfoPaneScroll")) > AxisEpsilon ||
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_UIhorizontal")) > AxisEpsilon ||
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_UIvertical")) > AxisEpsilon ||
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_MouseScroll")) > AxisEpsilon ||
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_MouseX")) > AxisEpsilon ||
                Mathf.Abs(UnityEngine.Input.GetAxisRaw("PC_MouseY")) > AxisEpsilon;
        }

        private static bool TryResolveInputButton(PlatformInput.InputButton button, KeyState state, ref bool result)
        {
            InputBinding binding;
            if (!ShelteredVanillaInputActions.TryGetBinding(button, out binding))
                return false;

            if (!_loggedInputHook)
            {
                _loggedInputHook = true;
                MMLog.WriteInfo("[PlatformInputKeybindPatches] Gameplay input hook active.");
            }

            if (button == PlatformInput.InputButton.Action
                || button == PlatformInput.InputButton.Interact
                || button == PlatformInput.InputButton.GoHere)
            {
                // Preserve vanilla behavior for click actions (down-only + not hovering UI).
                result = binding.IsDown() && UICamera.hoveredObject == null;
                return true;
            }

            result = Evaluate(binding, state);
            return true;
        }

        private static bool TryResolveMenuButton(PlatformInput.MenuInputButton button, KeyState state, ref bool result)
        {
            if (TryResolveTouchMapDrag(button, state, ref result))
                return true;

            InputBinding binding;
            if (!ShelteredVanillaInputActions.TryGetBinding(button, out binding))
                return false;

            if (!_loggedMenuHook)
            {
                _loggedMenuHook = true;
                MMLog.WriteInfo("[PlatformInputKeybindPatches] Menu input hook active.");
            }

            result = Evaluate(binding, state);
            return true;
        }

        private static bool TryResolveTouchMapDrag(PlatformInput.MenuInputButton button, KeyState state, ref bool result)
        {
            if (button != PlatformInput.MenuInputButton.UIdragMap)
                return false;

            switch (state)
            {
                case KeyState.Down:
                    result = TouchInputBridge.IsTouchDragDown(FullUiMinX, FullUiMaxX);
                    break;
                case KeyState.Up:
                    result = TouchInputBridge.IsTouchDragUp(FullUiMinX, FullUiMaxX);
                    break;
                default:
                    result = TouchInputBridge.IsTouchDragHeld(FullUiMinX, FullUiMaxX);
                    break;
            }

            if (!result)
                return false;

            if (!_loggedTouchMapHook)
            {
                _loggedTouchMapHook = true;
                MMLog.WriteInfo("[PlatformInputKeybindPatches] Touch drag mapped to UIdragMap.");
            }

            return true;
        }

        private static bool Evaluate(InputBinding binding, KeyState state)
        {
            switch (state)
            {
                case KeyState.Down:
                    return binding.IsDown();
                case KeyState.Up:
                    return binding.IsUp();
                default:
                    return binding.IsHeld();
            }
        }

        private enum KeyState
        {
            Down,
            Up,
            Held
        }
    }
}
