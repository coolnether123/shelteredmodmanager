using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ModAPI.Hooks;
using ModAPI.Hooks.Paging;
using ModAPI.Saves;
using ModAPI.Scenarios;
using ModAPI.UI;
using UnityEngine;

namespace ShelteredAPI.Scenarios
{
    internal static class ShelteredCustomScenarioRuntimeState
    {
        private static int _blockSlotClicksUntilFrame;
        private static int _lastLoggedBlockedUntilFrame = -1;
        private static bool _customModeActive;

        public static bool IsSlotClickBlocked
        {
            get { return Time.frameCount <= _blockSlotClicksUntilFrame; }
        }

        public static void SetCustomModeActive(bool active)
        {
            _customModeActive = active;
        }

        public static bool ShouldBlockSlotInteraction(Component component)
        {
            return UIFlowGuard.BlockSlotClicks || IsSlotClickBlocked || _customModeActive;
        }

        public static void BlockSlotClicksBriefly()
        {
            _blockSlotClicksUntilFrame = Math.Max(_blockSlotClicksUntilFrame, Time.frameCount + 2);
            if (_lastLoggedBlockedUntilFrame != _blockSlotClicksUntilFrame)
            {
                _lastLoggedBlockedUntilFrame = _blockSlotClicksUntilFrame;
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Slot clicks blocked until frame " + _blockSlotClicksUntilFrame
                    + " (current=" + Time.frameCount + ").");
            }
        }

        public static bool HasPendingCustomScenario()
        {
            CustomScenarioState state = ShelteredCustomScenarioService.Instance.CurrentState;
            return state != null && state.LifecycleState == CustomScenarioLifecycleState.Pending && !string.IsNullOrEmpty(state.ScenarioId);
        }

        public static void ClearPendingCustomScenario()
        {
            CustomScenarioState state = ShelteredCustomScenarioService.Instance.CurrentState;
            if (state != null && state.LifecycleState == CustomScenarioLifecycleState.Pending)
            {
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Clearing pending custom scenario state. scenarioId=" + state.ScenarioId + ".");
                ShelteredCustomScenarioService.Instance.ClearState();
            }
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSelection",
        TargetBehavior = "Custom scenario entries are surfaced in the vanilla Sheltered scenario selection panel.",
        FailureMode = "Registered custom scenarios are unavailable from the in-game scenario selection flow.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario selection patch host.")]
    [HarmonyPatch(typeof(ScenarioSelectionPanel))]
    internal static class ShelteredCustomScenarioSelectionPatches
    {
        private static ShelteredScenarioSelectionBrowserController Controller
        {
            get { return ShelteredScenarioSelectionBrowserController.Instance; }
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnShow")]
        private static void OnShowPostfix(ScenarioSelectionPanel __instance, List<UIButton> ___m_scenarioButtons)
        {
            Controller.Initialize(__instance, ___m_scenarioButtons);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnScenarioSelected")]
        private static bool OnScenarioSelectedPrefix(
            ScenarioSelectionPanel __instance,
            int ___m_selectedScenario,
            UILabel ___m_scenarioNameLabel,
            UILabel ___m_scenarioDescLabel,
            UILabel ___m_scenarioHighScore,
            GameObject ___m_stasis_scoreLabelsRoot)
        {
            return Controller.HandleScenarioSelected(
                __instance,
                ___m_selectedScenario,
                ___m_scenarioNameLabel,
                ___m_scenarioDescLabel,
                ___m_scenarioHighScore,
                ___m_stasis_scoreLabelsRoot);
        }

        [HarmonyPostfix]
        [HarmonyPatch("Update")]
        private static void UpdatePostfix(
            ScenarioSelectionPanel __instance,
            int ___m_selectedScenario,
            UILabel ___m_scenarioNameLabel,
            UILabel ___m_scenarioDescLabel,
            UILabel ___m_scenarioHighScore,
            GameObject ___m_stasis_scoreLabelsRoot,
            SlotSelectionPanel ___selectionPanel)
        {
            Controller.HandleUpdate(
                __instance,
                ___m_selectedScenario,
                ___m_scenarioNameLabel,
                ___m_scenarioDescLabel,
                ___m_scenarioHighScore,
                ___m_stasis_scoreLabelsRoot,
                ___selectionPanel);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnScenarioChosen")]
        private static bool OnScenarioChosenPrefix(
            ScenarioSelectionPanel __instance,
            int ___m_selectedScenario,
            List<UIButton> ___m_scenarioButtons,
            UILabel ___m_scenarioNameLabel,
            UILabel ___m_scenarioDescLabel,
            UILabel ___m_scenarioHighScore,
            GameObject ___m_stasis_scoreLabelsRoot)
        {
            return Controller.HandleScenarioChosen(__instance, ___m_selectedScenario, ___m_scenarioButtons);
        }

        [HarmonyPrefix]
        [HarmonyPatch("OnCancel")]
        private static bool OnCancelPrefix(ScenarioSelectionPanel __instance, List<UIButton> ___m_scenarioButtons)
        {
            return Controller.HandleCancel(__instance, ___m_scenarioButtons);
        }

        [HarmonyPostfix]
        [HarmonyPatch("OnDestroy")]
        private static void OnDestroyPostfix(ScenarioSelectionPanel __instance)
        {
            Controller.Cleanup(__instance);
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSpawn",
        TargetBehavior = "Pending custom scenarios are spawned through Sheltered QuestManager once a new world is ready.",
        FailureMode = "A selected custom scenario reaches save-slot selection but never starts in the new game.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario spawn patch host.")]
    [HarmonyPatch(typeof(QuestManager), "UpdateManager")]
    internal static class ShelteredCustomScenarioQuestManagerPatches
    {
        [HarmonyPostfix]
        private static void UpdateManagerPostfix()
        {
            try
            {
                ScenarioCompositionRoot.Resolve<IScenarioRuntimeOrchestrator>().UpdatePendingScenarioSpawn();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredCustomScenarioSpawn] UpdateManager hook failed: " + ex.Message);
            }
        }

    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioStateCleanup",
        TargetBehavior = "Pending custom scenario state is cleared when the player leaves custom scenario startup flow.",
        FailureMode = "A stale pending custom scenario may spawn after the player cancels or starts a vanilla mode.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario state cleanup patch host.")]
    internal static class ShelteredCustomScenarioStateCleanupPatches
    {
        [HarmonyPatch(typeof(GameModeSelectionPanel), "OnSurvivalModeChosen")]
        [HarmonyPostfix]
        private static void SurvivalChosenPostfix()
        {
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Survival mode chosen; checking for stale pending custom scenario state.");
            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "OnCancel")]
        [HarmonyPostfix]
        private static void SlotSelectionCancelPostfix()
        {
            MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Slot selection cancelled; checking for stale pending custom scenario state.");
            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
        }

        [HarmonyPatch(typeof(CustomisationPanel), "OnCancel")]
        [HarmonyPrefix]
        private static void CustomisationCancelPrefix(CustomisationPanel __instance)
        {
            if (__instance == null)
                return;

            int currentPage = 0;
            try { currentPage = Traverse.Create(__instance).Field("m_currentPageIndex").GetValue<int>(); }
            catch { }

            if (currentPage != 0)
                return;

            PlatformSaveProxy.Target pendingTarget;
            bool draftCancelled = false;
            if (PlatformSaveProxy.TryGetNextSave(SaveManager.SaveType.Slot1, out pendingTarget) && pendingTarget != null)
            {
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Customisation cancelled before game start. Clearing queued startup save. scenarioId="
                    + pendingTarget.scenarioId + " saveId=" + pendingTarget.saveId + ".");

                bool isDraftStartup = string.Equals(
                    pendingTarget.scenarioId,
                    ScenarioAuthoringDraftRepository.DraftStorageScenarioId,
                    StringComparison.OrdinalIgnoreCase);

                if (isDraftStartup)
                {
                    ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Customisation was cancelled before the scenario world started.");
                    draftCancelled = true;
                }
                else if (!string.IsNullOrEmpty(pendingTarget.scenarioId)
                    && !string.Equals(pendingTarget.scenarioId, "Standard", StringComparison.OrdinalIgnoreCase))
                {
                    ScenarioSaves.Delete(pendingTarget.scenarioId, pendingTarget.saveId);
                }

                PlatformSaveProxy.ClearNextSave(SaveManager.SaveType.Slot1);
            }

            // Guard covers the edge case where a draft was queued but no save target was
            // registered yet (e.g. the UI flow was interrupted before QueueNewGameSaveTarget ran).
            if (!draftCancelled)
                ScenarioAuthoringBootstrapService.Instance.CancelPendingDraft("Customisation was cancelled before the scenario world started.");

            ShelteredCustomScenarioRuntimeState.ClearPendingCustomScenario();
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ScenarioAuthoringGlobalUiIsolation",
        TargetBehavior = "Global gameplay hotkeys do not steal focus while scenario authoring owns the live shelter scene.",
        FailureMode = "Pause/map/clipboard hotkeys can still open vanilla panels during authoring pause.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario authoring global UI isolation patch host.")]
    internal static class ScenarioAuthoringGlobalUiIsolationPatches
    {
        [HarmonyPatch(typeof(UI_InputListener), "UpdateManager")]
        [HarmonyPrefix]
        private static bool UpdateManagerPrefix()
        {
            if (!ScenarioAuthoringRuntimeGuards.ShouldSuppressGlobalGameplayUi())
                return true;

            return false;
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ScenarioAuthoringPauseOwnership",
        TargetBehavior = "Scenario authoring owns pause without allowing the vanilla pause panel/menu stack to reopen.",
        FailureMode = "Authoring pause can route back through the vanilla pause flow and reopen the pause menu panel.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario authoring pause ownership patch host.")]
    internal static class ScenarioAuthoringPauseOwnershipPatches
    {
        [HarmonyPatch(typeof(PauseManager), "Pause")]
        [HarmonyPrefix]
        private static bool PausePrefix()
        {
            if (!ScenarioAuthoringRuntimeGuards.ShouldMaintainPausedSimulation())
                return true;

            ScenarioAuthoringPauseService.Instance.EnsurePaused("Vanilla pause request intercepted while scenario authoring owned the shelter scene.");
            return false;
        }

        [HarmonyPatch(typeof(PauseManager), "Resume")]
        [HarmonyPrefix]
        private static bool ResumePrefix()
        {
            if (!ScenarioAuthoringRuntimeGuards.ShouldMaintainPausedSimulation())
                return true;

            MMLog.WriteInfo("[ScenarioAuthoringPause] Ignored vanilla resume request while scenario authoring owned the pause state.");
            return false;
        }

        [HarmonyPatch(typeof(UIPanelManager), "PushPanel", new[] { typeof(BasePanel) })]
        [HarmonyPrefix]
        private static bool PushPanelPrefix(BasePanel panel)
        {
            if (!ScenarioAuthoringPauseService.Instance.ShouldSuppressPauseMenu())
                return true;

            if (!ScenarioAuthoringPauseService.Instance.IsPauseMenuPanel(panel))
                return true;

            MMLog.WriteInfo("[ScenarioAuthoringPause] Suppressed UIPanelManager.PushPanel for the vanilla pause menu while authoring.");
            return false;
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ScenarioAuthoringSimulationFreeze",
        TargetBehavior = "Scenario authoring keeps Sheltered's simulation and game clock frozen even when the vanilla pause panel is hidden.",
        FailureMode = "The editor hides the pause menu, but shelter actors and in-game time keep advancing.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario authoring simulation freeze patch host.")]
    internal static class ScenarioAuthoringSimulationFreezePatches
    {
        [HarmonyPatch(typeof(UIPanelManager), nameof(UIPanelManager.timePaused), MethodType.Getter)]
        [HarmonyPostfix]
        private static void TimePausedGetterPostfix(ref bool __result)
        {
            if (ScenarioAuthoringRuntimeGuards.ShouldMaintainPausedSimulation())
                __result = true;
        }

        [HarmonyPatch(typeof(GameTime), "Update")]
        [HarmonyPrefix]
        private static bool GameTimeUpdatePrefix()
        {
            return !ScenarioAuthoringRuntimeGuards.ShouldMaintainPausedSimulation();
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredScenarioDefinitionApply",
        TargetBehavior = "Active scenario definitions are applied after save load once the Sheltered world is ready.",
        FailureMode = "A scenario-bound save loads as vanilla until the next successful scenario apply.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the definition apply patch host.")]
    [HarmonyPatch(typeof(QuestManager), "UpdateManager")]
    internal static class ShelteredScenarioDefinitionApplyPatches
    {
        [HarmonyPostfix]
        private static void UpdateManagerPostfix()
        {
            try
            {
                ScenarioCompositionRoot.Resolve<IScenarioRuntimeOrchestrator>().UpdateActiveScenarioApply();
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[ShelteredScenarioDefinitionApply] UpdateManager hook failed: " + ex.Message);
            }
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ShelteredCustomScenarioSlotClickGuard",
        TargetBehavior = "Save-slot clicks are briefly blocked while custom scenario UI buttons are being pressed.",
        FailureMode = "Underlying save-slot controls can steal clicks from the custom scenario hub/list.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the custom scenario slot click guard patch host.")]
    internal static class ShelteredCustomScenarioSlotClickGuardPatches
    {
        [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotSelected")]
        [HarmonyPrefix]
        private static bool OnSlotSelectedPrefix(SlotSelectionPanel __instance)
        {
            bool allowed = !ShelteredCustomScenarioRuntimeState.ShouldBlockSlotInteraction(__instance);
            if (!allowed)
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Blocked SlotSelectionPanel.OnSlotSelected during guarded UI click.");
            return allowed;
        }

        [HarmonyPatch(typeof(SlotSelectionPanel), "OnSlotChosen")]
        [HarmonyPrefix]
        private static bool OnSlotChosenPrefix(SlotSelectionPanel __instance)
        {
            bool allowed = !ShelteredCustomScenarioRuntimeState.ShouldBlockSlotInteraction(__instance);
            if (!allowed)
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Blocked SlotSelectionPanel.OnSlotChosen during guarded UI click.");
            return allowed;
        }

        [HarmonyPatch(typeof(SaveSlotButton), "OnClick")]
        [HarmonyPrefix]
        private static bool SaveSlotButtonClickPrefix(SaveSlotButton __instance)
        {
            bool allowed = !ShelteredCustomScenarioRuntimeState.ShouldBlockSlotInteraction(__instance);
            if (!allowed)
                MMLog.WriteInfo("[ShelteredCustomScenarioSelection] Blocked SaveSlotButton.OnClick during guarded UI click.");
            return allowed;
        }
    }
}
