using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;
using ShelteredAPI.Networking.Diagnostics;
using UnityEngine;

namespace ShelteredAPI.Networking
{
    [PatchPolicy(PatchDomain.World, "ShelteredMultiplayerShelterMapAnchor",
        TargetBehavior = "Vanilla map UI and expedition code read the active multiplayer bunker position instead of the default center shelter anchor.",
        FailureMode = "Every peer sees and routes from a center-map shelter even after multiplayer bunker assignments are applied.",
        RollbackStrategy = "Disable the World patch domain or remove the multiplayer shelter map-anchor patch host.",
        StartupTiming = PatchStartupTiming.GameplayDeferred)]
    [HarmonyPatch(typeof(UI_ExpeditionMap))]
    internal static class ShelteredMultiplayerExpeditionMapUiAnchorPatches
    {
        private static readonly FieldInfo MapCameraField =
            AccessTools.Field(typeof(UI_ExpeditionMap), "m_mapCamera");
        private static readonly FieldInfo MapZoomField =
            AccessTools.Field(typeof(UI_ExpeditionMap), "m_mapZoom");
        private static readonly HashSet<int> SnappedInstances = new HashSet<int>();

        [HarmonyPatch("UpdateMapSymbols")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> UpdateMapSymbolsTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            return ShelteredMultiplayerShelterAnchorTranspiler.ReplaceShelterMapGetterPairs(instructions);
        }

        [HarmonyPatch("OnEnable")]
        [HarmonyPrefix]
        private static void ExpeditionMapOnEnablePrefix()
        {
            try
            {
                ShelteredMultiplayerBunkerAnchorRuntime.CacheActiveBunkerPosition("UI_ExpeditionMap.OnEnable");
                ShelteredMultiplayerMapAnchorDiagnostics.LogReport("UI_ExpeditionMap.OnEnable");
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerShelterMapAnchor.OnEnable",
                    "Multiplayer shelter map-anchor UI hook failed: " + ex.Message);
            }
        }

        [HarmonyPatch("OnEnable")]
        [HarmonyPostfix]
        private static void ExpeditionMapOnEnablePostfix(UI_ExpeditionMap __instance)
        {
            if (__instance == null)
                return;

            SnappedInstances.Remove(__instance.GetInstanceID());
            TrySnapMapAndCursorToActiveBunker(__instance, "UI_ExpeditionMap.OnEnable");
        }

        [HarmonyPatch("OnDisable")]
        [HarmonyPostfix]
        private static void ExpeditionMapOnDisablePostfix(UI_ExpeditionMap __instance)
        {
            if (__instance != null)
                SnappedInstances.Remove(__instance.GetInstanceID());
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        private static void ExpeditionMapUpdatePrefix(UI_ExpeditionMap __instance)
        {
            if (__instance == null)
                return;

            int id = __instance.GetInstanceID();
            if (SnappedInstances.Contains(id))
                return;

            if (TrySnapMapAndCursorToActiveBunker(__instance, "UI_ExpeditionMap.Update"))
                SnappedInstances.Add(id);
        }

        [HarmonyPatch("UpdateCursor")]
        [HarmonyPrefix]
        private static bool ExpeditionMapUpdateCursorPrefix(UI_ExpeditionMap __instance)
        {
            if (!ShelteredMultiplayerBunkerAnchorRuntime.IsMultiplayerAnchorActive())
                return true;

            try
            {
                if (__instance == null)
                    return false;

                if (__instance.cursor == null)
                {
                    MMLog.WarnOnce("ShelteredMultiplayerShelterMapAnchor.UpdateCursor.Cursor",
                        "Skipping UI_ExpeditionMap.UpdateCursor until the map cursor is available.");
                    return false;
                }

                if (MapCameraField == null || MapCameraField.GetValue(__instance) == null)
                {
                    MMLog.WarnOnce("ShelteredMultiplayerShelterMapAnchor.UpdateCursor.Camera",
                        "Skipping UI_ExpeditionMap.UpdateCursor until the map camera is available.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerShelterMapAnchor.UpdateCursor",
                    "Multiplayer shelter map-cursor guard failed: " + ex.Message);
            }

            return true;
        }

        private static bool TrySnapMapAndCursorToActiveBunker(UI_ExpeditionMap instance, string reason)
        {
            if (!ShelteredMultiplayerBunkerAnchorRuntime.IsMultiplayerAnchorActive())
                return false;

            if (instance == null || ExplorationManager.Instance == null)
                return false;

            try
            {
                Vector3 bunkerMapPixels = ShelteredMultiplayerBunkerAnchorRuntime.GetActiveBunkerMapPixels();
                if (bunkerMapPixels.sqrMagnitude <= 0.0001f)
                    return false;

                Camera mapCamera = MapCameraField != null ? MapCameraField.GetValue(instance) as Camera : null;
                if (mapCamera != null)
                {
                    float mapZoom = 0f;
                    object zoomValue = MapZoomField != null ? MapZoomField.GetValue(instance) : null;
                    if (zoomValue is float)
                        mapZoom = (float)zoomValue;
                    if (mapZoom <= 0f)
                        mapZoom = mapCamera.orthographicSize;

                    float halfMapWidth = ExplorationManager.Instance.mapImageWidth * 0.5f;
                    float halfMapHeight = ExplorationManager.Instance.mapImageHeight * 0.5f;
                    float halfViewHeight = 540f;
                    float halfViewWidth = halfViewHeight * mapCamera.aspect;
                    float clampX = Mathf.Max(halfMapWidth - halfViewWidth * mapZoom, 0f);
                    float clampY = Mathf.Max(halfMapHeight - halfViewHeight * mapZoom, 0f);

                    Vector3 cameraPosition = mapCamera.transform.localPosition;
                    cameraPosition.x = Mathf.Clamp(bunkerMapPixels.x, -clampX, clampX);
                    cameraPosition.y = Mathf.Clamp(bunkerMapPixels.y, -clampY, clampY);
                    mapCamera.transform.localPosition = cameraPosition;
                }

                if (instance.cursor != null)
                    instance.cursor.transform.localPosition = Vector3.zero;

                return mapCamera != null || instance.cursor != null;
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerShelterMapAnchor.Snap." + (reason ?? string.Empty),
                    "Multiplayer shelter map snap failed: " + ex.Message);
                return false;
            }
        }
    }
}
