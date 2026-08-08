using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredScenarioEditor.Shared;

namespace ShelteredScenarioEditor.Infrastructure.Harmony
{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioAuthoringGlobalUiIsolation",
        TargetBehavior = "Global gameplay hotkeys do not steal focus while scenario authoring owns the live shelter scene.",
        FailureMode = "Pause/map/clipboard hotkeys can still open vanilla panels during authoring pause.",
        RollbackStrategy = "Disable the scenario editor option or remove the editor assembly.",
        ManagerToggleId = ScenarioEditorFeature.EnabledOptionId,
        ManagerToggleLabel = ScenarioEditorFeature.EnabledOptionLabel,
        ManagerToggleDescription = ScenarioEditorFeature.EnabledOptionDescription,
        ManagerToggleDefault = false,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 100,
        StartupTiming = PatchStartupTiming.EditorDeferred)]
    internal static class ScenarioAuthoringGlobalUiIsolationPatches
    {
        [HarmonyPatch(typeof(UI_InputListener), "UpdateManager")]
        [HarmonyPrefix]
        private static bool UpdateManagerPrefix()
        {
            return !ScenarioAuthoringRuntimeGuards.ShouldSuppressGlobalGameplayUi();
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ScenarioAuthoringPauseOwnership",
        TargetBehavior = "Scenario authoring owns pause without allowing the vanilla pause panel/menu stack to reopen.",
        FailureMode = "Authoring pause can route back through the vanilla pause flow and reopen the pause menu panel.",
        RollbackStrategy = "Disable the scenario editor option or remove the editor assembly.",
        ManagerToggleId = ScenarioEditorFeature.EnabledOptionId,
        ManagerToggleLabel = ScenarioEditorFeature.EnabledOptionLabel,
        ManagerToggleDescription = ScenarioEditorFeature.EnabledOptionDescription,
        ManagerToggleDefault = false,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 100,
        StartupTiming = PatchStartupTiming.EditorDeferred)]
    internal static class ScenarioAuthoringPauseOwnershipPatches
    {
        [HarmonyPatch(typeof(PauseManager), "Pause")]
        [HarmonyPrefix]
        private static bool PausePrefix()
        {
            if (!ScenarioAuthoringRuntimeGuards.ShouldMaintainPausedSimulation())
                return true;

            ScenarioAuthoringPauseService.Instance.EnsurePaused(
                "Vanilla pause request intercepted while scenario authoring owned the shelter scene.");
            return false;
        }

        [HarmonyPatch(typeof(PauseManager), "Resume")]
        [HarmonyPrefix]
        private static bool ResumePrefix()
        {
            if (!ScenarioAuthoringRuntimeGuards.ShouldMaintainPausedSimulation())
                return true;
            if (ScenarioAuthoringPauseService.Instance.HandleVanillaResumeRequest())
                return false;

            MMLog.WriteInfo("[ScenarioAuthoringPause] Ignored vanilla resume request while scenario authoring owned the pause state.");
            return false;
        }

        [HarmonyPatch(typeof(UIPanelManager), "PushPanel", new[] { typeof(BasePanel) })]
        [HarmonyPrefix]
        private static bool PushPanelPrefix(UIPanelManager panelManager, BasePanel panel)
        {
            if (panelManager != null && panel is TutorialPopupPanel)
            {
                try
                {
                    if (panelManager.IsPanelOnStack(panel))
                        return false;
                }
                catch { }
            }

            return !ScenarioAuthoringPauseService.Instance.ShouldSuppressPauseMenu()
                || !ScenarioAuthoringPauseService.Instance.IsPauseMenuPanel(panel);
        }
    }

    [PatchPolicy(PatchDomain.Scenarios, "ScenarioAuthoringSimulationFreeze",
        TargetBehavior = "Scenario authoring keeps the simulation and clock frozen while its workspace is active.",
        FailureMode = "The editor hides the pause menu, but shelter actors and in-game time keep advancing.",
        RollbackStrategy = "Disable the scenario editor option or remove the editor assembly.",
        ManagerToggleId = ScenarioEditorFeature.EnabledOptionId,
        ManagerToggleLabel = ScenarioEditorFeature.EnabledOptionLabel,
        ManagerToggleDescription = ScenarioEditorFeature.EnabledOptionDescription,
        ManagerToggleDefault = false,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 100,
        StartupTiming = PatchStartupTiming.EditorDeferred)]
    internal static class ScenarioAuthoringSimulationFreezePatches
    {
        [HarmonyPatch(typeof(UIPanelManager), "timePaused", MethodType.Getter)]
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
            return !ScenarioAuthoringRuntimeGuards.ShouldMaintainPausedSimulation()
                && !ScenarioAuthoringRuntimeGuards.IsOpeningCutscenePreviewActive();
        }
    }
}
