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
        World
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
            IncludeOptionalPatches = true;
            SourceName = string.Empty;
        }

        /// <summary>Harmony safety/configuration options applied to discovered patches.</summary>
        public HarmonyUtil.PatchOptions PatchOptions { get; set; }
        /// <summary>Domains that should be skipped entirely during patch application.</summary>
        public HashSet<PatchDomain> DisabledDomains { get; private set; }
        /// <summary>Whether optional patches should be included.</summary>
        public bool IncludeOptionalPatches { get; set; }
        /// <summary>Human-readable source label used in patch registry logging.</summary>
        public string SourceName { get; set; }
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
        /// <summary>The resolved Harmony target methods for the patch host.</summary>
        public List<MethodBase> Targets;
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
    }

    /// <summary>
    /// Central registry for patch discovery, governance, and activation.
    /// </summary>
    public static class PatchRegistry
    {
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

                var record = CreateRecord(type);
                report.Discovered.Add(record);

                if (!record.HasExplicitPolicy)
                {
                    report.MissingPolicy.Add(record);
                }

                if (!ShouldApply(record, options))
                {
                    report.Skipped.Add(record);
                    LogSkip(record, options);
                    continue;
                }

                var patched = HarmonyUtil.PatchType(harmony, type, options.PatchOptions);
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
            return report;
        }

        /// <summary>
        /// Applies a manually registered patch module through the same governance checks as discovered patches.
        /// </summary>
        public static bool ApplyManualModule(HarmonyLib.Harmony harmony, Type moduleType, Action applyAction, PatchRegistryOptions options)
        {
            if (harmony == null || moduleType == null || applyAction == null) return false;
            if (options == null) options = new PatchRegistryOptions();

            var record = CreateRecord(moduleType);
            if (!ShouldApply(record, options))
            {
                LogSkip(record, options);
                return false;
            }

            try
            {
                applyAction();
                LogManualApply(record, options);
                return true;
            }
            catch (Exception ex)
            {
                MMLog.WriteWarning("[PatchRegistry] Manual patch module failed for "
                    + DescribeType(moduleType) + ": " + ex.Message);
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
            if (options != null && options.DisabledDomains != null && options.DisabledDomains.Contains(record.Domain))
                return false;

            if (record.IsOptional && options != null && !options.IncludeOptionalPatches)
                return false;

            if (record.DeveloperOnly && (options == null || options.PatchOptions == null || !options.PatchOptions.AllowDebugPatches))
                return false;

            return true;
        }

        private static PatchRecord CreateRecord(Type type)
        {
            var policy = FindPolicy(type);
            var targets = new List<MethodBase>();
            var discoveredTargets = HarmonyUtil.GetPatchTargets(type);
            if (discoveredTargets != null)
                targets.AddRange(discoveredTargets);

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
            record.Targets = targets;
            return record;
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

        private static void LogSummary(PatchApplyReport report, PatchRegistryOptions options)
        {
            string source = !string.IsNullOrEmpty(options.SourceName) ? options.SourceName : "runtime";
            MMLog.WriteInfo("[PatchRegistry] " + source
                + " discovered=" + report.Discovered.Count
                + ", applied=" + report.Applied.Count
                + ", skipped=" + report.Skipped.Count
                + ", missingPolicy=" + report.MissingPolicy.Count + ".");

            if (report.MissingPolicy.Count > 0)
            {
                int max = Math.Min(8, report.MissingPolicy.Count);
                for (int i = 0; i < max; i++)
                {
                    var record = report.MissingPolicy[i];
                    MMLog.WriteDebug("[PatchRegistry] Missing policy: " + DescribeRecord(record));
                }
            }
        }

        private static void LogSkip(PatchRecord record, PatchRegistryOptions options)
        {
            if (record == null) return;
            MMLog.WriteInfo("[PatchRegistry] skipped " + DescribeRecord(record)
                + " source=" + (options != null ? options.SourceName : string.Empty));
        }

        private static void LogManualApply(PatchRecord record, PatchRegistryOptions options)
        {
            if (record == null) return;
            MMLog.WriteInfo("[PatchRegistry] manual apply " + DescribeRecord(record)
                + " source=" + (options != null ? options.SourceName : string.Empty));
        }

        private static string DescribeRecord(PatchRecord record)
        {
            if (record == null) return "<null>";
            return DescribeType(record.PatchType)
                + " domain=" + record.Domain
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
