using System;
using System.Text.RegularExpressions;
using Manager.Core.Models;

namespace Manager.Core.Services
{
    public enum NexusReleaseChannel
    {
        Stable = 0,
        Prerelease = 1,
        Preview = 2,
        Alpha = 3,
        Beta = 4,
        ReleaseCandidate = 5
    }

    /// <summary>
    /// Classifies loose Nexus version/file labels without changing update version ordering.
    /// </summary>
    public static class NexusReleaseClassifier
    {
        private static readonly Regex ReleaseCandidatePattern = new Regex(@"(^|[^a-z0-9])(?:rc|release[\s._-]*candidate)(?:[\s._-]*\d+)?($|[^a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex BetaPattern = new Regex(@"(^|[^a-z0-9])beta(?:[\s._-]*\d+)?($|[^a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex AlphaPattern = new Regex(@"(^|[^a-z0-9])alpha(?:[\s._-]*\d+)?($|[^a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PreviewPattern = new Regex(@"(^|[^a-z0-9])(?:preview|experimental|nightly|dev(?:elopment)?|test)(?:[\s._-]*\d+)?($|[^a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private static readonly Regex PrereleasePattern = new Regex(@"(^|[^a-z0-9])(?:pre[\s._-]*release|prerelease|pre)(?:[\s._-]*\d+)?($|[^a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public static NexusReleaseChannel ClassifyVersion(string version)
        {
            return Classify(version, null);
        }

        public static NexusReleaseChannel ClassifyFile(NexusRemoteModFile file)
        {
            if (file == null)
                return NexusReleaseChannel.Stable;

            return Classify(file.Version, file.Name);
        }

        public static bool IsPrerelease(string version)
        {
            return ClassifyVersion(version) != NexusReleaseChannel.Stable;
        }

        public static bool IsPrerelease(NexusRemoteModFile file)
        {
            return ClassifyFile(file) != NexusReleaseChannel.Stable;
        }

        public static bool IsBeta(string version)
        {
            return ClassifyVersion(version) == NexusReleaseChannel.Beta;
        }

        public static string GetDisplayLabel(NexusReleaseChannel channel)
        {
            switch (channel)
            {
                case NexusReleaseChannel.ReleaseCandidate:
                    return "release candidate";
                case NexusReleaseChannel.Beta:
                    return "beta";
                case NexusReleaseChannel.Alpha:
                    return "alpha";
                case NexusReleaseChannel.Preview:
                    return "preview";
                case NexusReleaseChannel.Prerelease:
                    return "prerelease";
                default:
                    return "stable";
            }
        }

        private static NexusReleaseChannel Classify(string version, string name)
        {
            string text = ((version ?? string.Empty) + " " + (name ?? string.Empty)).Trim();
            if (text.Length == 0)
                return NexusReleaseChannel.Stable;

            if (ReleaseCandidatePattern.IsMatch(text))
                return NexusReleaseChannel.ReleaseCandidate;
            if (BetaPattern.IsMatch(text))
                return NexusReleaseChannel.Beta;
            if (AlphaPattern.IsMatch(text))
                return NexusReleaseChannel.Alpha;
            if (PreviewPattern.IsMatch(text))
                return NexusReleaseChannel.Preview;
            if (PrereleasePattern.IsMatch(text))
                return NexusReleaseChannel.Prerelease;

            return NexusReleaseChannel.Stable;
        }
    }
}
