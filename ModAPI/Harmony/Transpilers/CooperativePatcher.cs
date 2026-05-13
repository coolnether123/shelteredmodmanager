using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using ModAPI.Core;

namespace ModAPI.Harmony
{
    public enum PatchPriority
    {
        First = 0,
        VeryHigh = 100,
        High = 200,
        Normal = 300,
        Low = 400,
        VeryLow = 500,
        Last = 600
    }

    /// <summary>
    /// Orchestrates multiple transpilers on the same method to ensure compatibility.
    /// Replaces the "wild west" of conflicting Harmony patches with a managed pipeline.
    /// </summary>
    public static class CooperativePatcher
    {
        private class PatcherRegistration
        {
            public string AnchorId;
            public PatchPriority Priority;
            public Func<FluentTranspiler, FluentTranspiler> PatchLogic;
            public string OwnerMod;
            public string[] DependsOn;  // AnchorIds that must run first
            public string[] ConflictsWith;  // AnchorIds that cannot coexist
        }

        private static readonly Dictionary<MethodBase, List<PatcherRegistration>> _registrations = 
            new Dictionary<MethodBase, List<PatcherRegistration>>();
        private static readonly object _lock = new object();
        private static readonly object _quarantineLock = new object();
        private static readonly HashSet<string> _quarantinedOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a cooperative transpiler.
        /// NOTE: This does not apply the patch immediately. You must call Apply() or ensure ModAPI's master patcher is running.
        /// </summary>
        /// <example>
        /// <code>
        /// CooperativePatcher.RegisterTranspiler(
        ///     target,
        ///     "MyMod.ReplaceOriginalCall",
        ///     PatchPriority.High,
        ///     t =>
        ///     {
        ///         t.ForCall(typeof(SomeType), "Original")
        ///          .ReplaceWith(typeof(MyHooks), "Replacement");
        ///         return t;
        ///     },
        ///     conflictsWith: new[] { "Legacy.RawIlPatch" });
        /// </code>
        /// </example>
        public static void RegisterTranspiler(
            MethodBase target,
            string anchorId,
            PatchPriority priority,
            Func<FluentTranspiler, FluentTranspiler> patchLogic,
            string[] dependsOn = null,
            string[] conflictsWith = null)
        {
            lock (_lock)
            {
                if (!_registrations.ContainsKey(target))
                    _registrations[target] = new List<PatcherRegistration>();

                var registration = new PatcherRegistration
                {
                    AnchorId = anchorId,
                    Priority = priority,
                    PatchLogic = patchLogic,
                    OwnerMod = Assembly.GetCallingAssembly().GetName().Name,
                    DependsOn = dependsOn ?? new string[0],
                    ConflictsWith = conflictsWith ?? new string[0]
                };

                // Deduplication: Remove existing patch with same AnchorId from same mod
                _registrations[target].RemoveAll(r => r.AnchorId == anchorId && r.OwnerMod == registration.OwnerMod);
                
                _registrations[target].Add(registration);
                
                MMLog.WriteDebug($"[CooperativePatcher] Registered patch for {target.Name} from {registration.OwnerMod} (Priority: {priority}, Anchor: {anchorId})");
            }
        }

        public static bool UnregisterTranspiler(MethodBase target, string anchorId, string ownerMod = null)
        {
            lock (_lock)
            {
                if (!_registrations.ContainsKey(target)) return false;

                string mod = ownerMod ?? Assembly.GetCallingAssembly().GetName().Name;
                return _registrations[target].RemoveAll(r => r.AnchorId == anchorId && r.OwnerMod == mod) > 0;
            }
        }

        public static void UnregisterAll(string ownerMod = null)
        {
            string mod = ownerMod ?? Assembly.GetCallingAssembly().GetName().Name;
            lock (_lock)
            {
                foreach (var list in _registrations.Values)
                {
                    list.RemoveAll(r => r.OwnerMod == mod);
                }
            }
            lock (_quarantineLock) { _quarantinedOwners.Remove(mod); }
        }

        /// <summary>
        /// manual trigger to run all registered patches on the target.
        /// Currently, this must be called by the "Main" patcher or a bootstrap.
        /// </summary>
        public static IEnumerable<CodeInstruction> RunPipeline(MethodBase original, IEnumerable<CodeInstruction> instructions)
        {
            return RunPipeline(original, instructions, null);
        }

        /// <summary>
        /// Runs the cooperative pipeline with an optional ILGenerator so registrations
        /// can declare locals and labels just like a normal Harmony transpiler.
        /// </summary>
        public static IEnumerable<CodeInstruction> RunPipeline(MethodBase original, IEnumerable<CodeInstruction> instructions, ILGenerator generator)
        {
            List<PatcherRegistration> sortedPatches;
            lock (_lock)
            {
                if (!_registrations.ContainsKey(original))
                    return instructions;

                sortedPatches = OrderRegistrationsForExecution(_registrations[original]);
            }

            var currentInstructions = instructions.ToList();
            
            MMLog.WriteDebug($"[CooperativePatcher] Running pipeline for {original.Name} ({sortedPatches.Count} patches)");

            var appliedAnchors = new HashSet<string>();

            foreach (var patch in sortedPatches)
            {
                if (IsOwnerQuarantined(patch.OwnerMod))
                {
                    MMLog.WriteWarning($"[CooperativePatcher] Skipping {patch.OwnerMod}:{patch.AnchorId} - owner is quarantined due to prior critical patch failure.");
                    continue;
                }

                // Dependency Check
                if (patch.DependsOn.Length > 0)
                {
                    var missing = patch.DependsOn.Where(d => !appliedAnchors.Contains(d)).ToList();
                    if (missing.Any())
                    {
                        MMLog.WriteWarning($"[CooperativePatcher] Skipping {patch.OwnerMod}:{patch.AnchorId} - missing dependencies: {string.Join(", ", missing.ToArray())}");
                        continue;
                    }
                }

                // Conflict Check
                if (patch.ConflictsWith.Length > 0)
                {
                    var conflicts = patch.ConflictsWith.Where(c => appliedAnchors.Contains(c)).ToList();
                    if (conflicts.Any())
                    {
                        MMLog.WriteWarning($"[CooperativePatcher] Skipping {patch.OwnerMod}:{patch.AnchorId} - conflicts with applied patches: {string.Join(", ", conflicts.ToArray())}");
                        continue;
                    }
                }

                try
                {
                    MMLog.WriteDebug($"[CooperativePatcher] Applying {patch.OwnerMod} : {patch.AnchorId}");

                    var beforeInstructions = currentInstructions.ToList();
                    var sw = System.Diagnostics.Stopwatch.StartNew();

                    // Create transpiler wrapper on the CURRENT instructions
                    // We use valid COPY of the instructions to ensure isolation
                    var t = FluentTranspiler.For(currentInstructions, original, generator);

                    // Run logic
                    t = patch.PatchLogic(t);
                    if (t == null)
                    {
                        throw new InvalidOperationException($"Patch logic returned null for {patch.OwnerMod}:{patch.AnchorId}");
                    }

                    // Build strictness is policy-driven so safer defaults can be enforced globally.
                    var nextInstructions = t.Build(TranspilerSafetyPolicy.DefaultCooperativeProfile);
                    if (t.Diagnostics.Any(TranspilerSafetyPolicy.IsCriticalDiagnostic))
                    {
                        throw new InvalidOperationException(
                            $"Critical transpiler warnings for {patch.OwnerMod}:{patch.AnchorId}: " +
                            string.Join("; ", BuildDiagnosticLines(t).ToArray()));
                    }

                    if (t.Warnings.Count > 0)
                    {
                         MMLog.WriteWarning(
                            $"[CooperativePatcher] {patch.OwnerMod}:{patch.AnchorId} resulted in warnings: " +
                            string.Join("; ", BuildDiagnosticLines(t).ToArray()));
                    }
                    
                    // If successful, update current instructions and mark anchored
                    // This atomic swap prevents partial corruption if PatchLogic throws or Build fails
                    currentInstructions = nextInstructions.ToList();
                    appliedAnchors.Add(patch.AnchorId);

                    sw.Stop();
                    var origin = "CooperativePatcher|" + patch.OwnerMod + "|" + patch.AnchorId + "|Priority:" + patch.Priority;
                    var stepName = original != null && original.DeclaringType != null
                        ? original.DeclaringType.FullName + "." + original.Name
                        : (original != null ? original.Name : "UnknownMethod");
                    if (TranspilerSafetyPolicy.ShouldRecordDebugSnapshot(t.Warnings.Count, t.SoftFailures.Count, t.Notes.Count))
                    {
                        TranspilerDebugger.RecordSnapshot(
                            patch.OwnerMod,
                            stepName,
                            beforeInstructions,
                            currentInstructions,
                            sw.Elapsed.TotalMilliseconds,
                            t.Warnings.Count,
                            original,
                            origin,
                            warnings: BuildDiagnosticLines(t));
                        MMLog.WriteDebug("[CooperativePatcher] Snapshot recorded for patch origin: " + origin);
                    }
                }
                catch (Exception ex)
                {
                    MMLog.WriteError($"[CooperativePatcher] Patch {patch.OwnerMod}:{patch.AnchorId} FAILED and was skipped. Error: {ex.Message}");
                    QuarantineOwnerIfEnabled(patch.OwnerMod, patch.AnchorId);
                    // Continue with previous valid instructions - 'currentInstructions' remains untouched by this iteration
                }
            }

            return currentInstructions;
        }

        private static List<PatcherRegistration> OrderRegistrationsForExecution(List<PatcherRegistration> registrations)
        {
            var ordered = new List<PatcherRegistration>();
            if (registrations == null || registrations.Count == 0) return ordered;

            var all = registrations.ToList();
            var canonicalProviders = all
                .OrderBy(SortKey)
                .GroupBy(r => r.AnchorId ?? string.Empty, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            var indegree = new Dictionary<PatcherRegistration, int>();
            var outgoing = new Dictionary<PatcherRegistration, List<PatcherRegistration>>();
            for (int i = 0; i < all.Count; i++)
            {
                indegree[all[i]] = 0;
                outgoing[all[i]] = new List<PatcherRegistration>();
            }

            for (int i = 0; i < all.Count; i++)
            {
                var patch = all[i];
                var dependencies = patch.DependsOn ?? new string[0];
                for (int d = 0; d < dependencies.Length; d++)
                {
                    var dependency = dependencies[d];
                    if (string.IsNullOrEmpty(dependency)) continue;
                    if (!canonicalProviders.TryGetValue(dependency, out var provider)) continue;
                    if (ReferenceEquals(provider, patch)) continue;

                    outgoing[provider].Add(patch);
                    indegree[patch]++;
                }
            }

            var ready = all.Where(p => indegree[p] == 0).OrderBy(SortKey).ToList();
            while (ready.Count > 0)
            {
                var next = ready[0];
                ready.RemoveAt(0);
                ordered.Add(next);

                var dependents = outgoing[next];
                for (int i = 0; i < dependents.Count; i++)
                {
                    var dependent = dependents[i];
                    indegree[dependent]--;
                    if (indegree[dependent] == 0)
                    {
                        ready.Add(dependent);
                    }
                }

                ready = ready.OrderBy(SortKey).ToList();
            }

            if (ordered.Count != all.Count)
            {
                var unresolved = all.Except(ordered).OrderBy(SortKey).ToList();
                MMLog.WriteWarning(
                    "[CooperativePatcher] Dependency cycle or ambiguous dependency ordering detected. " +
                    "Falling back to priority order for: " +
                    string.Join(", ", unresolved.Select(p => p.OwnerMod + ":" + p.AnchorId).ToArray()));
                ordered.AddRange(unresolved);
            }

            return ordered;
        }

        private static string SortKey(PatcherRegistration registration)
        {
            if (registration == null) return string.Empty;
            return ((int)registration.Priority).ToString("D4") + "|" +
                   (registration.AnchorId ?? string.Empty) + "|" +
                   (registration.OwnerMod ?? string.Empty);
        }

        private static bool IsOwnerQuarantined(string ownerMod)
        {
            if (string.IsNullOrEmpty(ownerMod)) return false;
            lock (_quarantineLock)
            {
                return _quarantinedOwners.Contains(ownerMod);
            }
        }

        private static List<string> BuildDiagnosticLines(FluentTranspiler transpiler)
        {
            var lines = new List<string>();
            if (transpiler == null)
            {
                return lines;
            }

            lines.AddRange(transpiler.PatchDiagnostics.Select(diagnostic => diagnostic.ToSingleLine()));
            lines.AddRange(transpiler.Diagnostics.Select(diagnostic => diagnostic.ToString()));
            return lines.Distinct().ToList();
        }

        private static void QuarantineOwnerIfEnabled(string ownerMod, string anchorId)
        {
            if (!TranspilerSafetyPolicy.QuarantineOwnerOnFailure) return;
            if (string.IsNullOrEmpty(ownerMod)) return;

            lock (_quarantineLock)
            {
                _quarantinedOwners.Add(ownerMod);
            }
            MMLog.WriteWarning($"[CooperativePatcher] Quarantined owner '{ownerMod}' after failure in anchor '{anchorId}'. Disable with ModPrefs.TranspilerQuarantineOnFailure=false if needed.");
        }
    }
}
