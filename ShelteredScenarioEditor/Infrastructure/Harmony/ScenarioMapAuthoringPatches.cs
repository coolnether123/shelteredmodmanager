using ShelteredScenarioEditor.Shared;
using System;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using UnityEngine;

using ShelteredScenarioEditor.Composition;
using ShelteredScenarioEditor.Infrastructure.Unity;
using ShelteredAPI.Scenarios.Shared;

namespace ShelteredScenarioEditor.Infrastructure.Harmony{
    [PatchPolicy(PatchDomain.Scenarios, "ScenarioMapAuthoringAdapter",
        TargetBehavior = "Scenario map authoring observes vanilla expedition-map cursor regions and map-button presses without changing vanilla map behavior.",
        FailureMode = "The map authoring page can open the vanilla map, but clicks will not select vanilla regions for capture.",
        RollbackStrategy = "Disable the Scenarios patch domain or remove the scenario map authoring adapter patch host.",
        ManagerToggleId = ScenarioEditorFeature.EnabledOptionId,
        ManagerToggleLabel = ScenarioEditorFeature.EnabledOptionLabel,
        ManagerToggleDescription = ScenarioEditorFeature.EnabledOptionDescription,
        ManagerToggleDefault = false,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 100,
        StartupTiming = PatchStartupTiming.EditorDeferred)]
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
                {
                    Vector2 worldPosition;
                    service.ObserveHoveredRegion(
                        __instance,
                        __result,
                        TryGetWorldPositionUnderCursor(__instance, out worldPosition) ? (Vector2?)worldPosition : null);
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioMapAuthoring.ObserveHover", ex.Message);
            }
        }

        [HarmonyPatch(typeof(MapButton), "OnPress")]
        [HarmonyPostfix]
        private static void MapButtonOnPressPostfix(MapButton __instance, bool isDown)
        {
            if (!isDown || UICamera.currentTouchID >= 0 || UICamera.currentTouchID == -2)
                return;

            try
            {
                ScenarioMapAuthoringRuntimeService service = ScenarioCompositionRoot.Resolve<ScenarioMapAuthoringRuntimeService>();
                FieldInfo mapField = AccessTools.Field(typeof(MapButton), "map_screen");
                UI_ExpeditionMap map = mapField != null && __instance != null
                    ? mapField.GetValue(__instance) as UI_ExpeditionMap
                    : null;
                Vector2 worldPosition;
                if (service != null && TryGetWorldPositionUnderCursor(map, out worldPosition))
                    service.ClickMap(map, worldPosition, "map-button");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ScenarioMapAuthoring.MapButtonPress", ex.Message);
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
