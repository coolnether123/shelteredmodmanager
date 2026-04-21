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
    }
}
