using System;
using System.Globalization;

namespace ShelteredAPI.Networking.Diagnostics
{
    internal static class ShelteredMultiplayerTimelineFormatter
    {
        private const int MaxMessageLength = 180;

        public static string[] FormatCompact(ShelteredMultiplayerTimelineEntry[] entries, int maxEntries)
        {
            if (entries == null || entries.Length == 0 || maxEntries == 0)
                return new string[0];

            int count = entries.Length;
            if (maxEntries > 0 && count > maxEntries)
                count = maxEntries;

            int start = entries.Length - count;
            string[] lines = new string[count];
            for (int i = 0; i < count; i++)
                lines[i] = FormatCompact(entries[start + i]);

            return lines;
        }

        public static string FormatCompact(ShelteredMultiplayerTimelineEntry entry)
        {
            if (entry == null)
                return string.Empty;

            return FormatLocalTime(entry.TimestampUtc)
                + " #" + entry.Sequence.ToString(CultureInfo.InvariantCulture)
                + " [" + entry.Category + "] " + entry.EventKind
                + " role=" + entry.Mode
                + " sid=" + FormatEmpty(entry.ShortSessionId)
                + " lp=" + entry.LocalPlayerId.ToString(CultureInfo.InvariantCulture)
                + " peer=" + FormatPeer(entry.NetworkPeerId)
                + " phase=" + entry.SetupPhase
                + " tick=" + entry.WorldTick.ToString(CultureInfo.InvariantCulture)
                + " " + TrimMessage(entry.Message);
        }

        private static string FormatLocalTime(DateTime utc)
        {
            if (utc == DateTime.MinValue)
                return "--:--:--";

            try
            {
                return utc.ToLocalTime().ToString("HH:mm:ss", CultureInfo.InvariantCulture);
            }
            catch
            {
                return "--:--:--";
            }
        }

        private static string FormatEmpty(string value)
        {
            return string.IsNullOrEmpty(value) ? "-" : value;
        }

        private static string FormatPeer(int networkPeerId)
        {
            return networkPeerId >= 0
                ? networkPeerId.ToString(CultureInfo.InvariantCulture)
                : "-";
        }

        private static string TrimMessage(string message)
        {
            if (string.IsNullOrEmpty(message))
                return string.Empty;

            string clean = message.Replace('\r', ' ').Replace('\n', ' ').Trim();
            if (clean.Length <= MaxMessageLength)
                return clean;

            return clean.Substring(0, MaxMessageLength - 3) + "...";
        }
    }
}
