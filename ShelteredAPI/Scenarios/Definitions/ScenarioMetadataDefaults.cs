using System;

namespace ShelteredAPI.Scenarios.Definitions
{
    /// <summary>
    /// Canonical draft-safe defaults and semantic-version helpers for scenario metadata.
    /// </summary>
    internal static class ScenarioMetadataDefaults
    {
        internal const string DefaultTitle = "Untitled Scenario";
        internal const string DefaultAuthor = "unknown";
        internal const string DefaultVersion = "0.1.0";

        internal static string ForLoad(string value, string fallback)
        {
            return value == null ? fallback : value;
        }

        internal static string BumpVersion(string value, bool minor)
        {
            Version parsed;
            try
            {
                parsed = new Version(value ?? string.Empty);
            }
            catch
            {
                parsed = new Version(0, 1, 0);
            }

            int major = parsed.Major < 0 ? 0 : parsed.Major;
            int minorPart = parsed.Minor < 0 ? 0 : parsed.Minor;
            int build = parsed.Build < 0 ? 0 : parsed.Build;
            return minor
                ? major.ToString() + "." + (minorPart + 1).ToString() + ".0"
                : major.ToString() + "." + minorPart.ToString() + "." + (build + 1).ToString();
        }
    }
}
