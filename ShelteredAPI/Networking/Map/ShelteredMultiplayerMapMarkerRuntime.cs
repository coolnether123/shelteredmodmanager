using System;
using System.Collections.Generic;
using ModAPI.Core;

namespace ShelteredAPI.Networking.Map
{
    internal static class ShelteredMultiplayerMapMarkerRuntime
    {
        private const string LogSource = "ShelteredAPI.Multiplayer.MapMarkerRuntime";
        private static readonly ShelteredMultiplayerMapMarkerRenderer _renderer =
            new ShelteredMultiplayerMapMarkerRenderer();

        public static bool Enabled;

        public static void Refresh(string reason)
        {
            try
            {
                if (!ShouldRender())
                {
                    _renderer.Clear();
                    return;
                }

                List<ShelteredMultiplayerMapMarker> markers =
                    ShelteredMultiplayerMapMarkerService.Instance.BuildMarkers();
                _renderer.Render(markers);
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ShelteredMultiplayerMapMarkerRuntime.Refresh",
                    "Multiplayer map marker refresh failed: " + ex.Message);
            }
        }

        public static void Clear(string reason)
        {
            try
            {
                _renderer.Clear();
            }
            catch (Exception ex)
            {
                MMLog.WriteWithSource(MMLog.LogLevel.Warning, MMLog.LogCategory.Network, LogSource,
                    "Clearing multiplayer map markers failed for " + (reason ?? string.Empty) + ": " + ex.Message);
            }
        }

        private static bool ShouldRender()
        {
            if (!Enabled)
                return false;

            ShelteredMultiplayerSessionContext context = ShelteredMultiplayerSessionCoordinator.Instance.Context;
            return context != null && context.IsMultiplayerActive;
        }
    }
}
