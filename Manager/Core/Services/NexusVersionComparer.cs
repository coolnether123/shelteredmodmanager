using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Manager.Core.Services
{
    /// <summary>
    /// Handles loose version comparisons between local About.json versions and Nexus versions.
    /// </summary>
    public static class NexusVersionComparer
    {
        public static bool IsRemoteNewer(string localVersion, string remoteVersion)
        {
            var local = Normalize(localVersion);
            var remote = Normalize(remoteVersion);

            if (string.IsNullOrEmpty(remote))
                return false;
            if (string.IsNullOrEmpty(local))
                return true;

            List<int> localParts;
            List<int> remoteParts;
            bool localParsed = TryParseNumericParts(local, out localParts);
            bool remoteParsed = TryParseNumericParts(remote, out remoteParts);

            if (localParsed && remoteParsed)
            {
                int max = Math.Max(localParts.Count, remoteParts.Count);
                for (int i = 0; i < max; i++)
                {
                    int lv = i < localParts.Count ? localParts[i] : 0;
                    int rv = i < remoteParts.Count ? remoteParts[i] : 0;
                    if (rv > lv) return true;
                    if (rv < lv) return false;
                }
                return false;
            }

            return !string.Equals(local, remote, StringComparison.OrdinalIgnoreCase);
        }

        public static string Normalize(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            string normalized = value.Trim();
            if (normalized.StartsWith("v", StringComparison.OrdinalIgnoreCase))
                normalized = normalized.Substring(1).Trim();

            return normalized;
        }

        private static bool TryParseNumericParts(string value, out List<int> parts)
        {
            parts = new List<int>();

            if (string.IsNullOrEmpty(value))
                return false;

            var matches = Regex.Matches(value, "\\d+");
            if (matches == null || matches.Count == 0)
                return false;

            foreach (Match match in matches)
            {
                int parsed;
                if (int.TryParse(match.Value, out parsed))
                {
                    parts.Add(parsed);
                }
            }

            return parts.Count > 0;
        }
    }
}
