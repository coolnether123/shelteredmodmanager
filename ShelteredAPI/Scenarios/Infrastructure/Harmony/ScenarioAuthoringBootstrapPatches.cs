using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Shared;
namespace ShelteredAPI.Scenarios.Infrastructure.Harmony{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioAuthoringBootstrap",
        TargetBehavior = "Scenario authoring drafts bootstrap into a real vanilla new game, run briefly once the world is ready, then pause into authoring.",
        FailureMode = "Create Scenario falls back to a plain new game without entering authoring mode.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario authoring bootstrap patch host.",
        ManagerToggleId = ScenarioFeatureToggles.CustomScenarioEditorPatchToggleId,
        ManagerToggleLabel = ScenarioFeatureToggles.CustomScenarioEditorPatchLabel,
        ManagerToggleDescription = ScenarioFeatureToggles.CustomScenarioEditorPatchDescription,
        ManagerToggleDefault = false,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 100,
        StartupTiming = PatchStartupTiming.EditorDeferred)]
    internal static class ScenarioAuthoringBootstrapPatches
    {
        private static bool _loggedCameraSuspension;

        [HarmonyPatch(typeof(SlotSelectionPanel), "OnCancel")]
        [HarmonyPostfix]
        private static void SlotSelectionCancelPostfix()
        {
            ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Slot selection was cancelled.");
        }

        [HarmonyPatch(typeof(BasicCamera), "Update")]
        [HarmonyPrefix]
        private static bool BasicCameraUpdatePrefix()
        {
            try
            {
                if (!ScenarioAuthoringRuntimeGuards.ShouldSuspendCameraUpdateForAuthoring())
                {
                    _loggedCameraSuspension = false;
                    return true;
                }

                if (!_loggedCameraSuspension)
                {
                    _loggedCameraSuspension = true;
                    MMLog.WriteDebug("[ScenarioAuthoringBootstrap] Suspended BasicCamera.Update while scenario authoring owns the shelter scene.");
                }

                return false;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce(
                    "ScenarioAuthoringBootstrap.AuthoringCameraUpdateFailure",
                    "[ScenarioAuthoringBootstrap] Scenario authoring camera update guard failed: " + ex.Message);
                return true;
            }
        }
    }
}
