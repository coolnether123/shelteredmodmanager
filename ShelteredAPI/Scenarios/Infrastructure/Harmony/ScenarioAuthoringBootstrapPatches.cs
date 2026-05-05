using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Presentation.Authoring.Shell;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Infrastructure.Harmony{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioAuthoringBootstrap",
        TargetBehavior = "Scenario authoring drafts bootstrap into a real vanilla new game, run briefly once the world is ready, then pause into authoring.",
        FailureMode = "Create Scenario falls back to a plain new game without entering authoring mode.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario authoring bootstrap patch host.",
        ManagerToggleId = ScenarioFeatureToggles.CustomScenarioEditorPatchToggleId,
        ManagerToggleLabel = ScenarioFeatureToggles.CustomScenarioEditorPatchLabel,
        ManagerToggleDescription = ScenarioFeatureToggles.CustomScenarioEditorPatchDescription,
        ManagerToggleDefault = true,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 100,
        StartupTiming = PatchStartupTiming.EditorDeferred)]
    internal static class ScenarioAuthoringBootstrapPatches
    {
        [HarmonyPatch(typeof(SlotSelectionPanel), "OnCancel")]
        [HarmonyPostfix]
        private static void SlotSelectionCancelPostfix()
        {
            ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Slot selection was cancelled.");
        }

        [HarmonyPatch(typeof(CursorBase), "GetCameraFollowPosition")]
        [HarmonyPrefix]
        private static bool CursorBaseFollowPrefix(CursorBase __instance, ref Vector3 __result)
        {
            return TryAllowVanillaCameraFollow(__instance, ref __result);
        }

        [HarmonyPatch(typeof(CursorBase), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorBaseFollowPostfix(ref Vector3 __result)
        {
            TrySuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacement), "GetCameraFollowPosition")]
        [HarmonyPrefix]
        private static bool CursorPlacementFollowPrefix(CursorPlacement __instance, ref Vector3 __result)
        {
            return TryAllowVanillaCameraFollow(__instance, ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacement), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorPlacementFollowPostfix(ref Vector3 __result)
        {
            TrySuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacementRoom), "GetCameraFollowPosition")]
        [HarmonyPrefix]
        private static bool CursorPlacementRoomFollowPrefix(CursorPlacementRoom __instance, ref Vector3 __result)
        {
            return TryAllowVanillaCameraFollow(__instance, ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacementRoom), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorPlacementRoomFollowPostfix(ref Vector3 __result)
        {
            TrySuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorUpgrade), "GetCameraFollowPosition")]
        [HarmonyPrefix]
        private static bool CursorUpgradeFollowPrefix(CursorUpgrade __instance, ref Vector3 __result)
        {
            return TryAllowVanillaCameraFollow(__instance, ref __result);
        }

        [HarmonyPatch(typeof(CursorUpgrade), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorUpgradeFollowPostfix(ref Vector3 __result)
        {
            TrySuppressCameraFollowOverAuthoringUi(ref __result);
        }

        private static bool TryAllowVanillaCameraFollow(CursorBase cursor, ref Vector3 followPosition)
        {
            try
            {
                if (!TryGetCursorPosition(cursor, out followPosition))
                {
                    followPosition = GetCameraFallbackPosition(Vector3.zero);
                    MMLog.WarnOnce(
                        "ScenarioAuthoringBootstrap.CursorCameraFollowMissingCursor",
                        "[ScenarioAuthoringBootstrap] Camera-follow fallback used because the active cursor was unavailable.");
                    return false;
                }

                if (ShouldSuppressCameraFollowOverAuthoringUi())
                {
                    followPosition = GetCameraFallbackPosition(followPosition);
                    return false;
                }
            }
            catch (Exception ex)
            {
                followPosition = GetCameraFallbackPosition(followPosition);
                MMLog.WarnOnce(
                    "ScenarioAuthoringBootstrap.CursorCameraFollowPrefix",
                    "[ScenarioAuthoringBootstrap] Camera-follow prefix fallback used: " + ex.Message);
                return false;
            }

            return true;
        }

        private static void TrySuppressCameraFollowOverAuthoringUi(ref Vector3 followPosition)
        {
            try
            {
                SuppressCameraFollowOverAuthoringUi(ref followPosition);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ScenarioAuthoringBootstrap.SuppressCameraFollowOverAuthoringUi",
                    "[ScenarioAuthoringBootstrap] Camera-follow suppression failed: " + ex.Message);
            }
        }

        private static void SuppressCameraFollowOverAuthoringUi(ref Vector3 followPosition)
        {
            if (!ShouldSuppressCameraFollowOverAuthoringUi())
                return;

            followPosition = GetCameraFallbackPosition(followPosition);
        }

        private static bool ShouldSuppressCameraFollowOverAuthoringUi()
        {
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive())
                return false;

            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            return inputCapture != null && inputCapture.ShouldBlockGameCameraInput();
        }

        private static bool TryGetCursorPosition(CursorBase cursor, out Vector3 position)
        {
            position = Vector3.zero;
            if ((UnityEngine.Object)cursor == (UnityEngine.Object)null)
                return false;

            Transform transform = cursor.transform;
            if ((UnityEngine.Object)transform == (UnityEngine.Object)null)
                return false;

            position = transform.position;
            return true;
        }

        private static Vector3 GetCameraFallbackPosition(Vector3 fallback)
        {
            Camera camera = Camera.main;
            if ((UnityEngine.Object)camera == (UnityEngine.Object)null)
                return fallback;

            Transform transform = camera.transform;
            return (UnityEngine.Object)transform != (UnityEngine.Object)null ? transform.position : fallback;
        }
    }
}
