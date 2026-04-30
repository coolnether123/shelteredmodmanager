using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioAuthoringBootstrap",
        TargetBehavior = "Scenario authoring drafts bootstrap into a real vanilla new game and pause once the world is ready.",
        FailureMode = "Create Scenario falls back to a plain new game without entering authoring mode.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario authoring bootstrap patch host.")]
    internal static class ScenarioAuthoringBootstrapPatches
    {
        [HarmonyPatch(typeof(SlotSelectionPanel), "OnCancel")]
        [HarmonyPostfix]
        private static void SlotSelectionCancelPostfix()
        {
            ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Slot selection was cancelled.");
        }

        [HarmonyPatch(typeof(CursorBase), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorBaseFollowPostfix(ref Vector3 __result)
        {
            TrySuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorBase), "GetCameraFollowPosition")]
        [HarmonyFinalizer]
        private static Exception CursorBaseFollowFinalizer(Exception __exception, ref Vector3 __result)
        {
            return FinalizeCameraFollow("CursorBase.GetCameraFollowPosition", __exception, ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacement), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorPlacementFollowPostfix(ref Vector3 __result)
        {
            TrySuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacement), "GetCameraFollowPosition")]
        [HarmonyFinalizer]
        private static Exception CursorPlacementFollowFinalizer(Exception __exception, ref Vector3 __result)
        {
            return FinalizeCameraFollow("CursorPlacement.GetCameraFollowPosition", __exception, ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacementRoom), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorPlacementRoomFollowPostfix(ref Vector3 __result)
        {
            TrySuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacementRoom), "GetCameraFollowPosition")]
        [HarmonyFinalizer]
        private static Exception CursorPlacementRoomFollowFinalizer(Exception __exception, ref Vector3 __result)
        {
            return FinalizeCameraFollow("CursorPlacementRoom.GetCameraFollowPosition", __exception, ref __result);
        }

        [HarmonyPatch(typeof(CursorUpgrade), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorUpgradeFollowPostfix(ref Vector3 __result)
        {
            TrySuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorUpgrade), "GetCameraFollowPosition")]
        [HarmonyFinalizer]
        private static Exception CursorUpgradeFollowFinalizer(Exception __exception, ref Vector3 __result)
        {
            return FinalizeCameraFollow("CursorUpgrade.GetCameraFollowPosition", __exception, ref __result);
        }

        private static Exception FinalizeCameraFollow(string source, Exception exception, ref Vector3 followPosition)
        {
            if (exception == null)
                return null;

            try
            {
                if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive())
                    return exception;

                if (TryResolveCameraPosition(out Vector3 cameraPosition))
                    followPosition = cameraPosition;

                MMLog.WarnOnce(
                    "ScenarioAuthoringBootstrap.CameraFollowFinalizer." + source,
                    "[ScenarioAuthoringBootstrap] Suppressed " + source + " failure while scenario authoring is active: " + exception.Message);
                return null;
            }
            catch (Exception finalizerException)
            {
                MMLog.WarnOnce(
                    "ScenarioAuthoringBootstrap.CameraFollowFinalizer",
                    "[ScenarioAuthoringBootstrap] Camera-follow finalizer failed: " + finalizerException.Message);
                return exception;
            }
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
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive())
                return;

            ScenarioAuthoringInputCaptureService inputCapture = ScenarioCompositionRoot.Resolve<ScenarioAuthoringInputCaptureService>();
            if (inputCapture == null || !inputCapture.ShouldBlockGameCameraInput())
                return;

            Camera camera = Camera.main;
            if (camera == null)
                return;

            followPosition = camera.transform.position;
        }

        private static bool TryResolveCameraPosition(out Vector3 position)
        {
            Camera camera = Camera.main;
            if (camera == null)
            {
                Camera[] cameras = Camera.allCameras;
                if (cameras != null)
                {
                    for (int i = 0; i < cameras.Length; i++)
                    {
                        if (cameras[i] != null && cameras[i].enabled)
                        {
                            camera = cameras[i];
                            break;
                        }
                    }
                }
            }

            if (camera == null)
            {
                position = Vector3.zero;
                return false;
            }

            position = camera.transform.position;
            return true;
        }
    }
}
