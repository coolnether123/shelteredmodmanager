using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.InputActions;
using ShelteredAPI.Input;
using ShelteredAPI.Debugging;
using ShelteredAPI.Scenarios;
using UnityEngine;


using ShelteredAPI.Hooks;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
namespace ShelteredAPI.Harmony
{
    /// <summary>
    /// Routes vanilla PC input polling through ModAPI keybindings and Sheltered-specific pointer axis routing.
    /// </summary>
    [PatchPolicy(PatchDomain.Input, "ShelteredPlatformInputBridge",
        TargetBehavior = "Vanilla input polling bridge through ModAPI-managed keybindings",
        FailureMode = "Configured keybindings do not override vanilla PC input correctly.",
        RollbackStrategy = "Disable the Input patch domain or remove the Sheltered platform input bridge.",
        StartupTiming = PatchStartupTiming.MenuCritical)]
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
            if (TrySuppressButton(ref __result))
                return false;

            return !TryResolveInputButton(button, KeyState.Down, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonUp", new System.Type[] { typeof(PlatformInput.InputButton) })]
        [HarmonyPrefix]
        private static bool InputButtonUpPrefix(PlatformInput.InputButton button, ref bool __result)
        {
            if (TrySuppressButton(ref __result))
                return false;

            return !TryResolveInputButton(button, KeyState.Up, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonHeld", new System.Type[] { typeof(PlatformInput.InputButton) })]
        [HarmonyPrefix]
        private static bool InputButtonHeldPrefix(PlatformInput.InputButton button, ref bool __result)
        {
            if (TrySuppressButton(ref __result))
                return false;

            return !TryResolveInputButton(button, KeyState.Held, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonDown", new System.Type[] { typeof(PlatformInput.MenuInputButton) })]
        [HarmonyPrefix]
        private static bool MenuButtonDownPrefix(PlatformInput.MenuInputButton button, ref bool __result)
        {
            if (TrySuppressButton(ref __result))
                return false;

            return !TryResolveMenuButton(button, KeyState.Down, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonUp", new System.Type[] { typeof(PlatformInput.MenuInputButton) })]
        [HarmonyPrefix]
        private static bool MenuButtonUpPrefix(PlatformInput.MenuInputButton button, ref bool __result)
        {
            if (TrySuppressButton(ref __result))
                return false;

            return !TryResolveMenuButton(button, KeyState.Up, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetButtonHeld", new System.Type[] { typeof(PlatformInput.MenuInputButton) })]
        [HarmonyPrefix]
        private static bool MenuButtonHeldPrefix(PlatformInput.MenuInputButton button, ref bool __result)
        {
            if (TrySuppressButton(ref __result))
                return false;

            return !TryResolveMenuButton(button, KeyState.Held, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetAnyInput")]
        [HarmonyPostfix]
        private static void GetAnyInputPostfix(ref bool __result)
        {
            if (OverlayInputCaptureRuntime.ShouldSuppressAnyInput())
            {
                __result = false;
                return;
            }

            if (__result) return;
            if (ShelteredVanillaInputActions.IsAnyMappedKeyDown())
            {
                __result = true;
                return;
            }

            if (ShelteredTouchpadInputRouter.IsTouchDragHeld(FullUiMinX, FullUiMaxX))
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

        [HarmonyPatch(typeof(PlatformInput_PC), "GetInputAxis", new System.Type[] { typeof(PlatformInput.MenuInputAxis) })]
        [HarmonyPrefix]
        private static bool MenuAxisPrefix(PlatformInput.MenuInputAxis axis, ref float __result)
        {
            if (TrySuppressAxis(ref __result))
                return false;

            return !TryResolveMenuAxis(axis, false, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetInputAxisRaw", new System.Type[] { typeof(PlatformInput.MenuInputAxis) })]
        [HarmonyPrefix]
        private static bool MenuAxisRawPrefix(PlatformInput.MenuInputAxis axis, ref float __result)
        {
            if (TrySuppressAxis(ref __result))
                return false;

            return !TryResolveMenuAxis(axis, true, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetInputAxis", new System.Type[] { typeof(PlatformInput.InputAxis) })]
        [HarmonyPrefix]
        private static bool InputAxisPrefix(PlatformInput.InputAxis axis, ref float __result)
        {
            if (TrySuppressAxis(ref __result))
                return false;

            return !TryResolveGameplayAxis(axis, false, ref __result);
        }

        [HarmonyPatch(typeof(PlatformInput_PC), "GetInputAxisRaw", new System.Type[] { typeof(PlatformInput.InputAxis) })]
        [HarmonyPrefix]
        private static bool InputAxisRawPrefix(PlatformInput.InputAxis axis, ref float __result)
        {
            if (TrySuppressAxis(ref __result))
                return false;

            return !TryResolveGameplayAxis(axis, true, ref __result);
        }

        private static bool TrySuppressButton(ref bool result)
        {
            if (!OverlayInputCaptureRuntime.ShouldSuppressAnyInput())
                return false;

            result = false;
            return true;
        }

        private static bool TrySuppressAxis(ref float result)
        {
            if (!OverlayInputCaptureRuntime.ShouldSuppressAnyInput())
                return false;

            result = 0f;
            return true;
        }

        private static bool TryResolveInputButton(PlatformInput.InputButton button, KeyState state, ref bool result)
        {
            if (ShelteredFeedbackInputEnabler.IsOverlayVisible)
            {
                result = false;
                return true;
            }

            if (TryResolveAuthoringVanillaInteractionButton(button, state, ref result))
                return true;

            bool allowAuthoringVanillaInteract = ShouldAllowAuthoringVanillaInteractButton(button);

            // Scenario authoring uses the same mouse buttons as Sheltered's world controls.
            // If those buttons leak into vanilla gameplay, selecting a target also issues
            // move/orders to survivors. Keep the editor in exclusive control until playtest.
            if (!allowAuthoringVanillaInteract && ScenarioAuthoringRuntimeGuards.ShouldBlockGameplayButton(button))
            {
                result = false;
                return true;
            }

            InputBinding binding;
            if (!ShelteredVanillaInputActions.TryGetBinding(button, out binding))
                return false;

            if (!_loggedInputHook)
            {
                _loggedInputHook = true;
                MMLog.WriteDebug("[PlatformInputKeybindPatches] Gameplay input hook active.");
            }

            if (button == PlatformInput.InputButton.Action
                || button == PlatformInput.InputButton.Interact
                || button == PlatformInput.InputButton.GoHere)
            {
                result = Evaluate(binding, state) && UICamera.hoveredObject == null;
                NotifyAuthoringVanillaInteractionButton(button, state, result);
                return true;
            }

            result = Evaluate(binding, state);
            NotifyAuthoringVanillaInteractionButton(button, state, result);
            return true;
        }

        private static bool ShouldAllowAuthoringVanillaInteractButton(PlatformInput.InputButton button)
        {
            if (button != PlatformInput.InputButton.Interact)
                return false;

            try
            {
                ScenarioVanillaInteractionRuntimeService service = ScenarioCompositionRoot.Resolve<ScenarioVanillaInteractionRuntimeService>();
                return service != null && service.CanStartWorldInteraction();
            }
            catch
            {
                return false;
            }
        }

        private static bool TryResolveAuthoringVanillaInteractionButton(PlatformInput.InputButton button, KeyState state, ref bool result)
        {
            try
            {
                ScenarioVanillaInteractionRuntimeService service = ScenarioCompositionRoot.Resolve<ScenarioVanillaInteractionRuntimeService>();
                if (service == null)
                    return false;

                return service.TryResolveSyntheticLeftInteract(
                    button,
                    state == KeyState.Down,
                    state == KeyState.Up,
                    state == KeyState.Held,
                    out result);
            }
            catch
            {
                return false;
            }
        }

        private static void NotifyAuthoringVanillaInteractionButton(PlatformInput.InputButton button, KeyState state, bool result)
        {
            try
            {
                ScenarioVanillaInteractionRuntimeService service = ScenarioCompositionRoot.Resolve<ScenarioVanillaInteractionRuntimeService>();
                if (service != null)
                    service.NotifyGameplayButtonResult(button, state == KeyState.Up, result);
            }
            catch
            {
            }
        }

        private static bool TryResolveMenuButton(PlatformInput.MenuInputButton button, KeyState state, ref bool result)
        {
            if (ShelteredFeedbackInputEnabler.IsOverlayVisible)
            {
                result = false;
                return true;
            }

            if (TryResolveTouchMapDrag(button, state, ref result))
                return true;

            InputBinding binding;
            if (!ShelteredVanillaInputActions.TryGetBinding(button, out binding))
                return false;

            if (!_loggedMenuHook)
            {
                _loggedMenuHook = true;
                MMLog.WriteDebug("[PlatformInputKeybindPatches] Menu input hook active.");
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
                    result = ShelteredTouchpadInputRouter.IsTouchDragDown(FullUiMinX, FullUiMaxX);
                    break;
                case KeyState.Up:
                    result = ShelteredTouchpadInputRouter.IsTouchDragUp(FullUiMinX, FullUiMaxX);
                    break;
                default:
                    result = ShelteredTouchpadInputRouter.IsTouchDragHeld(FullUiMinX, FullUiMaxX);
                    break;
            }

            if (!result)
                return false;

            if (!_loggedTouchMapHook)
            {
                _loggedTouchMapHook = true;
                MMLog.WriteDebug("[PlatformInputKeybindPatches] Touch drag mapped to UIdragMap.");
            }

            return true;
        }

        private static bool TryResolveGameplayAxis(PlatformInput.InputAxis axis, bool raw, ref float result)
        {
            if (ShelteredFeedbackInputEnabler.IsOverlayVisible)
            {
                result = 0f;
                return true;
            }

            if (ScenarioAuthoringRuntimeGuards.ShouldBlockGameplayAxis(axis))
            {
                result = 0f;
                return true;
            }

            float resolved;
            if (!ShelteredTouchpadInputRouter.TryGetGameplayAxis(axis, raw, out resolved))
                return false;

            result = resolved;
            return true;
        }

        private static bool TryResolveMenuAxis(PlatformInput.MenuInputAxis axis, bool raw, ref float result)
        {
            if (ShelteredFeedbackInputEnabler.IsOverlayVisible)
            {
                result = 0f;
                return true;
            }

            if (ScenarioAuthoringRuntimeGuards.ShouldBlockMenuAxis(axis))
            {
                result = 0f;
                return true;
            }

            float resolved;
            if (!ShelteredTouchpadInputRouter.TryGetMenuAxis(axis, raw, out resolved))
                return false;

            result = resolved;
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
