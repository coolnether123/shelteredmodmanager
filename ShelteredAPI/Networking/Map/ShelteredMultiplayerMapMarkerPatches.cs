using System;
using System.Collections.Generic;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ShelteredAPI.Networking.Map
{
#if DEBUG
    [HarmonyUtil.DebugPatch("ShelteredMultiplayerMapMarkers")]
    [PatchPolicy(PatchDomain.Diagnostics, "ShelteredMultiplayerMapMarkers",
        TargetBehavior = "Debug-only expedition map logs list local and remote multiplayer bunker markers.",
        FailureMode = "Remote bunker marker diagnostics are unavailable; vanilla expedition map behavior is unchanged.",
        RollbackStrategy = "Disable debug patches or the Sheltered multiplayer bunker marker manager option.",
        IsOptional = true,
        DeveloperOnly = true,
        StartupTiming = PatchStartupTiming.DebugDeferred,
        ManagerToggleId = "ShelteredMultiplayerMapMarkers",
        ManagerToggleLabel = "Multiplayer Bunker Marker Debug Overlay",
        ManagerToggleDescription = "Logs local and remote multiplayer bunker marker positions when the expedition map opens.",
        ManagerToggleDefault = false,
        ManagerToggleRequiresRestart = true,
        ManagerToggleSortOrder = 240)]
    [HarmonyPatch(typeof(UI_ExpeditionMap))]
    internal static class ShelteredMultiplayerMapMarkerPatches
    {
        private const string LogSource = "ShelteredAPI.Multiplayer.MapMarkers";
        private static int _lastLoggedFrame = -1;

        [HarmonyPatch("OnEnable")]
        [HarmonyPostfix]
        private static void OnEnablePostfix()
        {
            try
            {
                if (_lastLoggedFrame == UnityEngine.Time.frameCount)
                    return;

                _lastLoggedFrame = UnityEngine.Time.frameCount;
                ShelteredMultiplayerMapMarkerRuntime.Enabled = true;
                ShelteredMultiplayerMapMarkerRuntime.Refresh("expedition-map-opened");
                LogMarkersForMapOpen();
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapMarkers.OnEnable",
                    "Multiplayer bunker marker debug hook failed: " + ex.Message);
            }
        }

        [HarmonyPatch("OnDisable")]
        [HarmonyPostfix]
        private static void OnDisablePostfix()
        {
            ShelteredMultiplayerMapMarkerRuntime.Clear("expedition-map-closed");
        }

        private static void LogMarkersForMapOpen()
        {
            List<ShelteredMultiplayerMapMarker> markers =
                ShelteredMultiplayerMapMarkerService.Instance.BuildBunkerMarkers();
            if (markers.Count == 0)
                return;

            MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                "Expedition map opened with " + markers.Count + " multiplayer bunker marker(s).");

            for (int i = 0; i < markers.Count; i++)
            {
                ShelteredMultiplayerMapMarker marker = markers[i];
                MMLog.WriteWithSource(MMLog.LogLevel.Info, MMLog.LogCategory.Network, LogSource,
                    "Marker " + marker.MarkerId
                    + " label='" + marker.Label + "'"
                    + " owner=" + marker.BunkerOwnerId
                    + " peer=" + marker.PeerId
                    + " local=" + marker.IsLocal
                    + " online=" + marker.IsOnline
                    + " mapPixels=(" + marker.MapPixels.x.ToString("F1")
                    + ", " + marker.MapPixels.y.ToString("F1") + ").");
            }
        }
    }
#endif
}
