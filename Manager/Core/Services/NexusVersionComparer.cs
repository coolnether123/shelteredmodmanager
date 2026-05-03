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
        private static readonly Regex PrereleaseTokenPattern = new Regex(@"(^|[^a-z0-9])(?:rc|release[\s._-]*candidate|beta|alpha|preview|experimental|nightly|dev(?:elopment)?|test|pre[\s._-]*release|prerelease|pre)(?:[\s._-]*\d+)?($|[^a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        /// <summary>
        /// Compares local and remote versions.
        /// Returns -1 when local is older, 0 when equal/unknown, 1 when local is newer.
        /// </summary>
        public static int CompareVersions(string localVersion, string remoteVersion)
        {
            var local = Normalize(localVersion);
            var remote = Normalize(remoteVersion);

            if (string.IsNullOrEmpty(local) && string.IsNullOrEmpty(remote))
                return 0;
            if (string.IsNullOrEmpty(local) && !string.IsNullOrEmpty(remote))
                return -1;
            if (!string.IsNullOrEmpty(local) && string.IsNullOrEmpty(remote))
                return 1;

            List<int> localParts;
            List<int> remoteParts;
            bool localParsed = TryParseNumericParts(GetCoreVersionText(local), out localParts);
            bool remoteParsed = TryParseNumericParts(GetCoreVersionText(remote), out remoteParts);

            if (localParsed && remoteParsed)
            {
                int max = Math.Max(localParts.Count, remoteParts.Count);
                for (int i = 0; i < max; i++)
                {
                    int lv = i < localParts.Count ? localParts[i] : 0;
                    int rv = i < remoteParts.Count ? remoteParts[i] : 0;
                    if (lv < rv) return -1;
                    if (lv > rv) return 1;
                }

                NexusReleaseChannel localChannel = NexusReleaseClassifier.ClassifyVersion(local);
                NexusReleaseChannel remoteChannel = NexusReleaseClassifier.ClassifyVersion(remote);
                int channelComparison = GetReleaseChannelRank(localChannel).CompareTo(GetReleaseChannelRank(remoteChannel));
                if (channelComparison != 0)
                    return channelComparison < 0 ? -1 : 1;

                if (localChannel != NexusReleaseChannel.Stable && remoteChannel != NexusReleaseChannel.Stable)
                {
                    List<int> localFullParts;
                    List<int> remoteFullParts;
                    if (TryParseNumericParts(local, out localFullParts) && TryParseNumericParts(remote, out remoteFullParts))
                    {
                        int fullMax = Math.Max(localFullParts.Count, remoteFullParts.Count);
                        for (int i = 0; i < fullMax; i++)
                        {
                            int lv = i < localFullParts.Count ? localFullParts[i] : 0;
                            int rv = i < remoteFullParts.Count ? remoteFullParts[i] : 0;
                            if (lv < rv) return -1;
                            if (lv > rv) return 1;
                        }
                    }
                }

                return 0;
            }

            if (string.Equals(local, remote, StringComparison.OrdinalIgnoreCase))
                return 0;

            // Fallback when versions are non-numeric labels.
            return string.Compare(local, remote, StringComparison.OrdinalIgnoreCase) < 0 ? -1 : 1;
        }

        public static bool IsRemoteNewer(string localVersion, string remoteVersion)
        {
            return CompareVersions(localVersion, remoteVersion) < 0;
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

        private static string GetCoreVersionText(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            Match prerelease = PrereleaseTokenPattern.Match(value);
            if (prerelease.Success && prerelease.Index > 0)
                return value.Substring(0, prerelease.Index);

            return value;
        }

        private static int GetReleaseChannelRank(NexusReleaseChannel channel)
        {
            switch (channel)
            {
                case NexusReleaseChannel.Stable:
                    return 100;
                case NexusReleaseChannel.ReleaseCandidate:
                    return 90;
                case NexusReleaseChannel.Beta:
                    return 80;
                case NexusReleaseChannel.Preview:
                    return 70;
                case NexusReleaseChannel.Alpha:
                    return 60;
                case NexusReleaseChannel.Prerelease:
                    return 50;
                default:
                    return 0;
            }
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
