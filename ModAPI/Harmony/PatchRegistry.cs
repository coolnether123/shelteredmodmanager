using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    /// <summary>
    /// Logical ownership domains for runtime Harmony patches.
    /// </summary>
    public enum PatchDomain
    {
        Unknown,
        Bootstrap,
        SaveFlow,
        UI,
        Input,
        Content,
        Diagnostics,
        Events,
        Interactions,
        Characters,
        World,
        Scenarios
    }

    /// <summary>
    /// Startup timing buckets used to defer Harmony patch hosts until their first safe trigger.
    /// </summary>
    public enum PatchStartupTiming
    {
        BootCritical,
        MenuCritical,
        SaveFlowCritical,
        GameplayDeferred,
        EditorDeferred,
        DebugDeferred
    }

    /// <summary>
    /// Declares governance metadata for a Harmony patch host.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class PatchPolicyAttribute : Attribute
    {
        /// <summary>
        /// Creates a patch policy for the specified domain and owning feature.
        /// </summary>
        public PatchPolicyAttribute(PatchDomain domain, string feature)
        {
            Domain = domain;
            Feature = feature ?? string.Empty;
            TargetBehavior = string.Empty;
            FailureMode = string.Empty;
            RollbackStrategy = string.Empty;
            ManagerToggleId = string.Empty;
            ManagerToggleLabel = string.Empty;
            ManagerToggleDescription = string.Empty;
            ManagerToggleDefault = true;
            ManagerToggleRequiresRestart = true;
            StartupTiming = PatchStartupTiming.BootCritical;
        }

        /// <summary>The domain that owns the patch.</summary>
        public PatchDomain Domain { get; private set; }
        /// <summary>The owning feature or subsystem name.</summary>
        public string Feature { get; private set; }
        /// <summary>The game/runtime behavior this patch changes.</summary>
        public string TargetBehavior { get; set; }
        /// <summary>The expected impact if the patch fails or is missing.</summary>
        public string FailureMode { get; set; }
        /// <summary>The recommended disable/remove strategy if the patch must be rolled back.</summary>
        public string RollbackStrategy { get; set; }
        /// <summary>True when the patch is optional and may be disabled without breaking core runtime.</summary>
        public bool IsOptional { get; set; }
        /// <summary>True when the patch is intended only for developer/debug scenarios.</summary>
        public bool DeveloperOnly { get; set; }
        /// <summary>Optional manager boolean option id that controls this patch host.</summary>
        public string ManagerToggleId { get; set; }
        /// <summary>Human-readable label for the manager boolean option.</summary>
        public string ManagerToggleLabel { get; set; }
        /// <summary>Human-readable detail text for the manager boolean option.</summary>
        public string ManagerToggleDescription { get; set; }
        /// <summary>Default manager boolean option value.</summary>
        public bool ManagerToggleDefault { get; set; }
        /// <summary>Whether changing the manager boolean option requires a game restart.</summary>
        public bool ManagerToggleRequiresRestart { get; set; }
        /// <summary>Sort order used by the desktop Manager for this option.</summary>
        public int ManagerToggleSortOrder { get; set; }
        /// <summary>Earliest safe runtime timing bucket for applying this patch host.</summary>
        public PatchStartupTiming StartupTiming { get; set; }
    }

    /// <summary>
    /// Options controlling registry-driven patch discovery and application.
    /// </summary>
    public sealed class PatchRegistryOptions
    {
        /// <summary>
        /// Creates default registry options.
        /// </summary>
        public PatchRegistryOptions()
        {
            PatchOptions = new HarmonyUtil.PatchOptions();
            DisabledDomains = new HashSet<PatchDomain>();
            IncludedStartupTimings = new HashSet<PatchStartupTiming>();
            IncludeOptionalPatches = true;
            SourceName = string.Empty;
            TriggerName = string.Empty;
        }

        /// <summary>Harmony safety/configuration options applied to discovered patches.</summary>
        public HarmonyUtil.PatchOptions PatchOptions { get; set; }
        /// <summary>Domains that should be skipped entirely during patch application.</summary>
        public HashSet<PatchDomain> DisabledDomains { get; private set; }
        /// <summary>When non-empty, only patch hosts in these startup timing buckets are considered.</summary>
        public HashSet<PatchStartupTiming> IncludedStartupTimings { get; private set; }
        /// <summary>Whether optional patches should be included.</summary>
        public bool IncludeOptionalPatches { get; set; }
        /// <summary>Human-readable source label used in patch registry logging.</summary>
        public string SourceName { get; set; }
        /// <summary>Human-readable runtime trigger that requested this patch scan.</summary>
        public string TriggerName { get; set; }
    }

    /// <summary>
    /// Describes one discovered patch host.
    /// </summary>
    public sealed class PatchRecord
    {
        /// <summary>The patch host type.</summary>
        public Type PatchType;
        /// <summary>The domain owning the patch.</summary>
        public PatchDomain Domain;
        /// <summary>The owning feature name.</summary>
        public string Feature;
        /// <summary>The runtime behavior targeted by the patch.</summary>
        public string TargetBehavior;
        /// <summary>The declared or inferred failure mode.</summary>
        public string FailureMode;
        /// <summary>The declared or inferred rollback strategy.</summary>
        public string RollbackStrategy;
        /// <summary>Whether the patch is optional.</summary>
        public bool IsOptional;
        /// <summary>Whether the patch is intended only for developer/debug scenarios.</summary>
        public bool DeveloperOnly;
        /// <summary>Whether the patch is marked dangerous.</summary>
        public bool IsDangerous;
        /// <summary>Whether governance metadata was explicitly declared.</summary>
        public bool HasExplicitPolicy;
        /// <summary>Earliest safe runtime timing bucket for applying this patch host.</summary>
        public PatchStartupTiming StartupTiming;
        /// <summary>The resolved Harmony target methods for the patch host.</summary>
        public List<MethodBase> Targets;
        /// <summary>Optional manager boolean option id that controls this patch host.</summary>
        public string ManagerToggleId;
        /// <summary>Human-readable label for the manager boolean option.</summary>
        public string ManagerToggleLabel;
        /// <summary>Human-readable detail text for the manager boolean option.</summary>
        public string ManagerToggleDescription;
        /// <summary>Default manager boolean option value.</summary>
        public bool ManagerToggleDefault;
        /// <summary>Whether changing the manager boolean option requires a game restart.</summary>
        public bool ManagerToggleRequiresRestart;
        /// <summary>Sort order used by the desktop Manager for this option.</summary>
        public int ManagerToggleSortOrder;
    }

    /// <summary>
    /// Result of applying registry-driven patch discovery to an assembly.
    /// </summary>
    public sealed class PatchApplyReport
    {
        /// <summary>All discovered Harmony patch hosts.</summary>
        public readonly List<PatchRecord> Discovered = new List<PatchRecord>();
        /// <summary>Patch hosts that were successfully applied.</summary>
        public readonly List<PatchRecord> Applied = new List<PatchRecord>();
        /// <summary>Patch hosts that were skipped or produced no patch operations.</summary>
        public readonly List<PatchRecord> Skipped = new List<PatchRecord>();
        /// <summary>Patch hosts missing explicit governance metadata.</summary>
        public readonly List<PatchRecord> MissingPolicy = new List<PatchRecord>();
        /// <summary>Non-blocking duplicate target diagnostics detected while building the report.</summary>
        public readonly List<PatchConflictReportDto> Conflicts = new List<PatchConflictReportDto>();
        /// <summary>Stable support-bundle-oriented snapshot of this application attempt.</summary>
        public PatchReportDto DiagnosticSnapshot { get; internal set; }
    }

    /// <summary>
    /// Diagnostic classification for multiple patch hosts resolved to one target method.
    /// </summary>
    public enum PatchConflictSeverity
    {
        Informational,
        Warning
    }

    /// <summary>
    /// Serialization-friendly description of one governed patch host.
    /// </summary>
    public sealed class PatchHostReportDto
    {
        /// <summary>Assembly containing the patch host.</summary>
        public string PatchAssemblyName { get; set; }
        /// <summary>Fully qualified patch host type name.</summary>
        public string PatchHostName { get; set; }
        /// <summary>Registry source name used when the host was discovered.</summary>
        public string SourceName { get; set; }
        /// <summary>Governance domain for the patch host.</summary>
        public PatchDomain Domain { get; set; }
        /// <summary>Feature that owns the patch host.</summary>
        public string OwningFeature { get; set; }
        /// <summary>Declared or inferred target behavior description.</summary>
        public string TargetBehavior { get; set; }
        /// <summary>Declared or inferred failure behavior.</summary>
        public string FailureMode { get; set; }
        /// <summary>Declared or inferred rollback guidance.</summary>
        public string RollbackStrategy { get; set; }
        /// <summary>Earliest timing group in which the host may be applied.</summary>
        public PatchStartupTiming StartupTiming { get; set; }
        /// <summary>Resolved stable target method signatures, when resolution ran for this timing group.</summary>
        public string[] TargetMethods { get; set; }
        /// <summary>Whether the host explicitly declares <see cref="PatchPolicyAttribute"/> metadata.</summary>
        public bool HasExplicitPolicy { get; set; }
        /// <summary>Whether the host declares itself optional.</summary>
        public bool IsOptional { get; set; }
        /// <summary>Whether the host is developer/debug-only.</summary>
        public bool DeveloperOnly { get; set; }
        /// <summary>Whether the host is marked dangerous.</summary>
        public bool IsDangerous { get; set; }
    }

    /// <summary>
    /// Serialization-friendly description of multiple patch hosts sharing a target.
    /// This is a diagnostic only and does not suppress patch application.
    /// </summary>
    public sealed class PatchConflictReportDto
    {
        /// <summary>Stable target method signature shared by the patch hosts.</summary>
        public string TargetMethod { get; set; }
        /// <summary>Diagnostic severity based on declared ownership metadata.</summary>
        public PatchConflictSeverity Severity { get; set; }
        /// <summary>Reason for the severity classification.</summary>
        public string Reason { get; set; }
        /// <summary>Patch hosts currently known to target the method.</summary>
        public PatchHostReportDto[] PatchHosts { get; set; }
    }

    /// <summary>
    /// Stable report snapshot retained for diagnostics and support-bundle collection.
    /// </summary>
    public sealed class PatchReportDto
    {
        /// <summary>UTC timestamp at which this report was retained.</summary>
        public DateTime CapturedUtc { get; set; }
        /// <summary>Assembly scanned for patch hosts.</summary>
        public string AssemblyName { get; set; }
        /// <summary>Source name supplied to the registry.</summary>
        public string SourceName { get; set; }
        /// <summary>Deferred/runtime trigger that initiated the scan, when known.</summary>
        public string TriggerName { get; set; }
        /// <summary>All patch hosts discovered during this scan.</summary>
        public PatchHostReportDto[] Discovered { get; set; }
        /// <summary>Patch hosts successfully applied during this scan.</summary>
        public PatchHostReportDto[] Applied { get; set; }
        /// <summary>Patch hosts skipped or producing no patch operation during this scan.</summary>
        public PatchHostReportDto[] Skipped { get; set; }
        /// <summary>Patch hosts without explicit policy metadata.</summary>
        public PatchHostReportDto[] MissingPolicy { get; set; }
        /// <summary>Non-blocking duplicate target diagnostics detected by this scan.</summary>
        public PatchConflictReportDto[] Conflicts { get; set; }
    }

    /// <summary>
    /// Central registry for patch discovery, governance, and activation.
    /// </summary>
    public static class PatchRegistry
    {
        private const int MaxRetainedReports = 64;
        private static readonly object DiagnosticsSync = new object();
        private static readonly List<PatchReportDto> RetainedReports = new List<PatchReportDto>();
        private static readonly Dictionary<string, List<PatchHostReportDto>> KnownTargetHosts =
            new Dictionary<string, List<PatchHostReportDto>>(StringComparer.Ordinal);
        private static readonly HashSet<string> LoggedInformationalConflicts =
            new HashSet<string>(StringComparer.Ordinal);

        /// <summary>
        /// Returns stable diagnostic snapshots retained from registry application attempts in this process.
        /// The oldest snapshots are removed after the bounded history reaches capacity.
        /// </summary>
        public static PatchReportDto[] GetReportHistory()
        {
            lock (DiagnosticsSync)
            {
                var history = new PatchReportDto[RetainedReports.Count];
                for (int i = 0; i < RetainedReports.Count; i++)
                    history[i] = CloneReport(RetainedReports[i]);
                return history;
            }
        }

        /// <summary>
        /// Returns the most recently retained diagnostic report, or null before patch discovery runs.
        /// </summary>
        public static PatchReportDto GetLatestReport()
        {
            lock (DiagnosticsSync)
            {
                if (RetainedReports.Count == 0)
                    return null;

                return CloneReport(RetainedReports[RetainedReports.Count - 1]);
            }
        }

        /// <summary>
        /// Discovers and applies Harmony patch hosts from the provided assembly.
        /// </summary>
        public static PatchApplyReport ApplyAssembly(HarmonyLib.Harmony harmony, Assembly assembly, PatchRegistryOptions options)
        {
            var report = new PatchApplyReport();
            if (harmony == null || assembly == null) return report;
            if (options == null) options = new PatchRegistryOptions();

            foreach (var type in SafeTypes(assembly))
            {
                if (type == null || !HarmonyUtil.HasHarmonyPatchAttributes(type)) continue;

                PatchStartupTiming startupTiming = ResolveStartupTiming(type);
                bool includedByTiming = IsStartupTimingIncluded(startupTiming, options);
                var record = CreateRecord(type, includedByTiming);
                report.Discovered.Add(record);
                DetectTargetConflicts(record, options, report.Conflicts);

                if (!record.HasExplicitPolicy)
                {
                    report.MissingPolicy.Add(record);
                }

                if (!includedByTiming)
                {
                    report.Skipped.Add(record);
                    LogSkip(record, options);
                    continue;
                }

                if (!ShouldApply(record, options))
                {
                    report.Skipped.Add(record);
                    LogSkip(record, options);
                    continue;
                }

                var patched = HarmonyUtil.PatchKnownType(harmony, type, options.PatchOptions, record.Targets);
                if (patched != null && patched.Count > 0)
                {
                    report.Applied.Add(record);
                }
                else
                {
                    report.Skipped.Add(record);
                }
            }

            LogSummary(report, options);
            RetainReport(report, assembly, options);
            return report;
        }

        /// <summary>
        /// Applies a manually registered patch module through the same governance checks as discovered patches.
        /// </summary>
        public static bool ApplyManualModule(HarmonyLib.Harmony harmony, Type moduleType, Action applyAction, PatchRegistryOptions options)
        {
            if (harmony == null || moduleType == null || applyAction == null) return false;
            if (options == null) options = new PatchRegistryOptions();

            var report = new PatchApplyReport();
            var record = CreateRecord(moduleType);
            report.Discovered.Add(record);
            DetectTargetConflicts(record, options, report.Conflicts);
            if (!record.HasExplicitPolicy)
                report.MissingPolicy.Add(record);

            if (!ShouldApply(record, options))
            {
                report.Skipped.Add(record);
                LogSkip(record, options);
                RetainReport(report, moduleType.Assembly, options);
                return false;
            }

            try
            {
                applyAction();
                report.Applied.Add(record);
                LogManualApply(record, options);
                RetainReport(report, moduleType.Assembly, options);
                return true;
            }
            catch (Exception ex)
            {
                report.Skipped.Add(record);
                MMLog.WriteWarning("[PatchRegistry] Manual patch module failed for "
                    + DescribeType(moduleType) + ": " + ex.Message);
                RetainReport(report, moduleType.Assembly, options);
                return false;
            }
        }

        /// <summary>
        /// Creates registry options from manager/runtime configuration.
        /// </summary>
        public static PatchRegistryOptions CreateManagerOptions(HarmonyUtil.PatchOptions patchOptions, string sourceName, Func<string, string> readString)
        {
            var options = new PatchRegistryOptions();
            options.PatchOptions = patchOptions ?? new HarmonyUtil.PatchOptions();
            options.SourceName = sourceName ?? string.Empty;
            options.IncludeOptionalPatches = readString == null || !string.Equals(readString("EnableOptionalPatches"), "false", StringComparison.OrdinalIgnoreCase);

            string disabledDomains = readString != null ? readString("DisabledPatchDomains") : null;
            ApplyDisabledDomains(options.DisabledDomains, disabledDomains);
            return options;
        }

        /// <summary>
        /// Creates an options copy that only applies the specified startup timing buckets.
        /// </summary>
        public static PatchRegistryOptions CreateTimingOptions(PatchRegistryOptions source, params PatchStartupTiming[] timings)
        {
            var options = new PatchRegistryOptions();
            if (source != null)
            {
                options.PatchOptions = source.PatchOptions;
                options.IncludeOptionalPatches = source.IncludeOptionalPatches;
                options.SourceName = source.SourceName;
                options.TriggerName = source.TriggerName;
                foreach (PatchDomain domain in source.DisabledDomains)
                    options.DisabledDomains.Add(domain);
            }

            if (timings != null)
            {
                for (int i = 0; i < timings.Length; i++)
                    options.IncludedStartupTimings.Add(timings[i]);
            }

            return options;
        }

        /// <summary>
        /// Parses and applies disabled patch domains from a configuration string.
        /// </summary>
        public static void ApplyDisabledDomains(HashSet<PatchDomain> domains, string raw)
        {
            if (domains == null || string.IsNullOrEmpty(raw)) return;

            string[] parts = raw.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                PatchDomain parsed;
                if (TryParseDomain(parts[i].Trim(), out parsed))
                {
                    domains.Add(parsed);
                }
            }
        }

        private static bool ShouldApply(PatchRecord record, PatchRegistryOptions options)
        {
            if (record == null) return false;
            if (!ShouldApplyManagerToggle(record, options))
                return false;

            if (options != null && options.DisabledDomains != null && options.DisabledDomains.Contains(record.Domain))
                return false;

            if (options != null
                && options.IncludedStartupTimings != null
                && options.IncludedStartupTimings.Count > 0
                && !options.IncludedStartupTimings.Contains(record.StartupTiming))
                return false;

            if (record.IsOptional && options != null && !options.IncludeOptionalPatches)
                return false;

            if (record.DeveloperOnly && (options == null || options.PatchOptions == null || !options.PatchOptions.AllowDebugPatches))
                return false;

            return true;
        }

        private static bool ShouldApplyManagerToggle(PatchRecord record, PatchRegistryOptions options)
        {
            if (record == null || string.IsNullOrEmpty(record.ManagerToggleId))
                return true;

            string sourceName = options != null ? options.SourceName : string.Empty;
            ManagerBooleanOptions.RegisterBooleanOption(new ManagerBooleanOptionDefinition
            {
                Id = record.ManagerToggleId,
                Owner = !string.IsNullOrEmpty(sourceName) ? sourceName : "runtime",
                Label = !string.IsNullOrEmpty(record.ManagerToggleLabel) ? record.ManagerToggleLabel : record.Feature,
                Description = record.ManagerToggleDescription ?? string.Empty,
                DefaultValue = record.ManagerToggleDefault,
                RequiresRestart = record.ManagerToggleRequiresRestart,
                SortOrder = record.ManagerToggleSortOrder
            });

            return ManagerBooleanOptions.GetBool(record.ManagerToggleId, record.ManagerToggleDefault);
        }

        private static PatchRecord CreateRecord(Type type)
        {
            return CreateRecord(type, true);
        }

        private static PatchRecord CreateRecord(Type type, bool resolveTargets)
        {
            var policy = FindPolicy(type);
            var targets = new List<MethodBase>();
            if (resolveTargets)
            {
                var discoveredTargets = HarmonyUtil.GetPatchTargets(type);
                if (discoveredTargets != null)
                    targets.AddRange(discoveredTargets);
            }

            var record = new PatchRecord();
            record.PatchType = type;
            record.Domain = policy != null ? policy.Domain : InferDomain(type);
            record.Feature = policy != null && !string.IsNullOrEmpty(policy.Feature) ? policy.Feature : InferFeature(type);
            record.TargetBehavior = policy != null && !string.IsNullOrEmpty(policy.TargetBehavior)
                ? policy.TargetBehavior
                : BuildTargetBehavior(targets);
            record.FailureMode = policy != null && !string.IsNullOrEmpty(policy.FailureMode)
                ? policy.FailureMode
                : "Runtime behavior falls back to vanilla or feature-specific behavior may be incomplete.";
            record.RollbackStrategy = policy != null && !string.IsNullOrEmpty(policy.RollbackStrategy)
                ? policy.RollbackStrategy
                : "Disable the owning patch domain or remove the patch class from registry-driven bootstrap.";
            record.IsOptional = policy != null && policy.IsOptional;
            record.DeveloperOnly = (policy != null && policy.DeveloperOnly) || HarmonyUtil.HasDebugAttribute(type);
            record.IsDangerous = HarmonyUtil.HasDangerousAttribute(type);
            record.HasExplicitPolicy = policy != null;
            record.StartupTiming = policy != null ? policy.StartupTiming : PatchStartupTiming.BootCritical;
            record.Targets = targets;
            record.ManagerToggleId = policy != null ? policy.ManagerToggleId : null;
            record.ManagerToggleLabel = policy != null ? policy.ManagerToggleLabel : null;
            record.ManagerToggleDescription = policy != null ? policy.ManagerToggleDescription : null;
            record.ManagerToggleDefault = policy == null || policy.ManagerToggleDefault;
            record.ManagerToggleRequiresRestart = policy == null || policy.ManagerToggleRequiresRestart;
            record.ManagerToggleSortOrder = policy != null ? policy.ManagerToggleSortOrder : 0;
            return record;
        }

        private static PatchStartupTiming ResolveStartupTiming(Type type)
        {
            var policy = FindPolicy(type);
            return policy != null ? policy.StartupTiming : PatchStartupTiming.BootCritical;
        }

        private static bool IsStartupTimingIncluded(PatchStartupTiming timing, PatchRegistryOptions options)
        {
            if (options == null || options.IncludedStartupTimings == null || options.IncludedStartupTimings.Count == 0)
                return true;

            return options.IncludedStartupTimings.Contains(timing);
        }

        private static PatchPolicyAttribute FindPolicy(Type type)
        {
            for (Type cursor = type; cursor != null; cursor = cursor.DeclaringType)
            {
                object[] attrs = cursor.GetCustomAttributes(typeof(PatchPolicyAttribute), false);
                if (attrs != null && attrs.Length > 0)
                    return attrs[0] as PatchPolicyAttribute;
            }
            return null;
        }

        private static IEnumerable<Type> SafeTypes(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null); }
            catch { return Enumerable.Empty<Type>(); }
        }

        private static PatchDomain InferDomain(Type type)
        {
            string fullName = type != null ? (type.FullName ?? string.Empty) : string.Empty;
            string lower = fullName.ToLowerInvariant();

            if (lower.Contains("custom saves") || lower.Contains(".save") || lower.Contains("platformsave") || lower.Contains("slotselection"))
                return PatchDomain.SaveFlow;
            if (lower.Contains(".ui") || lower.Contains("mainmenu") || lower.Contains("panel"))
                return PatchDomain.UI;
            if (lower.Contains(".input"))
                return PatchDomain.Input;
            if (lower.Contains(".content") || lower.Contains("inventoryintegration") || lower.Contains("localization"))
                return PatchDomain.Content;
            if (lower.Contains(".debug") || lower.Contains("diagnostic"))
                return PatchDomain.Diagnostics;
            if (lower.Contains(".events"))
                return PatchDomain.Events;
            if (lower.Contains(".interactions"))
                return PatchDomain.Interactions;
            if (lower.Contains(".characters"))
                return PatchDomain.Characters;
            if (lower.Contains(".harmony"))
                return PatchDomain.Bootstrap;

            return PatchDomain.Unknown;
        }

        private static string InferFeature(Type type)
        {
            if (type == null) return "Unknown";

            Type root = type;
            while (root.DeclaringType != null)
                root = root.DeclaringType;

            return root.Name;
        }

        private static string BuildTargetBehavior(List<MethodBase> targets)
        {
            if (targets == null || targets.Count == 0)
                return "Multiple or dynamically resolved patch targets.";

            var parts = new List<string>();
            for (int i = 0; i < targets.Count && i < 3; i++)
            {
                MethodBase target = targets[i];
                if (target == null) continue;
                string typeName = target.DeclaringType != null ? target.DeclaringType.Name : "<dynamic>";
                parts.Add(typeName + "." + target.Name);
            }

            if (targets.Count > 3)
                parts.Add("...");

            return string.Join(", ", parts.ToArray());
        }

        private static void DetectTargetConflicts(
            PatchRecord record,
            PatchRegistryOptions options,
            IList<PatchConflictReportDto> currentConflicts)
        {
            if (record == null || record.Targets == null || record.Targets.Count == 0)
                return;

            PatchHostReportDto host = CreateHostReport(record, GetSourceName(options));
            for (int i = 0; i < record.Targets.Count; i++)
            {
                MethodBase target = record.Targets[i];
                if (target == null)
                    continue;

                string targetName = DescribeTarget(target);
                PatchConflictReportDto conflict = null;
                bool logInformational = false;

                lock (DiagnosticsSync)
                {
                    List<PatchHostReportDto> hosts;
                    if (!KnownTargetHosts.TryGetValue(targetName, out hosts))
                    {
                        hosts = new List<PatchHostReportDto>();
                        KnownTargetHosts[targetName] = hosts;
                    }

                    if (!ContainsHost(hosts, host))
                        hosts.Add(CloneHost(host));

                    if (hosts.Count > 1)
                    {
                        conflict = CreateConflict(targetName, hosts);
                        AddOrReplaceConflict(currentConflicts, conflict);
                        if (conflict.Severity == PatchConflictSeverity.Informational)
                        {
                            string informationalKey = targetName + "|" + conflict.Severity;
                            logInformational = LoggedInformationalConflicts.Add(informationalKey);
                        }
                    }
                }

                if (conflict == null)
                    continue;

                string message = "[PatchRegistry] " + conflict.Severity
                    + " duplicate target " + conflict.TargetMethod + ": " + conflict.Reason;
                if (conflict.Severity == PatchConflictSeverity.Warning)
                {
                    MMLog.WarnOnce("PatchRegistry.Conflict." + conflict.Severity + "." + targetName, message);
                }
                else if (logInformational)
                {
                    MMLog.WriteInfo(message);
                }
            }
        }

        private static bool ContainsHost(IList<PatchHostReportDto> hosts, PatchHostReportDto candidate)
        {
            if (hosts == null || candidate == null)
                return false;

            for (int i = 0; i < hosts.Count; i++)
            {
                PatchHostReportDto host = hosts[i];
                if (host != null
                    && string.Equals(host.PatchAssemblyName, candidate.PatchAssemblyName, StringComparison.Ordinal)
                    && string.Equals(host.PatchHostName, candidate.PatchHostName, StringComparison.Ordinal)
                    && string.Equals(host.SourceName, candidate.SourceName, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static PatchConflictReportDto CreateConflict(string targetName, IList<PatchHostReportDto> hosts)
        {
            var conflict = new PatchConflictReportDto();
            conflict.TargetMethod = targetName ?? string.Empty;
            conflict.PatchHosts = CloneHosts(hosts);

            bool missingPolicy = conflict.PatchHosts.Any(host => host == null || !host.HasExplicitPolicy);
            bool unknownDomain = conflict.PatchHosts.Any(host => host == null || host.Domain == PatchDomain.Unknown);
            bool sameDomain = conflict.PatchHosts.Select(host => host.Domain).Distinct().Count() == 1;
            bool sameFeature = conflict.PatchHosts
                .Select(host => host.OwningFeature ?? string.Empty)
                .Distinct(StringComparer.Ordinal)
                .Count() == 1;
            bool allOptional = conflict.PatchHosts.All(host => host != null && host.IsOptional);

            if (missingPolicy)
            {
                conflict.Severity = PatchConflictSeverity.Warning;
                conflict.Reason = "At least one target owner is missing explicit patch policy metadata.";
            }
            else if (unknownDomain)
            {
                conflict.Severity = PatchConflictSeverity.Warning;
                conflict.Reason = "At least one target owner has an unknown governance domain.";
            }
            else if (!sameDomain)
            {
                conflict.Severity = PatchConflictSeverity.Warning;
                conflict.Reason = "Patch hosts from different governance domains share this target.";
            }
            else if (!sameFeature && !allOptional)
            {
                conflict.Severity = PatchConflictSeverity.Warning;
                conflict.Reason = "Required patch hosts owned by different features share this target.";
            }
            else if (!sameFeature)
            {
                conflict.Severity = PatchConflictSeverity.Informational;
                conflict.Reason = "Optional patch hosts owned by different features share this target.";
            }
            else
            {
                conflict.Severity = PatchConflictSeverity.Informational;
                conflict.Reason = "Multiple declared patch hosts belong to the same feature and domain.";
            }

            return conflict;
        }

        private static void AddOrReplaceConflict(
            IList<PatchConflictReportDto> conflicts,
            PatchConflictReportDto conflict)
        {
            if (conflicts == null || conflict == null)
                return;

            for (int i = 0; i < conflicts.Count; i++)
            {
                if (string.Equals(conflicts[i].TargetMethod, conflict.TargetMethod, StringComparison.Ordinal))
                {
                    conflicts[i] = CloneConflict(conflict);
                    return;
                }
            }

            conflicts.Add(CloneConflict(conflict));
        }

        private static void RetainReport(PatchApplyReport report, Assembly assembly, PatchRegistryOptions options)
        {
            if (report == null)
                return;

            string sourceName = GetSourceName(options);
            var snapshot = new PatchReportDto
            {
                CapturedUtc = DateTime.UtcNow,
                AssemblyName = SafeAssemblyName(assembly),
                SourceName = sourceName,
                TriggerName = options != null ? (options.TriggerName ?? string.Empty) : string.Empty,
                Discovered = CreateHostReports(report.Discovered, sourceName),
                Applied = CreateHostReports(report.Applied, sourceName),
                Skipped = CreateHostReports(report.Skipped, sourceName),
                MissingPolicy = CreateHostReports(report.MissingPolicy, sourceName),
                Conflicts = CloneConflicts(report.Conflicts)
            };

            report.DiagnosticSnapshot = CloneReport(snapshot);
            lock (DiagnosticsSync)
            {
                RetainedReports.Add(CloneReport(snapshot));
                while (RetainedReports.Count > MaxRetainedReports)
                    RetainedReports.RemoveAt(0);
            }
        }

        private static PatchHostReportDto[] CreateHostReports(IEnumerable<PatchRecord> records, string sourceName)
        {
            if (records == null)
                return new PatchHostReportDto[0];

            return records.Select(record => CreateHostReport(record, sourceName)).ToArray();
        }

        private static PatchHostReportDto CreateHostReport(PatchRecord record, string sourceName)
        {
            if (record == null)
                return null;

            return new PatchHostReportDto
            {
                PatchAssemblyName = record.PatchType != null ? SafeAssemblyName(record.PatchType.Assembly) : string.Empty,
                PatchHostName = DescribeType(record.PatchType),
                SourceName = sourceName ?? string.Empty,
                Domain = record.Domain,
                OwningFeature = record.Feature ?? string.Empty,
                TargetBehavior = record.TargetBehavior ?? string.Empty,
                FailureMode = record.FailureMode ?? string.Empty,
                RollbackStrategy = record.RollbackStrategy ?? string.Empty,
                StartupTiming = record.StartupTiming,
                TargetMethods = record.Targets != null
                    ? record.Targets.Where(target => target != null).Select(DescribeTarget).ToArray()
                    : new string[0],
                HasExplicitPolicy = record.HasExplicitPolicy,
                IsOptional = record.IsOptional,
                DeveloperOnly = record.DeveloperOnly,
                IsDangerous = record.IsDangerous
            };
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

        private static PatchHostReportDto[] CloneHosts(IEnumerable<PatchHostReportDto> hosts)
        {
            if (hosts == null)
                return new PatchHostReportDto[0];

            return hosts.Select(CloneHost).ToArray();
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
                TargetMethods = host.TargetMethods != null ? host.TargetMethods.ToArray() : new string[0],
                HasExplicitPolicy = host.HasExplicitPolicy,
                IsOptional = host.IsOptional,
                DeveloperOnly = host.DeveloperOnly,
                IsDangerous = host.IsDangerous
            };
        }

        private static PatchConflictReportDto[] CloneConflicts(IEnumerable<PatchConflictReportDto> conflicts)
        {
            if (conflicts == null)
                return new PatchConflictReportDto[0];

            return conflicts.Select(CloneConflict).ToArray();
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

        private static string GetSourceName(PatchRegistryOptions options)
        {
            return options != null && !string.IsNullOrEmpty(options.SourceName)
                ? options.SourceName
                : "runtime";
        }

        private static string DescribeTarget(MethodBase target)
        {
            if (target == null)
                return "<unknown>";

            try
            {
                string declaringType = target.DeclaringType != null
                    ? (target.DeclaringType.FullName ?? target.DeclaringType.Name)
                    : "<dynamic>";
                string[] parameterNames = target.GetParameters()
                    .Select(parameter => parameter.ParameterType != null
                        ? (parameter.ParameterType.FullName ?? parameter.ParameterType.Name)
                        : "<unknown>")
                    .ToArray();
                return declaringType + "." + target.Name + "(" + string.Join(", ", parameterNames) + ")";
            }
            catch
            {
                return target.Name ?? "<unknown>";
            }
        }

        private static string SafeAssemblyName(Assembly assembly)
        {
            try { return assembly != null ? assembly.GetName().Name : "<null>"; }
            catch { return "<unknown>"; }
        }

        private static void LogSummary(PatchApplyReport report, PatchRegistryOptions options)
        {
            string source = GetSourceName(options);
            MMLog.WriteInfo(source
                + " discovered=" + report.Discovered.Count
                + ", applied=" + report.Applied.Count
                + ", skipped=" + report.Skipped.Count
                + ", missingPolicy=" + report.MissingPolicy.Count
                + ", conflicts=" + report.Conflicts.Count + ".");

            if (report.MissingPolicy.Count > 0)
            {
                int max = Math.Min(8, report.MissingPolicy.Count);
                for (int i = 0; i < max; i++)
                {
                    var record = report.MissingPolicy[i];
                    MMLog.WriteInfo("Missing policy: " + DescribeRecord(record));
                }
            }
        }

        private static void LogSkip(PatchRecord record, PatchRegistryOptions options)
        {
            if (record == null) return;
            MMLog.WriteDebug("skipped " + DescribeRecord(record)
                + " source=" + (options != null ? options.SourceName : string.Empty));
        }

        private static void LogManualApply(PatchRecord record, PatchRegistryOptions options)
        {
            if (record == null) return;
            MMLog.WriteInfo("manual apply " + DescribeRecord(record)
                + " source=" + (options != null ? options.SourceName : string.Empty));
        }

        private static string DescribeRecord(PatchRecord record)
        {
            if (record == null) return "<null>";
            return DescribeType(record.PatchType)
                + " domain=" + record.Domain
                + " timing=" + record.StartupTiming
                + " feature=" + (record.Feature ?? string.Empty)
                + " target=" + (record.TargetBehavior ?? string.Empty);
        }

        private static string DescribeType(Type type)
        {
            return type != null ? (type.FullName ?? type.Name) : "<null>";
        }

        private static bool TryParseDomain(string raw, out PatchDomain domain)
        {
            domain = PatchDomain.Unknown;
            if (string.IsNullOrEmpty(raw)) return false;

            try
            {
                domain = (PatchDomain)Enum.Parse(typeof(PatchDomain), raw, true);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
