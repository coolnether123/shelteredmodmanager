using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private static readonly HashSet<string> _quarantinedOwners = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Registers a cooperative transpiler. 
        /// NOTE: This does not apply the patch immediately. You must call Apply() or ensure ModAPI's master patcher is running.
        /// </summary>
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
            lock (_lock)
            {
                string mod = ownerMod ?? Assembly.GetCallingAssembly().GetName().Name;
                foreach (var list in _registrations.Values)
                {
                    list.RemoveAll(r => r.OwnerMod == mod);
                }
                _quarantinedOwners.Remove(mod);
            }
        }

        /// <summary>
        /// manual trigger to run all registered patches on the target.
        /// Currently, this must be called by the "Main" patcher or a bootstrap.
        /// </summary>
        public static IEnumerable<CodeInstruction> RunPipeline(MethodBase original, IEnumerable<CodeInstruction> instructions)
        {
            List<PatcherRegistration> sortedPatches;
            lock (_lock)
            {
                if (!_registrations.ContainsKey(original))
                    return instructions;
                
                // TODO: Implement topological sort based on DependsOn if needed. For now, Priority is primary.
                sortedPatches = _registrations[original].OrderBy(p => p.Priority).ToList();
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
                    var t = FluentTranspiler.For(currentInstructions, original);
                    
                    // Run logic
                    t = patch.PatchLogic(t);
                    
                    // Build strictness is policy-driven so safer defaults can be enforced globally.
                    bool strictBuild = TranspilerSafetyPolicy.CooperativeStrictBuild;
                    var nextInstructions = t.Build(strict: strictBuild, validateStack: true);

                    if (t.Warnings.Any(w => !w.StartsWith("DeclareLocal"))) // Filter informational
                    {
                         MMLog.WriteWarning(
                            $"[CooperativePatcher] {patch.OwnerMod}:{patch.AnchorId} resulted in warnings: " +
                            string.Join("; ", t.Warnings.ToArray()));
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
                    TranspilerDebugger.RecordSnapshot(
                        patch.OwnerMod,
                        stepName,
                        beforeInstructions,
                        currentInstructions,
                        sw.Elapsed.TotalMilliseconds,
                        t.Warnings != null ? t.Warnings.Count : 0,
                        original,
                        origin);
                    MMLog.WriteDebug("[CooperativePatcher] Snapshot recorded for patch origin: " + origin);
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

        private static bool IsOwnerQuarantined(string ownerMod)
        {
            if (string.IsNullOrEmpty(ownerMod)) return false;
            lock (_lock)
            {
                return _quarantinedOwners.Contains(ownerMod);
            }
        }

        private static void QuarantineOwnerIfEnabled(string ownerMod, string anchorId)
        {
            if (!TranspilerSafetyPolicy.QuarantineOwnerOnFailure) return;
            if (string.IsNullOrEmpty(ownerMod)) return;

            lock (_lock)
            {
                _quarantinedOwners.Add(ownerMod);
            }
            MMLog.WriteWarning($"[CooperativePatcher] Quarantined owner '{ownerMod}' after failure in anchor '{anchorId}'. Disable with ModPrefs.TranspilerQuarantineOnFailure=false if needed.");
        }
    }
}
