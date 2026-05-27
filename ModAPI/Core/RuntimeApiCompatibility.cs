using System;

namespace ModAPI.Core
{
    internal delegate bool RuntimeApiVersionResolver(string apiName, out string version, out string failureReason);

    internal static class RuntimeApiCompatibility
    {
        internal const string ModApiName = "ModAPI";
        internal const string DefaultRuntimeApiName = "GameRuntime";

        public static bool IsRuntimeApiCompatible(
            ModAbout about,
            RuntimeApiVersionResolver versionResolver,
            out string reason)
        {
            reason = null;
            if (about == null)
                return true;

            if (!IsApiRequirementSatisfied(
                ModApiName,
                FirstNonEmpty(about.requiredModApiVersion, about.modApiVersion),
                versionResolver,
                out reason))
            {
                return false;
            }

            string runtimeApiRequirement = FirstNonEmpty(about.requiredRuntimeApiVersion, about.runtimeApiVersion);
            if (runtimeApiRequirement != null && !IsApiRequirementSatisfied(
                FirstNonEmpty(about.runtimeApiName, DefaultRuntimeApiName),
                runtimeApiRequirement,
                versionResolver,
                out reason))
            {
                return false;
            }

            return true;
        }

        public static bool IsVersionAtLeast(string current, string required)
        {
            Version currentVersion;
            Version requiredVersion;
            if (!TryParseVersion(current, out currentVersion) || !TryParseVersion(required, out requiredVersion))
                return false;

            return currentVersion.CompareTo(requiredVersion) >= 0;
        }

        private static bool IsApiRequirementSatisfied(
            string apiName,
            string requirement,
            RuntimeApiVersionResolver versionResolver,
            out string reason)
        {
            reason = null;
            string required = TrimToNull(requirement);
            if (required == null)
                return true;

            Version requiredVersion;
            if (!TryParseVersion(required, out requiredVersion))
            {
                reason = "Requires " + apiName + " '" + required + "', but the declared requirement is malformed.";
                return false;
            }

            string current;
            string failureReason = null;
            if (versionResolver == null || !versionResolver(apiName, out current, out failureReason))
            {
                reason = "Requires " + apiName + " " + required + " but runtime version is "
                    + (TrimToNull(failureReason) ?? "unavailable") + ".";
                return false;
            }

            Version currentVersion;
            if (!TryParseVersion(current, out currentVersion))
            {
                reason = "Requires " + apiName + " " + required + " but runtime version '"
                    + (current ?? string.Empty) + "' is malformed.";
                return false;
            }

            if (currentVersion.CompareTo(requiredVersion) < 0)
            {
                reason = "Requires " + apiName + " " + required + " but runtime has " + current + ".";
                return false;
            }

            return true;
        }

        private static bool TryParseVersion(string value, out Version version)
        {
            version = null;
            string normalized = TrimToNull(value);
            if (normalized == null)
                return false;

            int suffix = normalized.IndexOf('-');
            if (suffix >= 0)
                normalized = normalized.Substring(0, suffix);

            normalized = TrimToNull(normalized);
            if (normalized == null)
                return false;

            string[] parts = normalized.Split('.');
            if (parts.Length == 0 || parts.Length > 4)
                return false;

            int[] numbers = new int[] { 0, 0, 0, 0 };
            for (int i = 0; i < parts.Length; i++)
            {
                if (!TryParseVersionPart(parts[i], out numbers[i]))
                    return false;
            }

            version = new Version(numbers[0], numbers[1], numbers[2], numbers[3]);
            return true;
        }

        private static bool TryParseVersionPart(string value, out int number)
        {
            number = 0;
            string normalized = TrimToNull(value);
            if (normalized == null)
                return false;

            for (int i = 0; i < normalized.Length; i++)
            {
                if (!char.IsDigit(normalized[i]))
                    return false;
            }

            return int.TryParse(normalized, out number);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; values != null && i < values.Length; i++)
            {
                string normalized = TrimToNull(values[i]);
                if (normalized != null)
                    return normalized;
            }

            return null;
        }

        private static string TrimToNull(string value)
        {
            if (string.IsNullOrEmpty(value))
                return null;

            string trimmed = value.Trim();
            return trimmed.Length == 0 ? null : trimmed;
        }
    }
}
