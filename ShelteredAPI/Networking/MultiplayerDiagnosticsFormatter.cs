using System;
using ModAPI.Networking.Diagnostics;

namespace ShelteredAPI.Networking
{
    internal static class MultiplayerDiagnosticsFormatter
    {
        public static string FormatAge(DateTime utc)
        {
            if (utc == DateTime.MinValue)
                return "never";

            double seconds = (DateTime.UtcNow - utc).TotalSeconds;
            if (seconds < 0)
                seconds = 0;
            if (seconds < 1)
                return "now";
            if (seconds < 60)
                return seconds.ToString("0.0") + "s ago";

            return (seconds / 60.0).ToString("0.0") + "m ago";
        }

        public static string FormatLocalTime(DateTime utc)
        {
            try
            {
                return utc.ToLocalTime().ToString("HH:mm:ss");
            }
            catch
            {
                return "unknown";
            }
        }

        public static string FormatBytes(long bytes)
        {
            if (bytes < 1024)
                return bytes + " B";

            double kilobytes = bytes / 1024.0;
            if (kilobytes < 1024)
                return kilobytes.ToString("0.0") + " KB";

            return (kilobytes / 1024.0).ToString("0.0") + " MB";
        }

        public static string FormatEndpoint(NetworkPeerDiagnosticsSnapshot peer)
        {
            if (peer == null || peer.EndPoint == null)
                return "unknown endpoint";

            return peer.EndPoint.ToString();
        }

        public static string FormatLatency(NetworkPeerDiagnosticsSnapshot peer)
        {
            if (peer == null || !peer.HeartbeatLatencyMilliseconds.HasValue)
                return "unknown";

            return peer.HeartbeatLatencyMilliseconds.Value.ToString("0") + " ms";
        }

        public static string ExtractEndpoint(string discoveryLine)
        {
            if (string.IsNullOrEmpty(discoveryLine))
                return string.Empty;

            int separator = discoveryLine.IndexOf('|');
            if (separator < 0)
                return discoveryLine.Trim();

            return discoveryLine.Substring(0, separator).Trim();
        }

        public static bool HasUsableDiscoveryEndpoint(string endpoint)
        {
            return !string.IsNullOrEmpty(endpoint)
                && endpoint.IndexOf("No hosts", StringComparison.OrdinalIgnoreCase) < 0;
        }
    }
}
