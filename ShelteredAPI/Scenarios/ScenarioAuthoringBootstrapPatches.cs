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
            SuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacement), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorPlacementFollowPostfix(ref Vector3 __result)
        {
            SuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorPlacementRoom), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorPlacementRoomFollowPostfix(ref Vector3 __result)
        {
            SuppressCameraFollowOverAuthoringUi(ref __result);
        }

        [HarmonyPatch(typeof(CursorUpgrade), "GetCameraFollowPosition")]
        [HarmonyPostfix]
        private static void CursorUpgradeFollowPostfix(ref Vector3 __result)
        {
            SuppressCameraFollowOverAuthoringUi(ref __result);
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
    }
}
