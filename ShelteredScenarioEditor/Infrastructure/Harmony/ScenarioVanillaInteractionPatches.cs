using ShelteredScenarioEditor.Shared;
using System;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

using ShelteredScenarioEditor.Application.Authoring;
using ShelteredScenarioEditor.Composition;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredScenarioEditor.Infrastructure.Harmony{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioVanillaInteractionAuthoring",
        TargetBehavior = "Scenario authoring routes object interactions through vanilla Obj_Base/Int_Base while blocking world-exit interactions.",
        FailureMode = "Authoring object clicks may either remain editor-only or allow unsafe vanilla exits.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the vanilla interaction authoring patches.",
        ManagerToggleId = ScenarioEditorFeature.EnabledOptionId,
        ManagerToggleLabel = ScenarioEditorFeature.EnabledOptionLabel,
        ManagerToggleDescription = ScenarioEditorFeature.EnabledOptionDescription,
        ManagerToggleDefault = false,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 100,
        StartupTiming = PatchStartupTiming.EditorDeferred)]
    internal static class ScenarioVanillaInteractionPatches
    {
        [HarmonyPatch(typeof(Obj_Base), "OnInteractionSelected", new[] { typeof(FamilyMember), typeof(string) })]
        [HarmonyPrefix]
        private static bool ObjBaseOnInteractionSelectedPrefix(Obj_Base __instance, FamilyMember member, string type, ref bool __result)
        {
            try
            {
                ScenarioVanillaInteractionRuntimeService service = ScenarioCompositionRoot.Resolve<ScenarioVanillaInteractionRuntimeService>();
                string message;
                if (service != null && service.TryBlockInteraction(__instance, type, out message))
                {
                    __result = false;
                    return false;
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioVanillaInteraction.Policy", ex.Message);
            }

            return true;
        }

        [HarmonyPatch(typeof(Obj_Base), "OnInteractionSelected", new[] { typeof(FamilyMember), typeof(string) })]
        [HarmonyPostfix]
        private static void ObjBaseOnInteractionSelectedPostfix(Obj_Base __instance, FamilyMember member, string type, bool __result)
        {
            try
            {
                ScenarioVanillaInteractionRuntimeService service = ScenarioCompositionRoot.Resolve<ScenarioVanillaInteractionRuntimeService>();
                if (service != null)
                    service.TrackInteractionResult(member, __instance, type, __result);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioVanillaInteraction.Track", ex.Message);
            }
        }

        [HarmonyPatch(typeof(MainMenuPanel), "OnSaveExitButton")]
        [HarmonyPrefix]
        private static bool MainMenuPanelOnSaveExitButtonPrefix()
        {
            if (!ScenarioAuthoringRuntimeGuards.IsAuthoringActive())
                return true;

            ScenarioAuthoringBackendService.Instance.SetStatusMessage("Vanilla Save & Exit is blocked while authoring; use the editor save and exit controls.");
            return false;
        }
    }
}
