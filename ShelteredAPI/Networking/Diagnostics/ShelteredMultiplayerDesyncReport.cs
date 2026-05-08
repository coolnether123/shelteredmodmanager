using System.Collections.Generic;

namespace ShelteredAPI.Networking.Diagnostics
{
    internal sealed class ShelteredMultiplayerDesyncReport
    {
        public string SessionId = string.Empty;
        public long WorldTick;
        public string CompatibilityHash = string.Empty;
        public string RngDigest = string.Empty;
        public string LatestEventId = string.Empty;
        public int EventJournalCount;
        public int MapEntityCount;
        public int ActiveTravelCount;
        public string BunkerAssignmentSummary = string.Empty;
        public readonly List<string> RecentWarnings = new List<string>();

        public string ToText()
        {
            System.Text.StringBuilder builder = new System.Text.StringBuilder();
            builder.AppendLine("sessionId=" + SessionId);
            builder.AppendLine("worldTick=" + WorldTick);
            builder.AppendLine("compatibilityHash=" + CompatibilityHash);
            builder.AppendLine("rngDigest=" + RngDigest);
            builder.AppendLine("latestEventId=" + LatestEventId);
            builder.AppendLine("eventJournalCount=" + EventJournalCount);
            builder.AppendLine("mapEntityCount=" + MapEntityCount);
            builder.AppendLine("activeTravelCount=" + ActiveTravelCount);
            builder.AppendLine("bunkers=" + BunkerAssignmentSummary);
            builder.AppendLine("warnings=");
            for (int i = 0; i < RecentWarnings.Count; i++)
                builder.AppendLine("- " + RecentWarnings[i]);
            return builder.ToString();
        }
    }
}
