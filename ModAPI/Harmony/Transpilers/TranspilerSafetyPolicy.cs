using System;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Central policy surface for transpiler safety behavior.
    /// Keeps safety decisions in one place so FluentTranspiler and CooperativePatcher
    /// do not duplicate preference parsing logic.
    /// </summary>
    internal static class TranspilerSafetyPolicy
    {
        private const string PreserveWarnKeyPrefix = "TranspilerSafetyPolicy.Preserve.";

        public static bool SafeModeEnabled => ModPrefs.TranspilerSafeMode;
        public static bool ForcePreserveInstructionCount => ModPrefs.TranspilerForcePreserveInstructionCount;
        public static bool FailFastOnCritical => ModPrefs.TranspilerFailFastCritical;
        public static bool CooperativeStrictBuild => ModPrefs.TranspilerCooperativeStrictBuild;
        public static bool QuarantineOwnerOnFailure => ModPrefs.TranspilerQuarantineOnFailure;

        /// <summary>
        /// Resolves the effective preserve-count mode for pattern replacement.
        /// In safe mode we force preserve=true because branch targets can point into replaced spans.
        /// </summary>
        public static bool ResolvePreserveInstructionCount(bool requestedPreserve)
        {
            if (!SafeModeEnabled) return requestedPreserve;
            if (!ForcePreserveInstructionCount) return requestedPreserve;
            return true;
        }

        /// <summary>
        /// True when user requested preserve=false but policy upgraded it to safe mode.
        /// </summary>
        public static bool IsPreserveEscalated(bool requestedPreserve, bool effectivePreserve)
        {
            return !requestedPreserve && effectivePreserve;
        }

        /// <summary>
        /// Emits a one-time warning when preserve mode is force-enabled by policy.
        /// </summary>
        public static void WarnPreserveEscalation(string callerMod, string methodName)
        {
            var owner = string.IsNullOrEmpty(callerMod) ? "UnknownOwner" : callerMod;
            var method = string.IsNullOrEmpty(methodName) ? "UnknownMethod" : methodName;
            MMLog.WarnOnce(
                PreserveWarnKeyPrefix + owner + "." + method,
                "[TranspilerSafety] Safe mode forced preserveInstructionCount=true for " + owner + " patch on " + method + ".");
        }

        /// <summary>
        /// Matches warnings that should abort builds when fail-fast is enabled.
        /// </summary>
        public static bool IsCriticalWarning(string warning)
        {
            if (string.IsNullOrEmpty(warning)) return false;
            return warning.IndexOf("[CRITICAL", StringComparison.OrdinalIgnoreCase) >= 0
                   || warning.IndexOf("Stack Error:", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
