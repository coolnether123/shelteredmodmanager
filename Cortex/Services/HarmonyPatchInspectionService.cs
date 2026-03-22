using System;
using Cortex.Core.Abstractions;
using Cortex.Core.Models;

namespace Cortex.Services
{
    internal sealed class HarmonyPatchInspectionService
    {
        private static readonly TimeSpan SnapshotTtl = TimeSpan.FromSeconds(4d);
        private readonly IHarmonyRuntimeInspectionService _runtimeInspectionService;
        private readonly HarmonyPatchOwnershipService _ownershipService;
        private readonly HarmonyPatchOrderService _orderService;

        public HarmonyPatchInspectionService(
            IHarmonyRuntimeInspectionService runtimeInspectionService,
            HarmonyPatchOwnershipService ownershipService,
            HarmonyPatchOrderService orderService)
        {
            _runtimeInspectionService = runtimeInspectionService;
            _ownershipService = ownershipService;
            _orderService = orderService;
        }

        public bool IsAvailable
        {
            get { return _runtimeInspectionService != null && _runtimeInspectionService.IsAvailable; }
        }

        public string BuildKey(HarmonyPatchInspectionRequest request)
        {
            if (request == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrEmpty(request.AssemblyPath) && request.MetadataToken > 0)
            {
                return request.AssemblyPath + "|0x" + request.MetadataToken.ToString("X8");
            }

            return (request.AssemblyPath ?? string.Empty) + "|" +
                (request.DeclaringTypeName ?? string.Empty) + "|" +
                (request.MethodName ?? string.Empty) + "|" +
                (request.Signature ?? string.Empty);
        }

        public void RefreshSnapshot(CortexShellState state, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog)
        {
            if (state == null || state.Harmony == null)
            {
                return;
            }

            state.Harmony.RuntimeAvailable = IsAvailable;
            state.Harmony.RefreshRequested = false;
            if (!IsAvailable)
            {
                state.Harmony.SnapshotMethods = new HarmonyMethodPatchSummary[0];
                state.Harmony.SummaryCache.Clear();
                state.Harmony.SnapshotUtc = DateTime.UtcNow;
                state.Harmony.SnapshotStatusMessage = "Harmony runtime inspection is not available in the active host.";
                MMLog.WriteInfo("[Cortex.Harmony] Runtime inspection unavailable for the active host platform.");
                return;
            }

            var snapshot = _runtimeInspectionService.CaptureSnapshot() ?? new HarmonyPatchSnapshot();
            var methods = snapshot.Methods ?? new HarmonyMethodPatchSummary[0];
            state.Harmony.SnapshotMethods = methods;
            state.Harmony.SnapshotUtc = snapshot.GeneratedUtc != DateTime.MinValue ? snapshot.GeneratedUtc : DateTime.UtcNow;
            state.Harmony.SnapshotStatusMessage = snapshot.StatusMessage ?? string.Empty;
            MMLog.WriteInfo("[Cortex.Harmony] Snapshot refreshed. PatchedMethods=" + methods.Length + ", Status='" + (state.Harmony.SnapshotStatusMessage ?? string.Empty) + "'.");
            state.Harmony.SummaryCache.Clear();
            for (var i = 0; i < methods.Length; i++)
            {
                var normalized = Normalize(methods[i], loadedModCatalog, projectCatalog);
                if (normalized == null)
                {
                    continue;
                }

                methods[i] = normalized;
                var key = BuildKey(new HarmonyPatchInspectionRequest
                {
                    AssemblyPath = normalized.AssemblyPath,
                    MetadataToken = normalized.Target != null ? normalized.Target.MetadataToken : 0,
                    DeclaringTypeName = normalized.DeclaringType,
                    MethodName = normalized.MethodName,
                    Signature = normalized.Signature
                });
                if (!string.IsNullOrEmpty(key))
                {
                    state.Harmony.SummaryCache[key] = normalized;
                }
            }
        }

        public HarmonyMethodPatchSummary GetSummary(
            CortexShellState state,
            HarmonyPatchInspectionRequest request,
            ILoadedModCatalog loadedModCatalog,
            IProjectCatalog projectCatalog,
            bool forceRefresh,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || state.Harmony == null || request == null)
            {
                statusMessage = "Harmony request was incomplete.";
                return null;
            }

            var key = BuildKey(request);
            MMLog.WriteInfo("[Cortex.Harmony] GetSummary requested. ForceRefresh=" + forceRefresh +
                ", Key='" + (key ?? string.Empty) +
                "', Assembly='" + (request.AssemblyPath ?? string.Empty) +
                "', MetadataToken=0x" + request.MetadataToken.ToString("X8") +
                ", Display='" + (request.DisplayName ?? string.Empty) + "'.");
            if (forceRefresh || ShouldRefreshSnapshot(state))
            {
                RefreshSnapshot(state, loadedModCatalog, projectCatalog);
            }

            HarmonyMethodPatchSummary cached;
            if (!forceRefresh &&
                !string.IsNullOrEmpty(key) &&
                state.Harmony.SummaryCache.TryGetValue(key, out cached) &&
                cached != null)
            {
                statusMessage = state.Harmony.SnapshotStatusMessage ?? string.Empty;
                MMLog.WriteInfo("[Cortex.Harmony] Summary cache hit for key '" + key +
                    "'. Patched=" + cached.IsPatched +
                    ", TotalPatches=" + (cached.Counts != null ? cached.Counts.TotalCount : 0) + ".");
                return cached;
            }

            if (!IsAvailable)
            {
                statusMessage = "Harmony runtime inspection is not available in the active host.";
                return null;
            }

            var summary = _runtimeInspectionService.Inspect(request);
            if (summary == null)
            {
                statusMessage = "No Harmony metadata was returned for the selected method.";
                MMLog.WriteWarning("[Cortex.Harmony] Runtime inspection returned no summary for key '" + (key ?? string.Empty) + "'.");
                return null;
            }

            summary = Normalize(summary, loadedModCatalog, projectCatalog);
            if (!string.IsNullOrEmpty(key))
            {
                state.Harmony.SummaryCache[key] = summary;
            }

            statusMessage = summary.IsPatched
                ? "Loaded Harmony patch details for " + (summary.MethodName ?? string.Empty) + "."
                : "No live Harmony patches are registered for " + (summary.MethodName ?? string.Empty) + ".";
            MMLog.WriteInfo("[Cortex.Harmony] Summary loaded for '" + (summary.ResolvedMemberDisplayName ?? summary.MethodName ?? string.Empty) +
                "'. Patched=" + summary.IsPatched + ", TotalPatches=" + (summary.Counts != null ? summary.Counts.TotalCount : 0) + ".");
            return summary;
        }

        public HarmonyMethodPatchSummary GetCachedSummary(CortexShellState state, HarmonyPatchInspectionRequest request)
        {
            if (state == null || state.Harmony == null || request == null)
            {
                return null;
            }

            HarmonyMethodPatchSummary summary;
            return state.Harmony.SummaryCache.TryGetValue(BuildKey(request), out summary)
                ? summary
                : null;
        }

        public HarmonyMethodPatchSummary GetCachedSummary(
            CortexShellState state,
            HarmonyPatchInspectionRequest request,
            ILoadedModCatalog loadedModCatalog,
            IProjectCatalog projectCatalog,
            bool initializeSnapshot,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || state.Harmony == null || request == null)
            {
                return null;
            }

            if (initializeSnapshot && (state.Harmony.RefreshRequested || state.Harmony.SnapshotUtc == DateTime.MinValue))
            {
                RefreshSnapshot(state, loadedModCatalog, projectCatalog);
            }

            var cached = GetCachedSummary(state, request);
            statusMessage = cached != null
                ? state.Harmony.SnapshotStatusMessage ?? string.Empty
                : "Harmony patch data has not been loaded for this method yet.";
            return cached;
        }

        public HarmonyMethodPatchSummary[] GetTypeSummaries(
            CortexShellState state,
            string assemblyPath,
            string declaringTypeName,
            ILoadedModCatalog loadedModCatalog,
            IProjectCatalog projectCatalog,
            bool forceRefresh,
            out string statusMessage)
        {
            statusMessage = string.Empty;
            if (state == null || state.Harmony == null || string.IsNullOrEmpty(assemblyPath) || string.IsNullOrEmpty(declaringTypeName))
            {
                statusMessage = "Harmony type request was incomplete.";
                return new HarmonyMethodPatchSummary[0];
            }

            if (forceRefresh || ShouldRefreshSnapshot(state))
            {
                RefreshSnapshot(state, loadedModCatalog, projectCatalog);
            }

            if (!IsAvailable)
            {
                statusMessage = "Harmony runtime inspection is not available in the active host.";
                return new HarmonyMethodPatchSummary[0];
            }

            var snapshotMethods = state.Harmony.SnapshotMethods ?? new HarmonyMethodPatchSummary[0];
            var matches = new System.Collections.Generic.List<HarmonyMethodPatchSummary>();
            for (var i = 0; i < snapshotMethods.Length; i++)
            {
                var current = snapshotMethods[i];
                if (current == null ||
                    !string.Equals(current.AssemblyPath ?? string.Empty, assemblyPath, StringComparison.OrdinalIgnoreCase) ||
                    !string.Equals(current.DeclaringType ?? string.Empty, declaringTypeName, StringComparison.Ordinal))
                {
                    continue;
                }

                matches.Add(current);
            }

            statusMessage = matches.Count > 0
                ? "Loaded Harmony patch details for " + declaringTypeName + "."
                : "No live Harmony patches are registered for " + declaringTypeName + ".";
            MMLog.WriteInfo("[Cortex.Harmony] Type summary loaded. DeclaringType='" + declaringTypeName +
                "', Assembly='" + assemblyPath +
                "', PatchedMethods=" + matches.Count + ".");
            return matches.ToArray();
        }

        private bool ShouldRefreshSnapshot(CortexShellState state)
        {
            if (state == null || state.Harmony == null)
            {
                return false;
            }

            return state.Harmony.RefreshRequested ||
                state.Harmony.SnapshotUtc == DateTime.MinValue ||
                DateTime.UtcNow - state.Harmony.SnapshotUtc >= SnapshotTtl;
        }

        private HarmonyMethodPatchSummary Normalize(HarmonyMethodPatchSummary summary, ILoadedModCatalog loadedModCatalog, IProjectCatalog projectCatalog)
        {
            if (summary == null)
            {
                return null;
            }

            summary.Counts = summary.Counts ?? new HarmonyPatchCounts();
            summary.Entries = summary.Entries ?? new HarmonyPatchEntry[0];
            summary.Order = summary.Order ?? new HarmonyPatchOrderExplanation[0];
            summary.Owners = summary.Owners ?? new string[0];
            if (summary.Target == null)
            {
                summary.Target = new HarmonyPatchNavigationTarget();
            }

            if (string.IsNullOrEmpty(summary.Target.DisplayName))
            {
                summary.Target.DisplayName = !string.IsNullOrEmpty(summary.ResolvedMemberDisplayName)
                    ? summary.ResolvedMemberDisplayName
                    : (summary.DeclaringType ?? string.Empty) + "." + (summary.MethodName ?? string.Empty) + (summary.Signature ?? string.Empty);
            }

            if (string.IsNullOrEmpty(summary.Target.AssemblyPath))
            {
                summary.Target.AssemblyPath = summary.AssemblyPath ?? string.Empty;
            }

            if (string.IsNullOrEmpty(summary.DocumentPath))
            {
                summary.DocumentPath = summary.Target.DocumentPath ?? string.Empty;
            }

            if (string.IsNullOrEmpty(summary.CachePath))
            {
                summary.CachePath = summary.Target.CachePath ?? string.Empty;
            }

            var targetProject = _ownershipService != null
                ? _ownershipService.ResolveProjectForAssembly(summary.AssemblyPath, projectCatalog)
                : null;
            if (targetProject != null)
            {
                summary.ProjectModId = targetProject.ModId ?? string.Empty;
                summary.ProjectSourceRootPath = targetProject.SourceRootPath ?? string.Empty;
            }

            var targetMod = _ownershipService != null
                ? _ownershipService.ResolveLoadedModForAssembly(summary.AssemblyPath, loadedModCatalog)
                : null;
            if (targetMod != null)
            {
                summary.LoadedModId = targetMod.ModId ?? string.Empty;
                summary.LoadedModRootPath = targetMod.RootPath ?? string.Empty;
            }

            for (var i = 0; i < summary.Entries.Length; i++)
            {
                var entry = summary.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                entry.Before = entry.Before ?? new string[0];
                entry.After = entry.After ?? new string[0];
                entry.OwnerAssociation = _ownershipService != null
                    ? _ownershipService.Resolve(entry.OwnerId, entry.AssemblyPath, loadedModCatalog, projectCatalog)
                    : entry.OwnerAssociation;
                if (entry.OwnerAssociation != null && entry.OwnerAssociation.HasMatch)
                {
                    entry.OwnerDisplayName = entry.OwnerAssociation.DisplayName ?? entry.OwnerDisplayName;
                }
            }

            if (_orderService != null)
            {
                summary.Order = _orderService.BuildOrder(summary);
            }

            if (_orderService != null)
            {
                summary.ConflictHint = _orderService.BuildConflictHint(summary);
            }

            if (summary.CapturedUtc == DateTime.MinValue)
            {
                summary.CapturedUtc = DateTime.UtcNow;
            }

            return summary;
        }
    }
}
