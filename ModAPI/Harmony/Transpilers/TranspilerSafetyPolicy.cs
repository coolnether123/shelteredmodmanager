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
        public static bool LogValidationWarnings => ModPrefs.TranspilerLogValidationWarnings;
        public static bool WarnOnVirtualCallMismatch => ModPrefs.TranspilerWarnOnVirtualCallMismatch;
        public static bool WarnOnExceptionHandlerMethods => ModPrefs.TranspilerWarnOnExceptionHandlerMethods;
        public static bool VerboseTracingEnabled => ModPrefs.DebugTranspilers;
        public static FluentTranspiler.BuildProfile DefaultExecuteProfile =>
            FluentTranspiler.BuildProfile.Runtime;
        public static FluentTranspiler.BuildProfile DefaultCooperativeProfile =>
            CooperativeStrictBuild ? FluentTranspiler.BuildProfile.Strict : DefaultExecuteProfile;

        internal struct BuildOptions
        {
            public bool Strict;
            public bool ValidateStack;
            public bool ForceSnapshot;
        }

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

        public static bool IsCriticalDiagnostic(TranspilerDiagnostic diagnostic)
        {
            return diagnostic != null
                   && diagnostic.Severity == TranspilerDiagnosticSeverity.Warning
                   && IsCriticalWarning(diagnostic.Message);
        }

        public static BuildOptions ResolveBuildOptions(FluentTranspiler.BuildProfile profile)
        {
            switch (profile)
            {
                case FluentTranspiler.BuildProfile.Strict:
                    return new BuildOptions { Strict = true, ValidateStack = true, ForceSnapshot = true };
                case FluentTranspiler.BuildProfile.Debug:
                    return new BuildOptions { Strict = false, ValidateStack = true, ForceSnapshot = true };
                default:
                    return new BuildOptions { Strict = false, ValidateStack = true, ForceSnapshot = false };
            }
        }

        /// <summary>
        /// Controls when FluentTranspiler should emit expensive debug snapshots.
        /// Clean builds stay quiet unless explicit transpiler debugging is enabled.
        /// </summary>
        public static bool ShouldRecordDebugSnapshot(int warningCount, int softFailureCount, int noteCount)
        {
            if (DebugTranspilerTracingEnabled())
            {
                return true;
            }

            return warningCount > 0;
        }

        private static bool DebugTranspilerTracingEnabled()
        {
            return ModPrefs.DebugTranspilers;
        }
    }
}
