using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

using ShelteredAPI.Scenarios.Application.Authoring;
using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Infrastructure.Harmony{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioStorageAuthoringLiveTruth",
        TargetBehavior = "Scenario storage authoring adopts vanilla StoragePanel context-menu mutations into the scenario draft.",
        FailureMode = "Vanilla storage discard actions may be undone by the authoring inventory projection.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the storage authoring live-truth patch.",
        ManagerToggleId = ScenarioFeatureToggles.CustomScenarioEditorPatchToggleId,
        ManagerToggleLabel = ScenarioFeatureToggles.CustomScenarioEditorPatchLabel,
        ManagerToggleDescription = ScenarioFeatureToggles.CustomScenarioEditorPatchDescription,
        ManagerToggleDefault = true,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 101,
        StartupTiming = PatchStartupTiming.BootCritical)]
    internal static class ScenarioStorageAuthoringPatches
    {
        [HarmonyPatch(typeof(StoragePanel), "OnContextMenuSelected", new[] { typeof(string) })]
        [HarmonyPostfix]
        private static void StoragePanelOnContextMenuSelectedPostfix()
        {
            try
            {
                if (!ScenarioAuthoringRuntimeGuards.IsStorageAuthoringActive())
                    return;

                ScenarioEditorSession session = ScenarioEditorController.Instance.CurrentSession;
                if (session == null)
                    return;

                ScenarioAuthoringInventoryProjectionService projection =
                    ScenarioCompositionRoot.Resolve<ScenarioAuthoringInventoryProjectionService>();
                if (projection == null)
                    return;

                string message;
                if (projection.TryReconcileLiveTruth(session, "vanilla storage action", out message)
                    && !string.IsNullOrEmpty(message))
                {
                    ScenarioAuthoringBackendService.Instance.SetStatusMessage(message);
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioStorageAuthoring.ContextMenuLiveTruth", ex.Message);
            }
        }
    }
}
