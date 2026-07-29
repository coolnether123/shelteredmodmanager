using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;
using ModAPI.Harmony;

namespace ParalivesAPI.Core
{
    public sealed class ParalivesPatchSummary
    {
        public string State { get; internal set; }
        public int DiscoveredCount { get; internal set; }
        public int AppliedCount { get; internal set; }
        public int AlreadyAppliedCount { get; internal set; }
        public int EffectiveAppliedCount { get; internal set; }
        public int SkippedCount { get; internal set; }
        public int MissingPolicyCount { get; internal set; }
        public int ConflictCount { get; internal set; }
        public int RequiredFailureCount { get; internal set; }
        public int OptionalFailureCount { get; internal set; }
        public string LastFailure { get; internal set; }

        internal ParalivesPatchSummary Copy()
        {
            return new ParalivesPatchSummary
            {
                State = State,
                DiscoveredCount = DiscoveredCount,
                AppliedCount = AppliedCount,
                AlreadyAppliedCount = AlreadyAppliedCount,
                EffectiveAppliedCount = EffectiveAppliedCount,
                SkippedCount = SkippedCount,
                MissingPolicyCount = MissingPolicyCount,
                ConflictCount = ConflictCount,
                RequiredFailureCount = RequiredFailureCount,
                OptionalFailureCount = OptionalFailureCount,
                LastFailure = LastFailure
            };
        }
    }

    public static class ParalivesPatchDiagnostics
    {
        public static PatchReportDto GetLatestReport()
        {
            return ParalivesHarmonyPatcher.GetLatestReport();
        }

        public static ParalivesPatchSummary GetLatestSummary()
        {
            return ParalivesHarmonyPatcher.GetLatestSummary();
        }
    }

    internal enum ParalivesPatchState
    {
        NotStarted,
        Applying,
        Applied,
        AppliedWithFailures,
        Failed
    }

    internal static class ParalivesHarmonyPatcher
    {
        private const string HarmonyId = "ParalivesAPI.Core";
        private const string SourceName = "ParalivesAPI";
        private const string TriggerName = "ParalivesHarmonyPatcher.EnsurePatched";

        private static readonly object Sync = new object();
        private static ParalivesPatchState _state = ParalivesPatchState.NotStarted;
        private static PatchReportDto _latestReport;
        private static ParalivesPatchSummary _latestSummary = CreateEmptySummary(ParalivesPatchState.NotStarted, string.Empty);

        public static void EnsurePatched()
        {
            lock (Sync)
            {
                if (_state == ParalivesPatchState.Applied
                    || _state == ParalivesPatchState.AppliedWithFailures
                    || _state == ParalivesPatchState.Applying)
                {
                    return;
                }

                _state = ParalivesPatchState.Applying;
                _latestSummary = CreateEmptySummary(_state, string.Empty);
            }

            PatchApplyReport report = null;
            PatchAttemptDiagnostics attemptDiagnostics = new PatchAttemptDiagnostics();
            ParalivesPatchState finalState = ParalivesPatchState.Failed;
            string failure = string.Empty;

            try
            {
                Harmony harmony = new Harmony(HarmonyId);
                Assembly assembly = typeof(ParalivesHarmonyPatcher).Assembly;
                PatchRegistryOptions options = CreateRegistryOptions(attemptDiagnostics);

                DeferredPatchCoordinator.RegisterSource(harmony, assembly, options);

                PatchRegistryOptions bootOptions = PatchRegistry.CreateTimingOptions(options, PatchStartupTiming.BootCritical);
                bootOptions.TriggerName = TriggerName;
                report = PatchRegistry.ApplyAssembly(harmony, assembly, bootOptions);

                ParalivesPatchSummary summary = BuildSummary(report, attemptDiagnostics);
                finalState = ResolveState(summary, out failure);
                summary.State = finalState.ToString();
                summary.LastFailure = failure;
                LogOutcome(summary);

                lock (Sync)
                {
                    _state = finalState;
                    _latestReport = CloneReport(report != null ? report.DiagnosticSnapshot : null);
                    _latestSummary = summary.Copy();
                }
            }
            catch (Exception ex)
            {
                MMLog.WarnOnce("ParalivesHarmonyPatcher.EnsurePatched", "Failed to apply ParalivesAPI Harmony patches: " + ex.Message);
                failure = ex.ToString();

                lock (Sync)
                {
                    _state = ParalivesPatchState.Failed;
                    _latestReport = CloneReport(report != null ? report.DiagnosticSnapshot : null);
                    _latestSummary = CreateEmptySummary(ParalivesPatchState.Failed, failure);
                }
            }
        }

        internal static PatchReportDto GetLatestReport()
        {
            lock (Sync)
            {
                return CloneReport(_latestReport);
            }
        }

        internal static ParalivesPatchSummary GetLatestSummary()
        {
            lock (Sync)
            {
                return _latestSummary != null
                    ? _latestSummary.Copy()
                    : CreateEmptySummary(_state, string.Empty);
            }
        }

        private static PatchRegistryOptions CreateRegistryOptions(PatchAttemptDiagnostics attemptDiagnostics)
        {
            HarmonyUtil.PatchOptions patchOptions = new HarmonyUtil.PatchOptions
            {
                AllowDebugPatches = HarmonyBootstrap.ReadManagerBool("EnableDebugPatches", false),
                AllowDangerousPatches = HarmonyBootstrap.ReadManagerBool("AllowDangerousPatches", false),
                AllowStructReturns = HarmonyBootstrap.ReadManagerBool("AllowStructReturns", false),
                OnResult = delegate(object target, string result)
                {
                    if (attemptDiagnostics != null)
                        attemptDiagnostics.Record(target, result);

                    if (!string.IsNullOrEmpty(result))
                        MMLog.WriteDebug("[ParalivesAPI] " + DescribeTarget(target) + " -> " + result);
                }
            };

            PatchRegistryOptions options = PatchRegistry.CreateManagerOptions(
                patchOptions,
                SourceName,
                key => HarmonyBootstrap.ReadManagerString(key, null));
            options.IsPatchTypeAlreadyApplied = IsPatchTypeAlreadyApplied;
            return options;
        }

        private static ParalivesPatchSummary BuildSummary(PatchApplyReport report, PatchAttemptDiagnostics attemptDiagnostics)
        {
            ParalivesPatchSummary summary = CreateEmptySummary(ParalivesPatchState.Applying, string.Empty);
            if (report == null)
                return summary;

            summary.DiscoveredCount = report.Discovered.Count;
            summary.AppliedCount = report.Applied.Count;
            summary.SkippedCount = report.Skipped.Count;
            summary.MissingPolicyCount = report.MissingPolicy.Count;
            summary.ConflictCount = report.Conflicts.Count;

            for (int i = 0; i < report.Skipped.Count; i++)
            {
                PatchRecord record = report.Skipped[i];
                if (attemptDiagnostics != null && attemptDiagnostics.IsAlreadyApplied(record))
                {
                    summary.AlreadyAppliedCount++;
                    continue;
                }

                if (attemptDiagnostics == null || !attemptDiagnostics.IsFailure(record))
                    continue;

                if (record != null && record.IsOptional)
                {
                    summary.OptionalFailureCount++;
                }
                else
                {
                    summary.RequiredFailureCount++;
                }
            }

            summary.EffectiveAppliedCount = summary.AppliedCount + summary.AlreadyAppliedCount;
            return summary;
        }

        private static ParalivesPatchState ResolveState(ParalivesPatchSummary summary, out string failure)
        {
            failure = string.Empty;
            if (summary == null || summary.DiscoveredCount == 0)
            {
                failure = "No ParalivesAPI Harmony patch hosts were discovered.";
                return ParalivesPatchState.Failed;
            }

            if (summary.RequiredFailureCount > 0)
            {
                failure = summary.RequiredFailureCount + " required ParalivesAPI patch host(s) failed to apply.";
                return ParalivesPatchState.Failed;
            }

            if (summary.OptionalFailureCount > 0)
            {
                failure = summary.OptionalFailureCount + " optional ParalivesAPI patch host(s) failed to apply.";
                return ParalivesPatchState.AppliedWithFailures;
            }

            return ParalivesPatchState.Applied;
        }

        private static void LogOutcome(ParalivesPatchSummary summary)
        {
            if (summary == null)
                return;

            string counts = "discovered=" + summary.DiscoveredCount
                + ", applied=" + summary.AppliedCount
                + ", alreadyApplied=" + summary.AlreadyAppliedCount
                + ", skipped=" + summary.SkippedCount
                + ", missingPolicy=" + summary.MissingPolicyCount
                + ", conflicts=" + summary.ConflictCount;

            if (string.Equals(summary.State, ParalivesPatchState.Failed.ToString(), StringComparison.Ordinal))
            {
                MMLog.WarnOnce(
                    "ParalivesHarmonyPatcher.RequiredFailures",
                    "Failed to apply required ParalivesAPI Harmony patches: "
                    + counts + ", requiredFailures=" + summary.RequiredFailureCount + ".");
                return;
            }

            if (string.Equals(summary.State, ParalivesPatchState.AppliedWithFailures.ToString(), StringComparison.Ordinal))
            {
                MMLog.WriteInfo("[ParalivesAPI] Harmony patches applied with optional patch failures: "
                    + counts + ", optionalFailures=" + summary.OptionalFailureCount + ".");
                return;
            }

            MMLog.WriteInfo("[ParalivesAPI] Harmony patches applied: " + counts + ".");
        }

        private static ParalivesPatchSummary CreateEmptySummary(ParalivesPatchState state, string failure)
        {
            return new ParalivesPatchSummary
            {
                State = state.ToString(),
                DiscoveredCount = 0,
                AppliedCount = 0,
                AlreadyAppliedCount = 0,
                EffectiveAppliedCount = 0,
                SkippedCount = 0,
                MissingPolicyCount = 0,
                ConflictCount = 0,
                RequiredFailureCount = 0,
                OptionalFailureCount = 0,
                LastFailure = failure ?? string.Empty
            };
        }

        private static bool IsPatchTypeAlreadyApplied(Type patchType)
        {
            IEnumerable<MethodBase> patchedMethods;
            try
            {
                patchedMethods = Harmony.GetAllPatchedMethods();
            }
            catch
            {
                return false;
            }

            foreach (MethodBase method in patchedMethods)
            {
                HarmonyLib.Patches patches = Harmony.GetPatchInfo(method);
                if (patches == null)
                    continue;

                if (ContainsPatchFromType(patches.Prefixes, patchType)
                    || ContainsPatchFromType(patches.Postfixes, patchType)
                    || ContainsPatchFromType(patches.Transpilers, patchType)
                    || ContainsPatchFromType(patches.Finalizers, patchType))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsPatchFromType(IEnumerable<Patch> patches, Type patchType)
        {
            foreach (Patch patch in patches)
            {
                if (patch != null
                    && patch.PatchMethod != null
                    && patch.PatchMethod.DeclaringType == patchType)
                {
                    return true;
                }
            }

            return false;
        }

        private static string DescribeTarget(object target)
        {
            MemberInfo member = target as MemberInfo;
            if (member == null)
                return target != null ? target.ToString() : "<null>";

            return member.DeclaringType != null
                ? member.DeclaringType.FullName + "." + member.Name
                : member.Name;
        }

        private static PatchReportDto CloneReport(PatchReportDto report)
        {
            if (report == null)
                return null;

            return new PatchReportDto
            {
                CapturedUtc = report.CapturedUtc,
                AssemblyName = report.AssemblyName,
                SourceName = report.SourceName,
                TriggerName = report.TriggerName,
                Discovered = CloneHosts(report.Discovered),
                Applied = CloneHosts(report.Applied),
                Skipped = CloneHosts(report.Skipped),
                MissingPolicy = CloneHosts(report.MissingPolicy),
                Conflicts = CloneConflicts(report.Conflicts)
            };
        }

        private static PatchHostReportDto[] CloneHosts(PatchHostReportDto[] hosts)
        {
            if (hosts == null)
                return new PatchHostReportDto[0];

            PatchHostReportDto[] clones = new PatchHostReportDto[hosts.Length];
            for (int i = 0; i < hosts.Length; i++)
                clones[i] = CloneHost(hosts[i]);
            return clones;
        }

        private static PatchHostReportDto CloneHost(PatchHostReportDto host)
        {
            if (host == null)
                return null;

            return new PatchHostReportDto
            {
                PatchAssemblyName = host.PatchAssemblyName,
                PatchHostName = host.PatchHostName,
                SourceName = host.SourceName,
                Domain = host.Domain,
                OwningFeature = host.OwningFeature,
                TargetBehavior = host.TargetBehavior,
                FailureMode = host.FailureMode,
                RollbackStrategy = host.RollbackStrategy,
                StartupTiming = host.StartupTiming,
                TargetMethods = CloneStrings(host.TargetMethods),
                HasExplicitPolicy = host.HasExplicitPolicy,
                IsOptional = host.IsOptional,
                DeveloperOnly = host.DeveloperOnly,
                IsDangerous = host.IsDangerous
            };
        }

        private static PatchConflictReportDto[] CloneConflicts(PatchConflictReportDto[] conflicts)
        {
            if (conflicts == null)
                return new PatchConflictReportDto[0];

            PatchConflictReportDto[] clones = new PatchConflictReportDto[conflicts.Length];
            for (int i = 0; i < conflicts.Length; i++)
                clones[i] = CloneConflict(conflicts[i]);
            return clones;
        }

        private static PatchConflictReportDto CloneConflict(PatchConflictReportDto conflict)
        {
            if (conflict == null)
                return null;

            return new PatchConflictReportDto
            {
                TargetMethod = conflict.TargetMethod,
                Severity = conflict.Severity,
                Reason = conflict.Reason,
                PatchHosts = CloneHosts(conflict.PatchHosts)
            };
        }

        private static string[] CloneStrings(string[] values)
        {
            if (values == null)
                return new string[0];

            string[] clone = new string[values.Length];
            for (int i = 0; i < values.Length; i++)
                clone[i] = values[i];
            return clone;
        }

        private sealed class PatchAttemptDiagnostics
        {
            private readonly HashSet<Type> _failedTypes = new HashSet<Type>();
            private readonly HashSet<Type> _alreadyAppliedTypes = new HashSet<Type>();

            public void Record(object target, string result)
            {
                if (string.IsNullOrEmpty(result))
                    return;

                Type type = target as Type;
                if (type == null)
                    return;

                if (result.StartsWith("error:", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(result, "no methods patched", StringComparison.OrdinalIgnoreCase))
                {
                    _failedTypes.Add(type);
                }

                if (result.IndexOf("already applied", StringComparison.OrdinalIgnoreCase) >= 0)
                    _alreadyAppliedTypes.Add(type);
            }

            public bool IsFailure(PatchRecord record)
            {
                return record != null
                    && record.PatchType != null
                    && _failedTypes.Contains(record.PatchType);
            }

            public bool IsAlreadyApplied(PatchRecord record)
            {
                return record != null
                    && record.PatchType != null
                    && _alreadyAppliedTypes.Contains(record.PatchType);
            }
        }
    }
}
