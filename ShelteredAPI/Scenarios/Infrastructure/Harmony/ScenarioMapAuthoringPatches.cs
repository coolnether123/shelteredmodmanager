using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;

using ShelteredAPI.Scenarios.Composition;
using ShelteredAPI.Scenarios.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredAPI.Scenarios.Infrastructure.Harmony{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioMapAuthoringAdapter",
        TargetBehavior = "Scenario map authoring observes vanilla expedition-map cursor regions and click presses without changing vanilla map behavior.",
        FailureMode = "The map authoring page can open the vanilla map, but clicks will not select vanilla regions for capture.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario map authoring adapter patch host.",
        ManagerToggleId = ScenarioFeatureToggles.CustomScenarioEditorPatchToggleId,
        ManagerToggleLabel = ScenarioFeatureToggles.CustomScenarioEditorPatchLabel,
        ManagerToggleDescription = ScenarioFeatureToggles.CustomScenarioEditorPatchDescription,
        ManagerToggleDefault = true,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 100,
        StartupTiming = PatchStartupTiming.BootCritical)]
    internal static class ScenarioMapAuthoringPatches
    {
        [HarmonyPatch(typeof(UI_ExpeditionMap), "GetMapRegionUnderCursor")]
        [HarmonyPostfix]
        private static void GetMapRegionUnderCursorPostfix(UI_ExpeditionMap __instance, MapRegion __result)
        {
            try
            {
                ScenarioMapAuthoringRuntimeService service = ScenarioCompositionRoot.Resolve<ScenarioMapAuthoringRuntimeService>();
                if (service != null)
                    service.ObserveHoveredRegion(__instance, __result);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioMapAuthoring.ObserveHover", ex.Message);
            }
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "OnPress")]
        [HarmonyPostfix]
        private static void OnPressPostfix(UI_ExpeditionMap __instance, bool rightClick)
        {
            if (rightClick)
                return;

            try
            {
                ScenarioMapAuthoringRuntimeService service = ScenarioCompositionRoot.Resolve<ScenarioMapAuthoringRuntimeService>();
                Vector2 worldPosition;
                if (service != null && TryGetWorldPositionUnderCursor(__instance, out worldPosition))
                    service.ClickMap(__instance, worldPosition, "click");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioMapAuthoring.SelectHover", ex.Message);
            }
        }

        [HarmonyPatch(typeof(UI_ExpeditionMap), "OnDisable")]
        [HarmonyPostfix]
        private static void OnDisablePostfix()
        {
            try
            {
                ScenarioMapAuthoringRuntimeService service = ScenarioCompositionRoot.Resolve<ScenarioMapAuthoringRuntimeService>();
                if (service != null)
                    service.CleanupMarkers();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioMapAuthoring.CleanupMarkers", ex.Message);
            }
        }

        private static bool TryGetWorldPositionUnderCursor(UI_ExpeditionMap map, out Vector2 position)
        {
            position = Vector2.zero;
            if (map == null)
                return false;

            MethodInfo method = AccessTools.Method(typeof(UI_ExpeditionMap), "WorldPositionUnderCursor");
            if (method == null)
                return false;

            object value = method.Invoke(map, null);
            if (!(value is Vector2))
                return false;

            position = (Vector2)value;
            return true;
        }
    }
}
